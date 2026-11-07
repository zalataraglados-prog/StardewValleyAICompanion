using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropTeacherSeedPlantingReceipt
{
    internal static int Validate(ActionQueueEnvelope queue, SnapshotEnvelope before, QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after, MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        if (receipt.Success)
        {
            var reasons = QueueExecutionReceiptValidator.Validate(queue, before, receipt, after, request.RunId,
                request.ExecutorVersion, request.SelectedCandidateId, before.StateHash, requireTeacherPreferenceStateRebound: false);
            Require(reasons.Length == 0, "Owned planting native receipt rejected: " + string.Join(",", reasons));
            return queue.Items.Length;
        }
        var steps = receipt.StepResults ?? Array.Empty<QueueExecutionStepReceipt>();
        var consumed = steps.TakeWhile(step => step.Status == "applied" && step.PrimitiveVerificationStatus == "verified").Count();
        Require(consumed > 0 && consumed < queue.Items.Length && steps.Length <= consumed + 1 && steps.Length <= queue.Items.Length &&
                receipt.SchemaVersion == "queue_execution_receipt.v1" && receipt.QueueExecutionMode == "sequential_queue_items" &&
                receipt.RunId == request.RunId && !string.IsNullOrWhiteSpace(request.RunId) &&
                PolicyTrajectoryVersionPins.IsKnownExecutor(request.ExecutorVersion) && receipt.QueueId == queue.QueueId &&
                receipt.SelectedCandidateId == request.SelectedCandidateId && !string.IsNullOrWhiteSpace(request.SelectedCandidateId) &&
                queue.SchemaVersion == "action_queue.v1" && queue.StateHash == before.StateHash &&
                receipt.SourceStateHash == before.StateHash && receipt.AfterStateHash == after.StateHash &&
                receipt.BeforeGameTick == before.GameTick && receipt.AfterGameTick == after.GameTick && after.GameTick > before.GameTick &&
                receipt.AfterSnapshotFresh && !receipt.SelectedCandidateCompleted &&
                receipt.PlannedItemCount == queue.Items.Length && receipt.ExecutedItemCount == steps.Length &&
                receipt.FinalPendingItemCount == queue.Items.Length - steps.Length &&
                receipt.MaxQueueItemAttempts >= steps.Length && receipt.MaxQueueItemAttempts <= TeacherEvidenceRolloutLimits.MaxQueueItems &&
                receipt.Status == (steps.Length == consumed ? "applied" : "blocked"),
            "Owned planting incomplete receipt is not an exact applied prefix.");
        var expectedHash = before.StateHash;
        var expectedTick = before.GameTick;
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            ValidateStep(queue, step, index, expectedHash, expectedTick, index < consumed, completed: false);
            expectedHash = step.AfterStateHash;
            expectedTick = step.AfterGameTick;
        }
        Require(expectedHash == after.StateHash && expectedTick == after.GameTick &&
                receipt.BlockReasons.ToHashSet(StringComparer.Ordinal).SetEquals(steps.SelectMany(step => step.BlockReasons)),
            "Owned planting prefix terminal boundary/block reasons differ.");
        return consumed;
    }

    internal static void ValidateStep(ActionQueueEnvelope queue, QueueExecutionStepReceipt step, int index,
        string expectedHash, long expectedTick, bool successful, bool completed)
    {
        var item = queue.Items[index];
        Require(step.QueueItemIndex == index && step.QueueItemCount == queue.Items.Length &&
                step.OriginalPlannedItemCount == queue.Items.Length && step.QueueId == queue.QueueId &&
                step.QueueItemId == item.QueueItemId && step.OptionId == item.OptionId && step.PrimitiveKind == "plant_seed" &&
                step.CompiledCommandStateHash == queue.StateHash && !step.TeacherPreferenceStateRebound &&
                step.SelectedQueueCandidateCompleted == completed && step.SourceStateHash == expectedHash && step.BeforeGameTick == expectedTick &&
                step.AfterGameTick >= expectedTick && !string.IsNullOrWhiteSpace(step.AfterStateHash) && step.AfterSnapshotFresh &&
                step.StateHashChanged == (step.SourceStateHash != step.AfterStateHash) &&
                step.EffectiveQueueItem is { } effective &&
                ExecutionReceiptValidationSupport.EffectiveQueueItemMatches(effective, item, queue.StateHash),
            "Owned planting prefix step identity/order/source differs from its bound queue.");
        Require(successful
                ? step.Status == "applied" && step.PrimitiveVerificationStatus == "verified" && step.StateHashChanged &&
                  step.PrimitiveVerificationReasons.Length > 0 && step.BlockReasons.Length == 0 &&
                  string.IsNullOrWhiteSpace(step.FailureAttribution) && step.ChangedFacts.ValueKind == JsonValueKind.Array && step.ChangedFacts.GetArrayLength() > 0
                : step.Status == "blocked" && step.PrimitiveVerificationStatus != "verified" && step.BlockReasons.Length > 0,
            "Owned planting prefix has an unverified effect or unsupported failed suffix.");
    }
}
