using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalControlCompilation(string Kind, string ModelPlanPath, string QueuePath,
    string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory);

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalControlAsync(
        Func<Task<CropTeacherOriginalControlCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the existing crop phase before original control compilation.");
            var value = await compileOriginal().ConfigureAwait(false);
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "control_recovery";
            record["control_execution_protocol"] = "original_loop.v1";
            record["control_kind"] = value.Kind;
            record["original_model_plan_path"] = Path.GetFullPath(value.ModelPlanPath);
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("control_recovery", 0,
                Path.GetFullPath(value.QueuePath), headSnapshotPath, Path.GetFullPath(value.ReceiptPath),
                Path.GetFullPath(value.AfterSnapshotPath), options.RunId, options.ExecutorVersion, "pending-original-candidate");
            record["control_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenOriginalControlStart(record, requireNativeIdentity: false);
            record["control_transition"] = JsonSerializer.SerializeToNode(transition with
            { TotalDay = start.TotalDay, SelectedCandidateId = start.CandidateId }, JsonOptions);
            RequireNewSupportingOutputs(transition, watering: false);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                controlStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task ExecuteOriginalControlAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<Task<JsonObject>> executeOriginal) => ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Control);

    public Task RunControlRecoveryAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) => RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.Control);

    private CropTeacherControlRecovery.Preparation OpenOriginalControlStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = record["control_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original control transition is missing.");
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        if (record["operation_kind"]?.GetValue<string>() != "control_recovery" || record["step"] is not null ||
            record["prior_verified_snapshot_path"] is not null || record["route_transition"] is not null ||
            record["maintenance_transition"] is not null || record["harvest_transition"] is not null || record["overnight_transition"] is not null ||
            record["control_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }
                .Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TransitionKind != "control_recovery" ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original control changed its owner/run/executor/source or active coverage.");
        var start = CropTeacherControlRecovery.Preparation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            active, Text(record, "support_step_id"), Text(record, "control_kind"), StoredPath(record, "original_model_plan_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || requireNativeIdentity &&
            (transition.TotalDay != start.TotalDay || transition.SelectedCandidateId != start.CandidateId))
            throw new InvalidDataException("Original control does not continue its accepted source/candidate/day.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original control native output overlaps retained inputs.");
        if (options.NativeSaveBinding is { } binding)
            RequireOutsideNativeSaveSlot(new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath, StoredPath(record, "runtime_directory") }, binding);
        return start;
    }

    private JsonObject CreateOriginalControlIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_owned_control_execution.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["control_kind"] = Text(record, "control_kind"), ["original_model_plan_path"] = StoredPath(record, "original_model_plan_path"),
        ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(),
        ["control_transition"] = record["control_transition"]!.DeepClone(),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
        ["formal_product_training_authorized"] = false
    };

    private static void AddControlOutcomeEvidence(CropTeacherControlRecovery scope, JsonObject document)
    {
        document["native_control_recovery_verified"] = true;
        document["control_kind"] = scope.Kind;
    }
}
