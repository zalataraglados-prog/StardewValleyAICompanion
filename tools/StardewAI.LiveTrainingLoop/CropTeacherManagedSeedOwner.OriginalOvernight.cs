using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalOvernightCompilation(CropTeacherRunStep Step, string ModelPlanPath,
    string QueuePath, string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory);

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalOvernightAsync(Func<Task<CropTeacherOriginalOvernightCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            var binding = options.NativeSaveBinding ?? throw new InvalidOperationException("Owned overnight requires its original isolated save binding.");
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the existing phase before original overnight compilation.");
            var value = await compileOriginal().ConfigureAwait(false);
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "overnight_control";
            record["overnight_execution_protocol"] = "original_loop.v1";
            record["overnight_step"] = JsonSerializer.SerializeToNode(value.Step, JsonOptions);
            record["original_model_plan_path"] = Path.GetFullPath(value.ModelPlanPath);
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            record["before_save_fingerprint"] = JsonSerializer.SerializeToNode(
                await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(binding.SaveIsolationPath, binding.SaveSlot).ConfigureAwait(false), JsonOptions);
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("native_day_rollover", value.Step.TotalDay,
                Path.GetFullPath(value.QueuePath), headSnapshotPath, Path.GetFullPath(value.ReceiptPath), Path.GetFullPath(value.AfterSnapshotPath),
                options.RunId, options.ExecutorVersion, "pending-original-candidate");
            record["overnight_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenOriginalOvernightStart(record, requireNativeIdentity: false);
            record["overnight_transition"] = JsonSerializer.SerializeToNode(transition with { SelectedCandidateId = start.CandidateId }, JsonOptions);
            RequireNewSupportingOutputs(transition, watering: false);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null, overnightStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task ExecuteOriginalOvernightAsync(CropTeacherManagedSeedOperation operation, HttpClient http, Func<Task<JsonObject>> executeOriginal) =>
        ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Overnight);
    public Task RunOvernightAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit, Func<string, string, Task<JsonObject>> post) =>
        RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.Overnight);

    private CropTeacherOvernight.Preparation OpenOriginalOvernightStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = record["overnight_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original overnight transition is missing.");
        var step = record["overnight_step"]?.Deserialize<CropTeacherRunStep>(JsonOptions)
            ?? throw new InvalidDataException("Original overnight step is missing.");
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        var day = plan.Days.SingleOrDefault(row => row.TotalDay == step.TotalDay);
        if (options.NativeSaveBinding is null || record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            record["route_transition"] is not null || record["maintenance_transition"] is not null || record["harvest_transition"] is not null ||
            record["route_execution_protocol"] is not null || record["maintenance_execution_protocol"] is not null || record["harvest_execution_protocol"] is not null ||
            record["overnight_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TransitionKind != "native_day_rollover" ||
            step.Stage != CropTeacherRunStageKinds.NativeSleep || step.ActionBatch is not null || day is null || (!day.NativeSleepRequired && day.TotalDay != plan.LastTotalDay) ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original overnight changed its owner/run/executor/source or planned day.");
        _ = ReadOriginalBeforeSave(record);
        var start = CropTeacherOvernight.Preparation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            active, Text(record, "support_step_id"), StoredPath(record, "original_model_plan_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || start.TotalDay != step.TotalDay || transition.TotalDay != step.TotalDay ||
            requireNativeIdentity && transition.SelectedCandidateId != start.CandidateId)
            throw new InvalidDataException("Original overnight does not continue its accepted source/candidate/day.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original overnight native output overlaps retained inputs.");
        RequireOutsideNativeSaveSlot(new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath, StoredPath(record, "runtime_directory") }, options.NativeSaveBinding);
        return start;
    }

    private JsonObject CreateOriginalOvernightIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_owned_overnight_execution.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["overnight_step"] = record["overnight_step"]!.DeepClone(), ["original_model_plan_path"] = StoredPath(record, "original_model_plan_path"),
        ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(), ["overnight_transition"] = record["overnight_transition"]!.DeepClone(),
        ["before_save_fingerprint"] = record["before_save_fingerprint"]!.DeepClone(), ["native_save_binding"] = JsonSerializer.SerializeToNode(options.NativeSaveBinding, JsonOptions),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
    };

    private static void AddOvernightOutcomeEvidence(CropTeacherOvernight scope, JsonObject document)
    {
        document["native_overnight_control_verified"] = true;
        document["from_total_day"] = scope.FromTotalDay; document["to_total_day"] = scope.ToTotalDay;
        document["verified_crop_tile_count"] = scope.VerifiedCropTileCount;
        document["unverified_layout_tile_count"] = scope.UnverifiedLayoutTileCount;
    }
}
