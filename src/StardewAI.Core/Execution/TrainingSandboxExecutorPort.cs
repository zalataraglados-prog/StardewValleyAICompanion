using System;
using System.Linq;
using StardewAI.Contracts;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Execution
{
    public sealed class TrainingSandboxExecutorPort : IExecutorPort
    {
        public bool ExecutionEnabled => true;

        public ExecutionBatchResult Execute(ActionQueueEnvelope queue)
        {
            var targetAllowed = queue.ExecutionMode == "training_singleplayer" &&
                queue.Actor.ActorType == "training_farmer" &&
                queue.Actor.ControlSurface == "training_sandbox" &&
                !string.IsNullOrWhiteSpace(queue.Actor.ActorId);
            var queueReady = targetAllowed && queue.Status == "pending";
            var results = queue.Items
                .Select(item =>
                {
                    var ready = queueReady && item.Status == "pending";
                    return new ExecutionItemResult
                    {
                        QueueItemId = item.QueueItemId,
                        OptionId = item.OptionId,
                        Actor = queue.Actor,
                        Status = ready ? "sandbox_applied" : "blocked",
                        FeedbackKey = "option:" + item.OptionId,
                        Reason = ready
                            ? "training_sandbox_applied_normalized_command"
                            : targetAllowed ? "queue_or_item_blocked_before_training_sandbox" : "training_sandbox_rejected_execution_target"
                    };
                })
                .ToArray();
            var completed = results
                .Where(result => result.Status == "sandbox_applied")
                .Select(result => result.OptionId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new ExecutionBatchResult
            {
                QueueId = queue.QueueId,
                ExecutorMode = "training_sandbox",
                StateHash = queue.StateHash,
                AfterStateHash = completed.Length == 0 ? string.Empty : SyntheticAfterHash(queue),
                Actor = queue.Actor,
                Status = completed.Length == queue.Items.Length && completed.Length > 0 ? "applied" : "blocked",
                FeedbackAvailable = completed.Length > 0,
                CompletedOptionIds = completed,
                Results = results
            };
        }

        private static string SyntheticAfterHash(ActionQueueEnvelope queue)
        {
            return "training_after." + queue.QueueId + "." + Math.Abs(string.Join("|", queue.Items.Select(item => item.OptionId)).GetHashCode()).ToString("X");
        }
    }
}
