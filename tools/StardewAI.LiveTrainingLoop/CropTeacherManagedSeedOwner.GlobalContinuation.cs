using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // An operational handoff references accepted native work; it is not a new terminal proof.
    internal async Task<JsonObject> ReadTerminalGlobalContinuationAsync(CropTeacherManagedSeedOperation operation,
        CropTeacherTerminalShippingTarget target, CropTeacherManagedProductionEvidence production)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired || !ReferenceEquals(operation.Owner, this) ||
                !ReferenceEquals(operation, lastAcceptedOperation) || !operation.IsOvernight ||
                !HasCompletedTerminalDeposits || target != terminalTarget)
                throw new InvalidOperationException("Global continuation requires this owner's accepted terminal overnight and completed deposits.");
            if (terminalProductionReference is not { } reference ||
                !SamePath(reference.ManifestPath, production.ManifestPath) ||
                reference.ManifestSha256 != production.ManifestSha256 || HashFile(reference.ManifestPath) != reference.ManifestSha256)
                throw new InvalidDataException("Global continuation lost the originally selected production certificate.");

            var savePath = AcceptedNativeSaveBoundaryPath();
            var save = Read<JsonObject>(savePath);
            var source = Read<JsonObject>(headSnapshotPath);
            if (save["native_save_boundary_verified"]?.GetValue<bool>() != true ||
                save["from_total_day"]?.GetValue<int>() != target.TotalDay ||
                save["to_total_day"]?.GetValue<int>() != target.TotalDay + 1 ||
                source["state"]?["time"]?["total_days"]?["value"]?.GetValue<int>() != target.TotalDay + 1)
                throw new InvalidDataException("Global continuation requires the original terminal day's accepted native save.");

            var hashes = new Dictionary<string, string>(fixedHashes, PathComparer);
            foreach (var (path, hash) in frontierHashes) hashes[path] = hash;
            hashes[savePath] = HashFile(savePath);
            hashes[reference.ManifestPath] = reference.ManifestSha256;
            return new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_global_continuation.v1",
                ["run_id"] = options.RunId, ["save_id"] = saveId, ["player_id"] = playerId,
                ["owner_directory"] = options.EvidenceDirectory, ["native_run_plan_path"] = options.NativeRunPlanPath,
                ["accepted_operation_path"] = lastAcceptedPath, ["accepted_operation_sha256"] = lastAcceptedHash,
                ["accepted_snapshot_path"] = headSnapshotPath, ["accepted_ledger_path"] = headLedgerPath,
                ["after_state_hash"] = Text(source, "state_hash"), ["to_total_day"] = target.TotalDay + 1,
                ["overnight_save_boundary_path"] = savePath, ["terminal_production_manifest_path"] = reference.ManifestPath,
                ["terminal_deposit_count"] = target.AcceptedDepositCount,
                ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(hashes, JsonOptions),
                ["operational_handoff_ready"] = true, ["native_save_boundary_verified"] = true,
                ["terminal_settlement_verified"] = false, ["semantic_saved_content_verified"] = false,
                ["complete_managed_runtime_verified"] = false, ["teacher_preference_label_eligible"] = false,
                ["formal_product_training_authorized"] = false
            };
        }
        finally { gate.Release(); }
    }
}
