using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalWateringCompilation(CropTeacherRunStep Step, string ModelActionPath,
    string QueuePath, string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory);

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
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "watering_maintenance";
            record["maintenance_execution_protocol"] = "original_loop.v1";
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
        Func<Task<JsonObject>> executeOriginal) => ExecuteOriginalSupportingAsync(operation, http, executeOriginal, watering: true);

    private CropTeacherWateringMaintenance.Preparation OpenOriginalWateringStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = record["maintenance_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original watering transition is missing.");
        var step = record["maintenance_step"]?.Deserialize<CropTeacherRunStep>(JsonOptions)
            ?? throw new InvalidDataException("Original watering step is missing.");
        var batch = step.ActionBatch;
        var original = plan.Days.SingleOrDefault(day => day.TotalDay == step.TotalDay)?.ActionBatches
            .SingleOrDefault(row => row.BatchIndex == batch?.BatchIndex);
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null || record["route_transition"] is not null ||
            record["route_execution_protocol"] is not null || record["maintenance_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TransitionKind != "crop_actions" ||
            step.Stage != CropTeacherRunStageKinds.ExecuteCropBatch || batch is null || batch.OptionId != "executor.water_crop" ||
            step.CommitmentId != Text(record, "commitment_id") || batch.CommitmentId != step.CommitmentId ||
            !plan.CoveredCommitmentIds.Contains(step.CommitmentId, StringComparer.Ordinal) || original is null ||
            !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(batch with { Tiles = original.Tiles }, JsonOptions), JsonSerializer.SerializeToNode(original, JsonOptions)) ||
            batch.Tiles.Length is < 1 or > 8 || !batch.Tiles.SequenceEqual(original.Tiles.Where(tile => batch.Tiles.Contains(tile))) ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original watering changed its owner/run/executor/source or original due batch.");
        var start = CropTeacherWateringMaintenance.Preparation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            active, step.CommitmentId, Text(record, "support_step_id"), StoredPath(record, "original_model_action_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || start.TotalDay != step.TotalDay || transition.TotalDay != step.TotalDay ||
            start.CandidateId != $"crop:{step.TotalDay}:{step.CommitmentId}:{batch.BatchIndex}" ||
            !start.Tiles.SequenceEqual(batch.Tiles.Take(start.Tiles.Count)) ||
            requireNativeIdentity && transition.SelectedCandidateId != start.CandidateId)
            throw new InvalidDataException("Original watering does not continue its accepted source/candidate/day/targets.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original watering native output overlaps its retained inputs.");
        return start;
    }

    private JsonObject CreateOriginalWateringIntent(JsonObject record) => new()
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
}
