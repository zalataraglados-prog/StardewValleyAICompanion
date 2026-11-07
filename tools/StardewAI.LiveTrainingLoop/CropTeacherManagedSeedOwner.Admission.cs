using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> AdmitAsync(CropTeacherRunStep step, string beforeSnapshotPath,
        string ledgerPath, string runtimeDirectory, string? priorVerifiedSnapshotPath = null)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Managed crop owner must recover its retained phase before another admission.");
            var sequence = checked((lastAcceptedOperation?.Sequence ?? 0) + 1);
            var runtime = Path.GetFullPath(runtimeDirectory);
            if (!Within(options.EvidenceDirectory, runtime) || SamePath(options.EvidenceDirectory, runtime) ||
                Directory.Exists(runtime) || File.Exists(runtime) || ReservedRuntimePath(runtime))
                throw new IOException("Managed crop operation requires a new runtime directory inside its owner.");
            RequireDirectoryPath(runtime);
            var record = new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_seed_admission.v1", ["sequence"] = sequence,
                ["run_id"] = options.RunId, ["support_step_id"] = StepId(sequence),
                ["previous_accepted_sha256"] = lastAcceptedHash, ["runtime_directory"] = runtime,
                ["before_snapshot_path"] = Path.GetFullPath(beforeSnapshotPath), ["before_ledger_path"] = Path.GetFullPath(ledgerPath),
                ["prior_verified_snapshot_path"] = priorVerifiedSnapshotPath is null ? null : Path.GetFullPath(priorVerifiedSnapshotPath),
                ["step"] = JsonSerializer.SerializeToNode(step, JsonOptions),
                ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
            };
            var scopes = OpenScopes(record);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(Inputs(scopes), JsonOptions);
            var path = RecordPath(sequence, "intent");
            await SaveAsync(path, record).ConfigureAwait(false);
            Pending = new(this, record, path, scopes);
            TrackFrontier(path);
            return Pending;
        }
        finally { gate.Release(); }
    }

    public async Task PinPreparedAsync(CropTeacherManagedSeedOperation operation, ICropTeacherSeedRuntime runtime)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (operation.Prepared is not null || runtime is not ICropTeacherSeedManagedRuntime original)
                throw new InvalidOperationException("Managed crop preparation must come once from an original runtime transaction.");
            var prepared = original.ReadPreparedBoundary();
            RequireIdentity(operation, prepared.RuntimeDirectory, prepared.BackendUrl, prepared.RunId, prepared.ExecutorVersion, prepared.SupportStepId);
            var step = operation.ReadStep();
            var request = operation.Scopes.Purchase?.MarkerRequest ?? operation.Scopes.Transfer?.PreparationRequest ?? operation.Scopes.Planting!.MarkerRequest;
            if (prepared.CommitmentId != step.CommitmentId ||
                !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(prepared.InputHashes, JsonOptions), operation.Record["input_artifact_hashes"]) ||
                !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(prepared.PreparationRequest, JsonOptions), JsonSerializer.SerializeToNode(request, JsonOptions)) ||
                operation.Scopes.Purchase is not null && runtime is not CropTeacherSeedPurchaseRuntime ||
                operation.Scopes.Transfer is not null && runtime is not CropTeacherSeedTransferRuntime ||
                operation.Scopes.Planting is not null && (runtime is not CropTeacherSeedPlantingRuntime ||
                    !JsonNode.DeepEquals(prepared.PreparationDetails?["requested_batch"], JsonSerializer.SerializeToNode(step.ActionBatch, JsonOptions)) ||
                    prepared.PreparationDetails?["reservation_id"]?.GetValue<string>() != step.SeedReservationId))
                throw new InvalidDataException("Original crop preparation differs from this owner's exact scope/step.");
            prepared.Validate();
            var record = new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_seed_preparation.v1", ["sequence"] = operation.Sequence,
                ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
                ["admission_sha256"] = HashFile(operation.RecordPath), ["original_intent_sha256"] = prepared.OriginalIntentSha256,
                ["queue_path"] = prepared.QueuePath, ["queue_sha256"] = prepared.QueueSha256,
                ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
            };
            var path = RecordPath(operation.Sequence, "prepared");
            await SaveAsync(path, record).ConfigureAwait(false);
            operation.Prepared = record;
            TrackFrontier(path);
        }
        finally { gate.Release(); }
    }

    private void RequireIdentity(CropTeacherManagedSeedOperation operation, string runtime, string backendUrl, string run,
        string executor, string stepId)
    {
        // Exact canonical text preserves the original intent/journal identity on restart.
        if (!SamePath(operation.RuntimeDirectory, runtime) || backendUrl != options.BackendUrl ||
            run != options.RunId || executor != options.ExecutorVersion ||
            stepId != operation.SupportStepId)
            throw new InvalidDataException("Original crop runtime is not bound to this owner's admitted phase.");
    }
}
