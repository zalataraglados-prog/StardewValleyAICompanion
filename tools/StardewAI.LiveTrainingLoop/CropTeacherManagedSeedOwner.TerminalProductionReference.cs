using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private CropTeacherTerminalProductionReference? terminalProductionReference;

    private static CropTeacherTerminalProductionReference? ReadTerminalProductionReference(JsonObject record)
    {
        if (record["terminal_production_manifest_path"] is null && record["terminal_production_manifest_sha256"] is null) return null;
        return new(StoredPath(record, "terminal_production_manifest_path"), Text(record, "terminal_production_manifest_sha256"));
    }

    private void ValidateTerminalProductionReference(JsonObject record)
    {
        var reference = ReadTerminalProductionReference(record);
        if (reference is null)
        {
            if (terminalProductionReference is not null)
                throw new InvalidDataException("Terminal production reference cannot disappear from the accepted continuation.");
            return; // Keep the original calibration contract when no production certificate was selected.
        }
        var output = Path.GetDirectoryName(reference.ManifestPath)!;
        if (!Within(options.EvidenceDirectory, reference.ManifestPath) || SamePath(options.EvidenceDirectory, reference.ManifestPath))
            throw new IOException("Terminal production reference must be the original owned certificate.");
        RequireDirectoryPath(output);
        if (HashFile(reference.ManifestPath) != reference.ManifestSha256 ||
            terminalProductionReference is not null && reference != terminalProductionReference)
            throw new InvalidDataException("Terminal production certificate reference/hash changed.");
        if (terminalProductionReference is not null) return;
        var manifest = Read<JsonObject>(reference.ManifestPath);
        var target = record["terminal_target"]!.Deserialize<CropTeacherTerminalShippingTarget>(JsonOptions)!;
        if (target.AcceptedDepositCount != 0 ||
            Text(manifest, "schema_version") != "crop_teacher_managed_production_evidence.v1" ||
            !SamePath(StoredPath(manifest, "native_run_plan_path"), options.NativeRunPlanPath) ||
            !SamePath(StoredPath(manifest, "accepted_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(manifest, "accepted_ledger_path"), headLedgerPath) ||
            manifest["native_crop_lifecycle_verified"]?.GetValue<bool>() != true ||
            manifest["whole_wave_output_retention_verified"]?.GetValue<bool>() != true ||
            manifest["terminal_settlement_verified"]?.GetValue<bool>() != false ||
            manifest["formal_product_training_authorized"]?.GetValue<bool>() != false ||
            !(manifest["output_retention_proof_paths"]?.Deserialize<string[]>(JsonOptions) ?? [])
                .Any(path => SamePath(path, target.RetentionProofPath)))
            throw new InvalidDataException("Terminal production reference is not this owner's original selected production head.");
    }

    private static JsonObject AddTerminalProductionIntentReference(JsonObject record, JsonObject intent)
    {
        if (ReadTerminalProductionReference(record) is { } reference)
        {
            intent["terminal_production_manifest_path"] = reference.ManifestPath;
            intent["terminal_production_manifest_sha256"] = reference.ManifestSha256;
        }
        return intent;
    }

    private void AddTerminalProductionReference(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (ReadTerminalProductionReference(operation.Record) is { } reference)
        {
            ValidateTerminalProductionReference(operation.Record);
            accepted["terminal_production_manifest_sha256"] = reference.ManifestSha256;
        }
    }

    private void ValidateTerminalProductionReferenceAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        var reference = operation.IsTerminalShipping ? ReadTerminalProductionReference(operation.Record) : null;
        if (reference is null ? accepted["terminal_production_manifest_sha256"] is not null :
            accepted["terminal_production_manifest_sha256"]?.GetValue<string>() != reference.ManifestSha256)
            throw new InvalidDataException("Accepted terminal production certificate reference changed or belongs to another phase.");
    }
}
