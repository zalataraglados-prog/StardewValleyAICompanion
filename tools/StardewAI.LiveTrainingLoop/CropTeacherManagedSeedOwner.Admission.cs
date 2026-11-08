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
            var record = CreateAdmissionRecord(beforeSnapshotPath, ledgerPath, runtimeDirectory, priorVerifiedSnapshotPath);
            record["step"] = JsonSerializer.SerializeToNode(step, JsonOptions);
            var scopes = OpenScopes(record);
            record["original_intent_sha256"] = SeedIntentHash(record, scopes, step);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), scopes)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private JsonObject CreateAdmissionRecord(string before, string ledger, string runtimeDirectory, string? prior = null)
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
        return new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_seed_admission.v1", ["sequence"] = sequence,
            ["run_id"] = options.RunId, ["support_step_id"] = StepId(sequence),
            ["previous_accepted_sha256"] = lastAcceptedHash, ["runtime_directory"] = runtime,
            ["before_snapshot_path"] = Path.GetFullPath(before), ["before_ledger_path"] = Path.GetFullPath(ledger),
            ["prior_verified_snapshot_path"] = prior is null ? null : Path.GetFullPath(prior),
            ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
        };
    }

    private async Task<CropTeacherManagedSeedOperation> PersistAdmissionAsync(JsonObject record, CropTeacherManagedSeedOperation operation)
    {
        record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(OperationInputs(operation), JsonOptions);
        await SaveAsync(operation.RecordPath, record).ConfigureAwait(false);
        Pending = operation;
        TrackFrontier(operation.RecordPath);
        return operation;
    }

    public async Task PinPreparedAsync(CropTeacherManagedSeedOperation operation, ICropTeacherSeedRuntime runtime)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await PinPreparedCoreAsync(operation, runtime).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task PinPreparedCoreAsync(CropTeacherManagedSeedOperation operation, ICropTeacherSeedRuntime runtime)
    {
        RequireOperation(operation);
        if (operation.IsLifecycle) throw new InvalidOperationException("Lifecycle operations have no native seed preparation.");
        if (operation.Prepared is not null || runtime is not ICropTeacherSeedManagedRuntime original)
            throw new InvalidOperationException("Managed crop preparation must come once from an original runtime transaction.");
        var prepared = original.ReadPreparedBoundary();
        RequireIdentity(operation, prepared.RuntimeDirectory, prepared.BackendUrl, prepared.RunId, prepared.ExecutorVersion, prepared.SupportStepId);
        RequireAdmittedSeedIntent(operation, prepared.OriginalIntentSha256);
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
        if (operation.Started is not null)
        {
            var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory,
                "commit", prepared.PreparationRequest, JsonOptions);
            TrackFrontier(journal.RequestPath);
            TrackFrontier(journal.JournalPath);
            _ = await PortfolioMutationRequestJournal.ReadAsync(journal.JournalPath, journal.RequestPath, journal.Expected, journal.Hash).ConfigureAwait(false);
            ValidateState();
        }
        if (HasOwnedDispatchProtocol(operation)) AttachPreparedRuntime(operation, runtime);
        await PersistPreparedAsync(operation, prepared.OriginalIntentSha256, prepared.QueuePath, prepared.QueueSha256,
            preparationReference: prepared.PreparationReference).ConfigureAwait(false);
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
