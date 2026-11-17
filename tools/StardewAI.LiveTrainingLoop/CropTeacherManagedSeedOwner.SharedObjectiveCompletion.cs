using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private bool VerifyCompletedSharedNativeObjective(
        CropTeacherManagedSeedOperation operation,
        CropTeacherSharedSourceReturn shared,
        CropSeedRuntimeArtifacts artifacts)
    {
        var transition = OriginalSupportingTransition(operation);
        var raw = artifacts.Read<JsonObject>(transition.ExecutionReceiptPath);
        var selectedCompleted = raw["selected_candidate_completed"]?.GetValue<bool>() == true;
        var objectiveCompleted = raw["objective_continuation_completed"]?.GetValue<bool>() == true;
        if (!shared.HasMechanicalContinuation
                ? selectedCompleted || !objectiveCompleted
                : !selectedCompleted && !objectiveCompleted)
            return false;
        var selection = SharedSourceSelection(SharedExecutionRecord(operation.Record));
        var original = SelectedQueueDecisionLease.CreateFromPlan(artifacts.Read<SmallModelPlanEnvelope>(selection.DecisionPlanPath),
            artifacts.Read<JsonObject>(selection.DecisionRankingPath)).CandidateAt(selection.SelectedQueueIndex);
        var queue = artifacts.Read<JsonObject>(selection.QueuePath);
        return VerifySharedNativeObjectiveCompletion(
            queue,
            raw,
            original,
            shared.VerifiedNativeWorkItems,
            shared.HasMechanicalContinuation);
    }

    private static bool VerifySharedNativeObjectiveCompletion(
        JsonObject initialQueue,
        JsonObject raw,
        SelectedQueueCandidateLock original,
        IReadOnlyList<ActionQueueItem> verifiedItems,
        bool hasMechanicalContinuation)
    {
        var selectedCompleted = raw["selected_candidate_completed"]?.GetValue<bool>() == true;
        var objectiveCompleted = raw["objective_continuation_completed"]?.GetValue<bool>() == true;
        if (!hasMechanicalContinuation)
        {
            if (selectedCompleted || !objectiveCompleted) return false;
            return IsCompletedSharedNativeObjective(initialQueue, raw, original);
        }

        if (!selectedCompleted && !objectiveCompleted) return false;
        if (!objectiveCompleted)
            throw new InvalidDataException("Completed native work does not finish the original selected typed objective.");
        var verifiedQueue = new JsonObject
        {
            ["items"] = JsonSerializer.SerializeToNode(verifiedItems)
        };
        return IsCompletedSharedNativeObjective(verifiedQueue, raw, original);
    }

    private static bool IsCompletedSharedNativeObjective(JsonObject queue, JsonObject raw, SelectedQueueCandidateLock original)
    {
        var objective = original.ObjectiveContinuation;
        var observed = JsonNode.Parse(NativeObjectiveContinuationJournal.ReadRecordedObjective(
            JsonSerializer.SerializeToElement(raw)).GetRawText())!.AsObject();
        // The native reader has already verified the supplied executed items/frames. Reuse the executor's
        // existing completion rule, not a new per-action definition or the broader candidate-success flag.
        if (objective is null || observed is null || observed["kind"]?.GetValue<string>() != objective["kind"]?.GetValue<string>() ||
            observed["option_id"]?.GetValue<string>() != original.OptionId || queue["items"] is not JsonArray items ||
            !items.OfType<JsonObject>().Any(item => QueueReplanFilter.CompletesObjectiveContinuation(item, objective, "applied") &&
                JsonNode.DeepEquals(QueueReplanFilter.ReadObjectiveContinuation(item), observed)))
            throw new InvalidDataException("Completed native work does not finish the original selected typed objective.");
        return true;
    }
}
