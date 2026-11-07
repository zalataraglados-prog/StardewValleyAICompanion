using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class QueueExecutionReceiptValidator
{
    // Current Loop receipts distinguish applied progress from candidate success.
    // Only a consumer proving its own terminal objective may opt into this form.
    private static bool IsAppliedVerifiedContinuationProgress(
        QueueExecutionReceiptEnvelope receipt,
        ContinuationState? continuation)
    {
        return receipt.Status == "applied" &&
            !receipt.Success &&
            !receipt.SelectedCandidateCompleted &&
            receipt.AfterSnapshotFresh &&
            continuation is
            {
                QueueProgressVerified: true,
                PrimitiveVerificationStatus: "verified",
                RetryNextIteration: true,
                QueueReplanStopReason:
                    "selected_queue_continuation_retry_next_iteration",
                HasActiveObjectiveContinuation: true,
                DecisionComplete: false,
                ResumeIndex: >= 0
            } &&
            ((!continuation.ObjectiveCompleted &&
              continuation.CompletedObjectiveCount == 0) ||
             (continuation.ObjectiveCompleted &&
              continuation.CompletedObjectiveCount == 1 &&
              continuation.CompletedContinuationMatchesActive));
    }
}
