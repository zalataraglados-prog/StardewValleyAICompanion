using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task AcceptLiveAsync(CropTeacherManagedSeedOperation operation, ICropTeacherSeedRuntime runtime)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (operation.IsLifecycle || operation.IsSupportingRoute) throw new InvalidOperationException("Non-seed acceptance uses its original phase runtime.");
            if (HasOwnedDispatchProtocol(operation) && (operation.Dispatch is null || !ReferenceEquals(operation.PreparedRuntime, runtime)))
                throw new InvalidOperationException("Owned seed live acceptance requires its claimed original execution loop/runtime.");
            if (runtime is not ICropTeacherSeedManagedRuntime original)
                throw new InvalidOperationException("Managed crop acceptance requires the original verified runtime.");
            var boundary = original.ReadVerifiedBoundary();
            RequirePrepared(operation, boundary.Preparation.OriginalIntentSha256);
            RequireIdentity(operation, boundary.Preparation.RuntimeDirectory, boundary.Preparation.BackendUrl, boundary.Preparation.RunId,
                boundary.Preparation.ExecutorVersion, boundary.Preparation.SupportStepId);
            boundary.Validate();
            await AdvanceAsync(operation, boundary.Evidence, boundary.AfterSnapshotPath, boundary.Validate, "live_transaction").ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task RecoverPendingAsync(HttpClient http, string evidenceDirectory,
        Func<string, string, Task<JsonObject>> post)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            var operation = Pending ?? throw new InvalidOperationException("Managed crop owner has no pending phase.");
            RequireOperation(operation);
            var recovered = await RecoverAsync(http, operation, evidenceDirectory, post).ConfigureAwait(false);
            operation.VerifiedStop = recovered.Stop;
            if (!operation.IsLifecycle && !operation.IsSupportingRoute && operation.Prepared is null)
            {
                var seed = recovered.Seed!;
                seed.ValidateArtifactsUnchanged();
                await PersistPreparedAsync(operation, seed.OriginalIntentSha256, seed.PreparedQueuePath,
                    seed.PreparedQueueSha256, recoveredAfterNativeVerification: true,
                    preparationReference: seed.PreparationReference).ConfigureAwait(false);
                seed.ValidateArtifactsUnchanged();
            }
            await AdvanceAsync(operation, recovered.Evidence, recovered.SnapshotPath,
                recovered.Validate, "original_recovery").ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task RestoreHeadAsync(HttpClient http, string evidenceDirectory,
        Func<string, string, Task<JsonObject>> post)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || !HeadRecoveryRequired || lastAcceptedOperation is null)
                throw new InvalidOperationException("Recover the pending phase first, or no accepted head needs restoration.");
            var recovered = await RecoverAsync(http, lastAcceptedOperation, evidenceDirectory, post).ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(Read<StrategyCommitmentLedger>(headLedgerPath), recovered.Evidence.Ledger, "owner head restore");
            if (!SamePath(recovered.SnapshotPath, headSnapshotPath))
                throw new InvalidDataException("Recovered crop source differs from the accepted owner boundary.");
            liveHeadGuard = recovered.Validate;
            HeadRecoveryRequired = false;
        }
        finally { gate.Release(); }
    }

    private sealed record RecoveredOwnerBoundary(StrategyLedgerEvidence Evidence, string SnapshotPath, Action Validate,
        CropTeacherSeedRecoveredBoundary? Seed = null, CropTeacherSeedNoDispatchBoundary? Stop = null);

    private async Task<RecoveredOwnerBoundary> RecoverAsync(HttpClient http, CropTeacherManagedSeedOperation operation,
        string output, Func<string, string, Task<JsonObject>> post)
    {
        if (operation.IsSupportingRoute)
            return await RecoverSupportingRouteAsync(http, operation, output, post).ConfigureAwait(false);
        if (operation.IsLifecycle)
        {
            if (operation.Started is null) throw new InvalidOperationException("Managed lifecycle has not claimed an original invocation.");
            var value = LifecycleOptions(operation.Record);
            var lifecycle = await CropTeacherSeedLifecycleRecovery.RecoverAsync(http,
                new(options.BackendUrl, operation.RuntimeDirectory, options.PortfolioJournalDirectory, output, options.RunId,
                    Text(operation.Record, "original_intent_sha256"), value.Mode, value.CancellationReason), operation.Lifecycle!, post, write).ConfigureAwait(false);
            return new(new(lifecycle.LedgerPath, lifecycle.ReadLedger()), lifecycle.SnapshotPath, lifecycle.ValidateArtifactsUnchanged);
        }
        var intentHash = operation.Prepared is not null ? Text(operation.Prepared, "original_intent_sha256") :
            operation.Record["original_intent_sha256"]?.GetValue<string>() ??
            throw new InvalidOperationException("Legacy seed phase has no pinned intent/preparation; retain its original journals without replay.");
        if (operation.StopAccepted || HasOwnedDispatchProtocol(operation) && operation.Dispatch is not null &&
            File.Exists(Path.Combine(operation.RuntimeDirectory, "controller-stop-transition.json")))
        {
            var stopped = await CropTeacherSeedRuntimeRecovery.RecoverNoDispatchAsync(http,
                new(options.BackendUrl, operation.RuntimeDirectory, options.PortfolioJournalDirectory, output,
                    options.RunId, options.ExecutorVersion, intentHash) { PreparationReference = ReadPreparationReference(operation) },
                operation.Scopes, operation.ReadStep(), post, write).ConfigureAwait(false);
            return new(stopped.Evidence, stopped.SnapshotPath, stopped.Validate, Stop: stopped);
        }
        if (operation.Prepared is null && operation.Started is null && !File.Exists(Path.Combine(operation.RuntimeDirectory, "support-intent.json")))
            throw new InvalidOperationException("Seed phase has no retained original preparation to recover; this is not proof of zero execution.");
        var seed = await CropTeacherSeedRuntimeRecovery.RecoverAsync(http,
            new(options.BackendUrl, operation.RuntimeDirectory, options.PortfolioJournalDirectory, output,
                options.RunId, options.ExecutorVersion, intentHash) { PreparationReference = ReadPreparationReference(operation) },
            operation.Scopes, operation.ReadStep(), post, write).ConfigureAwait(false);
        return new(new(seed.LedgerPath, seed.ReadLedger()), seed.AfterSnapshotPath, seed.ValidateArtifactsUnchanged, seed);
    }

    private void RequirePrepared(CropTeacherManagedSeedOperation operation, string intentHash)
    {
        if (operation.Prepared is null || Text(operation.Prepared, "original_intent_sha256") != intentHash)
            throw new InvalidDataException("Managed crop acceptance lacks its pinned original preparation.");
    }

    private async Task AdvanceAsync(CropTeacherManagedSeedOperation operation, StrategyLedgerEvidence evidence,
        string afterSnapshotPath, Action validate, string source)
    {
        validate();
        var after = Read<SnapshotEnvelope>(afterSnapshotPath);
        if (after.StateHash != evidence.Ledger.SourceStateHash || after.SaveId.Value != saveId || after.PlayerId.Value != playerId ||
            evidence.Ledger.SaveId != saveId || evidence.Ledger.PlayerId != playerId)
            throw new InvalidDataException("Managed crop completion changed its original actor/source identity.");
        var accepted = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_seed_acceptance.v1", ["sequence"] = operation.Sequence,
            ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
            ["previous_accepted_sha256"] = lastAcceptedHash, ["admission_sha256"] = HashFile(operation.RecordPath),
            ["after_snapshot_path"] = Path.GetFullPath(afterSnapshotPath), ["after_snapshot_sha256"] = HashFile(afterSnapshotPath),
            ["after_ledger_path"] = Path.GetFullPath(evidence.Path), ["after_ledger_sha256"] = HashFile(evidence.Path),
            ["after_state_hash"] = after.StateHash, ["acceptance_source"] = source,
            ["original_boundary_verified"] = true, ["execution_authorized"] = false,
            ["formal_product_training_authorized"] = false
        };
        AddAcceptedPhaseEvidence(operation, accepted);
        ValidateState();
        validate();
        var path = RecordPath(operation.Sequence, "accepted");
        await SaveAsync(path, accepted).ConfigureAwait(false);
        SetHead(operation, accepted, path);
        Pending = null;
        HeadRecoveryRequired = false;
        liveHeadGuard = validate;
    }
}
