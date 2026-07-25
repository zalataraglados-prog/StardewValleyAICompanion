using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingLoopQueueReplanFilterTests
{
    [Fact]
    public void MainLoopExecutesPendingSubsetOnlyWhenContinueFlagIsEnabled()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.cs"));

        Assert.Contains("options.ContinueAfterBlockedQueueItems", source, StringComparison.Ordinal);
        Assert.Contains("ExecutableQueueItems(queue).Length", source, StringComparison.Ordinal);
        Assert.Contains("executing_pending_subset", source, StringComparison.Ordinal);
        Assert.Contains("executableSubsetCount == 0", source, StringComparison.Ordinal);
    }

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

    [Fact]
    public void FilterUnattemptedIgnoresRecomputedBudgetMetadata()
    {
        var original = QueueItem("queue.original", "executor.clear_obstacle", "62", "21", string.Empty);
        original["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("budget.remaining_minutes_before", "859"));
        var regenerated = QueueItem("queue.regenerated", "executor.clear_obstacle", "62", "21", string.Empty);
        regenerated["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("budget.remaining_minutes_before", "857"));
        var attempted = new HashSet<string>(StringComparer.Ordinal)
        {
            QueueReplanFilter.SemanticQueueItemKey(original)
        };

        var filtered = QueueReplanFilter.FilterUnattempted(new[] { regenerated }, attempted);

        Assert.Empty(filtered);
    }

    [Fact]
    public void ContinuationFilterKeepsSameNpcAndRejectsObjectiveSwitch()
    {
        var continuation = new JsonObject
        {
            ["option_id"] = "social.talk_npc",
            ["npc_name"] = "Abigail",
            ["target_location"] = "SeedShop",
            ["slot_index"] = string.Empty,
            ["qualified_item_id"] = string.Empty
        };
        var candidates = new JsonArray
        {
            SocialCandidate("social.talk_npc", "Leah", false),
            SocialCandidate("social.talk_npc", "Abigail", true),
            SocialCandidate("social.gift_npc", "Abigail", false)
        };

        var filtered = QueueReplanFilter.FilterRankedCandidates(candidates, continuation);

        var selectedNode = Assert.Single(filtered);
        Assert.NotNull(selectedNode);
        var selected = selectedNode!.AsObject();
        var parameters = Assert.IsType<JsonArray>(selected["parameters"]);
        var parameter = Assert.IsType<JsonObject>(Assert.Single(parameters));
        Assert.Equal("Abigail", parameter["value"]!.GetValue<string>());
    }

    [Fact]
    public void AppliedWaitCarriesContinuationAndAppliedMatchingInteractionCompletesIt()
    {
        var wait = QueueItem("queue.wait", "executor.wait_ticks", "0", "0", string.Empty);
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.option_id", "social.talk_npc"));
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.npc_name", "Abigail"));
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.target_location", "SeedShop"));
        var continuation = QueueReplanFilter.ReadSocialContinuation(wait);

        Assert.NotNull(continuation);
        Assert.Equal("Abigail", continuation!["npc_name"]!.GetValue<string>());

        var interact = QueueItem("queue.social", "executor.social_interact", "10", "10", string.Empty);
        interact["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("npc_name", "Abigail"));
        interact["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("social_action_kind", "talk"));
        Assert.True(QueueReplanFilter.CompletesSocialContinuation(interact, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesSocialContinuation(interact, continuation, "blocked"));
    }

    [Fact]
    public void MachineContinuationKeepsSameExecutorLocationAndTileUntilApplied()
    {
        var route = QueueItem("queue.machine.route", "executor.traverse_connector", "4", "8", string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.option_id", "executor.collect_machine_output"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.machine_location_id", "Cellar"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.machine_tile_x", "5"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.machine_tile_y", "6"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("machine", continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            MachineCandidate("collect_machine_output_tile", "Cellar", 5, 7),
            MachineCandidate("collect_machine_output_tile", "Cellar", 5, 6),
            MachineCandidate("load_machine_input_tile", "Cellar", 5, 6)
        };
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(candidates, continuation));
        Assert.Equal(6, selected!["tile_y"]!.GetValue<int>());

        var collect = QueueItem("queue.machine.collect", "executor.collect_machine_output", "5", "6", string.Empty);
        collect["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("machine_location_id", "Cellar"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(collect, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(collect, continuation, "blocked"));
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

    private static JsonObject MachineCandidate(string kind, string locationId, int tileX, int tileY)
    {
        return new JsonObject
        {
            ["option_id"] = "farm.process_machines",
            ["kind"] = kind,
            ["location_id"] = locationId,
            ["tile_x"] = tileX,
            ["tile_y"] = tileY,
            ["parameters"] = new JsonArray()
        };
    }

    private static JsonObject SocialCandidate(string optionId, string npcName, bool continuationParameters)
    {
        return new JsonObject
        {
            ["candidate_id"] = "social:" + optionId + ":" + npcName,
            ["option_id"] = optionId,
            ["parameters"] = new JsonArray
            {
                Parameter(continuationParameters ? "continuation.npc_name" : "npc_name", npcName)
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

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Cannot find repository root.");
        }

        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
