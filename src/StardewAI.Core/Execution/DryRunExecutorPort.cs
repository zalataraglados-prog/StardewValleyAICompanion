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
            var results = queue.Items
                .Select(item =>
                {
                    var executable = queue.Status == "pending" && item.Status == "pending";
                    return new ExecutionItemResult
                    {
                        QueueItemId = item.QueueItemId,
                        OptionId = item.OptionId,
                        Status = executable ? "dry_run_ready" : "blocked",
                        Reason = executable
                            ? "dry_run_executor_did_not_mutate_game_state"
                            : "queue_or_item_blocked_before_executor"
                    };
                })
                .ToArray();

            return new ExecutionBatchResult
            {
                QueueId = queue.QueueId,
                ExecutorMode = "dry_run",
                StateHash = queue.StateHash,
                Status = results.All(result => result.Status == "dry_run_ready")
                    ? "dry_run_ready"
                    : "blocked",
                Results = results
            };
        }
    }
}
