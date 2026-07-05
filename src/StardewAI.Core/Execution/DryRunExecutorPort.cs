using System.Linq;
using StardewAI.Contracts;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Execution
{
    public sealed class DryRunExecutorPort : IExecutorPort
    {
        public bool ExecutionEnabled => false;

        public ExecutionBatchResult Execute(ActionQueueEnvelope queue)
        {
            var actorAllowed = queue.Actor.ActorType == "ai_companion" &&
                queue.Actor.ControlSurface == "companion_actor" &&
                !string.IsNullOrWhiteSpace(queue.Actor.ActorId);
            var results = queue.Items
                .Select(item =>
                {
                    var executable = actorAllowed && queue.Status == "pending" && item.Status == "pending";
                    return new ExecutionItemResult
                    {
                        QueueItemId = item.QueueItemId,
                        OptionId = item.OptionId,
                        Actor = queue.Actor,
                        Status = executable ? "dry_run_ready" : "blocked",
                        Reason = executable
                            ? "dry_run_executor_did_not_mutate_game_state"
                            : actorAllowed ? "queue_or_item_blocked_before_executor" : "executor_rejected_non_companion_actor"
                    };
                })
                .ToArray();

            return new ExecutionBatchResult
            {
                QueueId = queue.QueueId,
                ExecutorMode = "dry_run",
                StateHash = queue.StateHash,
                Actor = queue.Actor,
                Status = results.All(result => result.Status == "dry_run_ready")
                    ? "dry_run_ready"
                    : "blocked",
                Results = results
            };
        }
    }
}
