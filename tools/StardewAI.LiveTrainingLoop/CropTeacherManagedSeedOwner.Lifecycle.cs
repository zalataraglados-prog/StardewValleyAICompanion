using System.Text;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> AdmitLifecycleAsync(string commitmentId, string mode, string reason,
        string beforeSnapshotPath, string ledgerPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var record = CreateAdmissionRecord(beforeSnapshotPath, ledgerPath, runtimeDirectory);
            record["operation_kind"] = "lifecycle";
            record["commitment_id"] = commitmentId;
            record["mode"] = mode;
            record["cancellation_reason"] = reason;
            var scope = OpenLifecycle(record);
            record["original_intent_sha256"] = LifecycleIntentHash(record, scope);
            var operation = new CropTeacherManagedSeedOperation(this, record, RecordPath(Sequence(record), "intent"), null, scope);
            return await PersistAdmissionAsync(record, operation).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task RunLifecycleAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (!operation.IsLifecycle) throw new InvalidOperationException("Managed crop operation is not a lifecycle closure.");
            if (operation.Started is not null)
                throw new InvalidOperationException("Managed lifecycle invocation is already claimed; recover original evidence instead of repeating it.");
            var started = new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_lifecycle_start.v1", ["sequence"] = operation.Sequence,
                ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
                ["admission_sha256"] = HashFile(operation.RecordPath),
                ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"),
                ["runtime_invocation_claimed"] = true, ["formal_product_training_authorized"] = false
            };
            var startedPath = RecordPath(operation.Sequence, "started");
            await SaveAsync(startedPath, started).ConfigureAwait(false);
            operation.Started = started;
            TrackFrontier(startedPath);
            var boundary = await CropTeacherSeedLifecycleRuntime.RunManagedAsync(http, LifecycleOptions(operation.Record), operation.Lifecycle!,
                commit, post, write).ConfigureAwait(false);
            var artifacts = boundary.Artifacts;
            if (artifacts.Hashes[Path.Combine(operation.RuntimeDirectory, "lifecycle-intent.json")] != Text(operation.Record, "original_intent_sha256"))
                throw new InvalidDataException("Managed lifecycle differs from its pinned original intent.");
            if (boundary.ReleaseRequest is not null)
                _ = await CropTeacherSeedRuntimeRecovery.ReadReplyAsync(options, options.BackendUrl,
                    operation.RuntimeDirectory, options.EvidenceDirectory, artifacts, "release", "commit",
                    boundary.ReleaseRequest, post, write).ConfigureAwait(false);
            void Validate() { ValidateLifecycle(operation); artifacts.Validate(); }
            await AdvanceAsync(operation, boundary.Evidence, operation.Lifecycle!.SnapshotPath, Validate, "live_lifecycle").ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private CropTeacherSeedLifecycleSupport OpenLifecycle(JsonObject record)
    {
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            !plan.CoveredCommitmentIds.Contains(Text(record, "commitment_id"), StringComparer.Ordinal))
            throw new InvalidDataException("Managed lifecycle does not continue its original covered ledger boundary.");
        _ = LifecycleOptions(record);
        var scope = CropTeacherSeedLifecycleSupport.Open(StoredPath(record, "before_snapshot_path"), headLedgerPath,
            Text(record, "commitment_id"), Text(record, "support_step_id"));
        var expectedSource = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (scope.InputHashes[StoredPath(record, "before_snapshot_path")] != expectedSource)
            throw new InvalidDataException("Managed lifecycle source has no accepted native continuity.");
        return scope;
    }

    private CropTeacherSeedLifecycleRuntimeOptions LifecycleOptions(JsonObject record)
    {
        var value = new CropTeacherSeedLifecycleRuntimeOptions(options.BackendUrl, StoredPath(record, "runtime_directory"), options.RunId,
            Text(record, "mode"), Text(record, "cancellation_reason"));
        CropTeacherSeedLifecycleRuntime.ValidateOptions(value);
        return value;
    }

    private string LifecycleIntentHash(JsonObject record, CropTeacherSeedLifecycleSupport scope)
    {
        var value = LifecycleOptions(record);
        var cancel = value.Mode == CropTeacherSeedLifecycleRuntime.ExplicitCancellation ? scope.BeginCancellation(value.CancellationReason) : null;
        return PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(
            CropTeacherSeedLifecycleRuntime.CreateIntent(value, scope, cancel, scope.BaseLedger).ToJsonString(JsonOptions)));
    }

    private void ValidateLifecycle(CropTeacherManagedSeedOperation operation)
    {
        operation.Lifecycle!.ValidateInputsUnchanged();
        if (LifecycleIntentHash(operation.Record, operation.Lifecycle) != Text(operation.Record, "original_intent_sha256"))
            throw new InvalidDataException("Managed lifecycle changed its admitted intent.");
    }
}
