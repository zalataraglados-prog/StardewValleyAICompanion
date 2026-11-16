using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private bool VerifyCompletedSharedNativeObjective(CropTeacherManagedSeedOperation operation, CropSeedRuntimeArtifacts artifacts)
    {
        var transition = OriginalSupportingTransition(operation);
        var raw = artifacts.Read<JsonObject>(transition.ExecutionReceiptPath);
        if (raw["selected_candidate_completed"]?.GetValue<bool>() == true ||
            raw["objective_continuation_completed"]?.GetValue<bool>() != true) return false;
        var selection = SharedSourceSelection(SharedExecutionRecord(operation.Record));
        var original = SelectedQueueDecisionLease.CreateFromPlan(artifacts.Read<SmallModelPlanEnvelope>(selection.DecisionPlanPath),
            artifacts.Read<JsonObject>(selection.DecisionRankingPath)).CandidateAt(selection.SelectedQueueIndex);
        var queue = artifacts.Read<JsonObject>(selection.QueuePath);
        return IsCompletedSharedNativeObjective(queue, raw, original);
    }

    private static bool IsCompletedSharedNativeObjective(JsonObject queue, JsonObject raw, SelectedQueueCandidateLock original)
    {
        var objective = original.ObjectiveContinuation;
        var observed = raw["objective_continuation"] as JsonObject;
        // The native reader has already verified every immutable queue item/frame. Reuse the executor's
        // existing completion rule, not a new per-action definition or the broader candidate-success flag.
        if (objective is null || observed is null || observed["kind"]?.GetValue<string>() != objective["kind"]?.GetValue<string>() ||
            observed["option_id"]?.GetValue<string>() != original.OptionId || queue["items"] is not JsonArray items ||
            !items.OfType<JsonObject>().Any(item => QueueReplanFilter.CompletesObjectiveContinuation(item, objective, "applied") &&
                JsonNode.DeepEquals(QueueReplanFilter.ReadObjectiveContinuation(item), observed)))
            throw new InvalidDataException("Completed native work does not finish the original selected typed objective.");
        return true;
    }
}
