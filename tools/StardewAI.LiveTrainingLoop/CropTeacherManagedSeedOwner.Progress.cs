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
            await AdvanceAsync(operation, new(recovered.LedgerPath, recovered.ReadLedger()), recovered.AfterSnapshotPath,
                recovered.ValidateArtifactsUnchanged, "original_recovery").ConfigureAwait(false);
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
            CropTeacherSeedRuntimeTransaction.RequireLedger(Read<StrategyCommitmentLedger>(headLedgerPath), recovered.ReadLedger(), "owner head restore");
            if (!SamePath(recovered.AfterSnapshotPath, headSnapshotPath))
                throw new InvalidDataException("Recovered crop source differs from the accepted owner boundary.");
            liveHeadGuard = recovered.ValidateArtifactsUnchanged;
            HeadRecoveryRequired = false;
        }
        finally { gate.Release(); }
    }

    private Task<CropTeacherSeedRecoveredBoundary> RecoverAsync(HttpClient http, CropTeacherManagedSeedOperation operation,
        string output, Func<string, string, Task<JsonObject>> post)
    {
        var intentHash = operation.Prepared is null
            ? throw new InvalidOperationException("Managed crop phase has no pinned preparation; inspect its original pending journals without replay.")
            : Text(operation.Prepared, "original_intent_sha256");
        return CropTeacherSeedRuntimeRecovery.RecoverAsync(http,
            new(options.BackendUrl, operation.RuntimeDirectory, options.PortfolioJournalDirectory, output,
                options.RunId, options.ExecutorVersion, intentHash), operation.Scopes, operation.ReadStep(), post, write);
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
            ["prepared_sha256"] = HashFile(RecordPath(operation.Sequence, "prepared")),
            ["original_intent_sha256"] = Text(operation.Prepared!, "original_intent_sha256"),
            ["original_proof_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "support-proof.json")),
            ["after_snapshot_path"] = Path.GetFullPath(afterSnapshotPath), ["after_snapshot_sha256"] = HashFile(afterSnapshotPath),
            ["after_ledger_path"] = Path.GetFullPath(evidence.Path), ["after_ledger_sha256"] = HashFile(evidence.Path),
            ["after_state_hash"] = after.StateHash, ["acceptance_source"] = source,
            ["original_boundary_verified"] = true, ["execution_authorized"] = false,
            ["formal_product_training_authorized"] = false
        };
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
