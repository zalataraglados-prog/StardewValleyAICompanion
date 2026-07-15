using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingLoopQueueReplanFilterTests
{
    [Theory]
    [InlineData("blocked", true, true, false, true, true, true, false, "blocked_continue_after_fresh_after_snapshot")]
    [InlineData("blocked", true, true, false, false, true, false, true, "stale_after_snapshot")]
    [InlineData("applied", true, true, false, true, true, false, false, "continuable_execution")]
    [InlineData("blocked", false, true, false, true, true, false, true, "continue_after_blocked_disabled")]
    [InlineData("blocked", true, false, false, true, true, false, false, "non_daily_plan_continue_after_blocked")]
    [InlineData("blocked", true, true, true, true, true, false, false, "non_daily_plan_continue_after_blocked")]
    public void DecideAfterExecutionCoversBlockedFreshStaleAndSkipCases(
        string executionStatus,
        bool continueAfterBlocked,
        bool useDailyPlan,
        bool hasExecutorOverride,
        bool afterSnapshotFresh,
        bool canAttemptMoreItems,
        bool shouldReplan,
        bool shouldStop,
        string reason)
    {
        var decision = QueueReplanFilter.DecideAfterExecution(
            executionStatus,
            continueAfterBlocked,
            useDailyPlan,
            hasExecutorOverride,
            afterSnapshotFresh,
            canAttemptMoreItems);

        Assert.Equal(shouldReplan, decision.ShouldReplan);
        Assert.Equal(shouldStop, decision.ShouldStop);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void FilterUnattemptedUsesStableSemanticIdentityInsteadOfQueueItemId()
    {
        var blockedOriginal = QueueItem("queue_item.original.blocked", "executor.collect_machine_output", "64", "15", "(O)388");
        var completedOriginal = QueueItem("queue_item.original.completed", "executor.load_machine_input", "65", "15", "(O)262");
        var regeneratedBlocked = QueueItem("queue_item.regenerated.blocked", "executor.collect_machine_output", "64", "15", "(O)388");
        var regeneratedCompleted = QueueItem("queue_item.regenerated.completed", "executor.load_machine_input", "65", "15", "(O)262");
        var differentValidRemaining = QueueItem("queue_item.regenerated.remaining", "executor.load_machine_input", "66", "15", "(O)262");
        var attempted = new HashSet<string>(StringComparer.Ordinal)
        {
            QueueReplanFilter.SemanticQueueItemKey(blockedOriginal),
            QueueReplanFilter.SemanticQueueItemKey(completedOriginal)
        };

        var filtered = QueueReplanFilter.FilterUnattempted(
            new[] { regeneratedBlocked, regeneratedCompleted, differentValidRemaining },
            attempted);

        var remaining = Assert.Single(filtered);
        Assert.Equal("queue_item.regenerated.remaining", remaining["queue_item_id"]!.GetValue<string>());
        Assert.DoesNotContain(filtered, item => item["queue_item_id"]!.GetValue<string>() == "queue_item.regenerated.blocked");
        Assert.DoesNotContain(filtered, item => item["queue_item_id"]!.GetValue<string>() == "queue_item.regenerated.completed");
    }

    private static JsonObject QueueItem(string queueItemId, string optionId, string targetX, string targetY, string qualifiedItemId)
    {
        return new JsonObject
        {
            ["queue_item_id"] = queueItemId,
            ["option_id"] = optionId,
            ["status"] = "pending",
            ["normalized_command"] = new JsonObject
            {
                ["command_type"] = "compiled_action_steps",
                ["parameters"] = new JsonArray
                {
                    Parameter("target_tile_x", targetX),
                    Parameter("target_tile_y", targetY),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("compiler_context.season", "spring"),
                    Parameter("estimated_minutes", "1")
                },
                ["steps"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["step_type"] = "move_to_tile",
                        ["target"] = "Farm(" + targetX + "," + targetY + ")"
                    }
                }
            }
        };
    }

    private static JsonObject Parameter(string name, string value)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["value"] = value
        };
    }
}
