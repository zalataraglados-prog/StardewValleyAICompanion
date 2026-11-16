using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalWateringCompilation(CropTeacherRunStep Step, string ModelActionPath,
    string QueuePath, string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory)
{
    public bool IsRefillApproach { get; init; }
    public bool UseSelectedRefillResourceEffects { get; init; }
    public bool UseNativeRefillOutcomeAuthority { get; init; }
}

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalWateringAsync(
        Func<Task<CropTeacherOriginalWateringCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the existing crop phase before original watering compilation.");
            var value = await compileOriginal().ConfigureAwait(false);
            if (value.UseSelectedRefillResourceEffects && !value.IsRefillApproach)
                throw new InvalidDataException("Selected refill resource effects require the original refill approach phase.");
            if (value.UseNativeRefillOutcomeAuthority && (!value.IsRefillApproach || !value.UseSelectedRefillResourceEffects || !options.UseNativeRefillOutcomeAuthority))
                throw new InvalidDataException("Native refill authority was not selected by this original owner.");
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "watering_maintenance";
            record["maintenance_execution_protocol"] = "original_loop.v1";
            if (value.IsRefillApproach) record["maintenance_control_kind"] = value.UseNativeRefillOutcomeAuthority
                ? "refill_approach_native" : value.UseSelectedRefillResourceEffects ? "refill_approach_resources" : "refill_approach";
            record["commitment_id"] = value.Step.CommitmentId;
            record["maintenance_step"] = JsonSerializer.SerializeToNode(value.Step, JsonOptions);
            record["original_model_action_path"] = Path.GetFullPath(value.ModelActionPath);
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("crop_actions", value.Step.TotalDay,
                Path.GetFullPath(value.QueuePath), headSnapshotPath, Path.GetFullPath(value.ReceiptPath),
                Path.GetFullPath(value.AfterSnapshotPath), options.RunId, options.ExecutorVersion, "pending-original-candidate");
            record["maintenance_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenOriginalWateringStart(record, requireNativeIdentity: false);
            record["maintenance_transition"] = JsonSerializer.SerializeToNode(transition with { SelectedCandidateId = start.CandidateId }, JsonOptions);
            RequireNewSupportingOutputs(transition, watering: true);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                wateringStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task ExecuteOriginalWateringAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<Task<JsonObject>> executeOriginal) => ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Watering);

    private CropTeacherWateringMaintenance.Preparation OpenOriginalWateringStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = record["maintenance_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original watering transition is missing.");
        var step = record["maintenance_step"]?.Deserialize<CropTeacherRunStep>(JsonOptions)
            ?? throw new InvalidDataException("Original watering step is missing.");
        var batch = step.ActionBatch;
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        var control = record["maintenance_control_kind"]?.GetValue<string>();
        var resources = control == "refill_approach_resources";
        var native = control == "refill_approach_native";
        var refill = control == "refill_approach" || resources || native;
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null || record["route_transition"] is not null ||
            control is not (null or "refill_approach" or "refill_approach_resources" or "refill_approach_native") ||
            native && !options.UseNativeRefillOutcomeAuthority ||
            record["route_execution_protocol"] is not null || record["maintenance_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TransitionKind != "crop_actions" ||
            step.CommitmentId != Text(record, "commitment_id") || !OriginalCropBatchMatches(step, "executor.water_crop") ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original watering changed its owner/run/executor/source or original due batch.");
        var start = !refill
            ? CropTeacherWateringMaintenance.Preparation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
                active, step.CommitmentId, Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath)
            : native
                ? CropTeacherWateringMaintenance.Preparation.OpenNativeObservedRefillApproach(plan.RequestPath,
                    StoredPath(record, "before_snapshot_path"), headLedgerPath, active, step.CommitmentId,
                    Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath)
            : transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor
                ? resources
                    ? CropTeacherWateringMaintenance.Preparation.OpenNativeRefillApproachWithSelectedResourceEffects(plan.RequestPath,
                        StoredPath(record, "before_snapshot_path"), headLedgerPath, active, step.CommitmentId,
                        Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath)
                    : CropTeacherWateringMaintenance.Preparation.OpenNativeRefillApproach(plan.RequestPath,
                        StoredPath(record, "before_snapshot_path"), headLedgerPath, active, step.CommitmentId,
                        Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath)
            : resources
                ? CropTeacherWateringMaintenance.Preparation.OpenRefillApproachWithSelectedResourceEffects(plan.RequestPath,
                    StoredPath(record, "before_snapshot_path"), headLedgerPath, active, step.CommitmentId,
                    Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath)
                : CropTeacherWateringMaintenance.Preparation.OpenRefillApproach(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
                    active, step.CommitmentId, Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || start.TotalDay != step.TotalDay || transition.TotalDay != step.TotalDay ||
            (!refill && start.CandidateId != $"crop:{step.TotalDay}:{step.CommitmentId}:{batch!.BatchIndex}") ||
            !start.Tiles.SequenceEqual(batch!.Tiles.Take(start.Tiles.Count)) ||
            requireNativeIdentity && transition.SelectedCandidateId != start.CandidateId)
            throw new InvalidDataException("Original watering does not continue its accepted source/candidate/day/targets.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original watering native output overlaps its retained inputs.");
        return start;
    }

    private JsonObject CreateOriginalWateringIntent(JsonObject record)
    {
        var intent = new JsonObject
        {
            ["schema_version"] = "crop_teacher_owned_maintenance_execution.v1", ["run_id"] = options.RunId,
            ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
            ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
            ["commitment_id"] = Text(record, "commitment_id"), ["maintenance_step"] = record["maintenance_step"]!.DeepClone(),
            ["original_model_action_path"] = StoredPath(record, "original_model_action_path"),
            ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(),
            ["maintenance_transition"] = record["maintenance_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
            ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false
        };
        // Preserve the exact historical watering intent when no new control phase was admitted.
        if (record["maintenance_control_kind"] is { } control) intent["maintenance_control_kind"] = control.DeepClone();
        return intent;
    }
}
