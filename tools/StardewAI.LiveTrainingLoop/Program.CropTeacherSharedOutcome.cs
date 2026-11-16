using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<string> RecordCropTeacherSharedOutcomeAsync(LiveTrainingOptions options, int iteration,
        CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation, CropTeacherDailySelection selection,
        JsonObject execution)
    {
        if (owner.Pending is not null || operation.SharedSourceReturn is null)
            throw new InvalidOperationException("Record joint selection outcomes only after the original source return is accepted.");
        var binding = owner.ReadSharedSourceExecutionSelection(operation);
        var selected = selection.Lease.CandidateAt(binding.SelectedQueueIndex);
        var transition = CropTeacherManagedSeedOwner.OriginalSupportingTransition(operation);
        if (!SameSharedPath(selection.PlanPath, binding.DecisionPlanPath) ||
            !SameSharedPath(selection.RankingPath, binding.DecisionRankingPath) ||
            !SameSharedPath(owner.CurrentSnapshotPath, transition.AfterSnapshotPath) ||
            !SameSharedPath(ReadString(execution, "execution_path"), transition.ExecutionReceiptPath))
            throw new InvalidDataException("Joint selection outcome differs from its accepted original choice/receipt.");
        var inputs = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        foreach (var path in new[] { binding.DecisionSnapshotPath, binding.DecisionPlanPath, binding.DecisionRankingPath,
            binding.EvidencePath, operation.RecordPath, transition.QueuePath, transition.ExecutionReceiptPath,
            transition.AfterSnapshotPath, owner.CurrentLedgerPath }) inputs.Pin(path);
        if (binding.DecisionReference is { } reference) inputs.Pin(reference.Path);
        var receipt = inputs.Read<JsonObject>(transition.ExecutionReceiptPath);
        var record = new JsonObject
        {
            ["schema_version"] = "crop_teacher_selected_decision_outcome.v1",
            ["owner_sequence"] = operation.Sequence, ["run_id"] = transition.RunId,
            ["decision_reference"] = JsonSerializer.SerializeToNode(binding.DecisionReference, JsonOptions),
            ["decision_snapshot_path"] = binding.DecisionSnapshotPath, ["decision_plan_path"] = binding.DecisionPlanPath,
            ["decision_ranking_path"] = binding.DecisionRankingPath,
            ["selected_queue_index"] = selected.QueueIndex, ["selected_candidate_id"] = selected.CandidateId,
            ["selected_option_id"] = selected.OptionId, ["mechanical_candidate_id"] = transition.SelectedCandidateId,
            ["mechanical_refresh_path"] = binding.EvidencePath, ["execution_receipt_path"] = transition.ExecutionReceiptPath,
            ["after_snapshot_path"] = transition.AfterSnapshotPath,
            ["selected_candidate_completed"] = selection.CompletedForeignCandidateIds.Contains(selected.CandidateId),
            ["typed_objective_completed"] = receipt["objective_continuation_completed"]?.DeepClone(),
            ["objective_continuation"] = selection.ForeignObjectiveContinuations.TryGetValue(selected.CandidateId, out var continuation)
                ? continuation.DeepClone() : null,
            ["policy_model_invoked"] = false, ["native_dispatch_performed_by_recorder"] = false,
            ["decision_aggregation_required"] = true, ["new_policy_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false,
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(inputs.Hashes, JsonOptions)
        };
        var output = Path.Combine(options.SnapshotDir, "crop-joint-daily-" + iteration.ToString("D4") + "-outcome.json");
        inputs.Validate();
        await WritePortfolioJournalFileAsync(output, record.ToJsonString(JsonOptions)).ConfigureAwait(false);
        // Do not rewrite the sealed native receipt. This in-memory annotation routes recording only.
        execution["joint_selection_outcome_path"] = output;
        return output;
    }
}
