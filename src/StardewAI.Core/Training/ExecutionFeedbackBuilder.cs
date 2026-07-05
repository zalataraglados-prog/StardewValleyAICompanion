using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class ExecutionFeedbackBuilder
    {
        public TrainingFeedback Build(ExecutionBatchResult result)
        {
            return new TrainingFeedback
            {
                FeedbackMode = result.ExecutorMode == "training_sandbox"
                    ? "training_sandbox_state_delta"
                    : "observed_state_delta",
                ExecutorRequired = true,
                AvailableNow = result.FeedbackAvailable,
                Source = result.ExecutorMode,
                ObservedDelta = new ObservedStateDelta
                {
                    BeforeStateHash = result.StateHash,
                    AfterStateHash = result.AfterStateHash,
                    CompletedDirectionIds = result.CompletedOptionIds
                }
            };
        }
    }
}
