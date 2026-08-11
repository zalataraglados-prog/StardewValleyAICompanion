using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DailyQuestAcceptanceMainlineTests
{
    [Fact]
    public void DailyQuestUsesRollingApproachThenNativeBoardAcceptance()
    {
        var approachSnapshot = Snapshot(StateJson(playerX: 40, canAccept: true));
        var approach = Evaluate(approachSnapshot);
        var approachCandidate = Assert.Single(approach.Options.Single().EventCandidates);
        Assert.True(approachCandidate.Available, string.Join(";", approachCandidate.BlockReasons));
        Assert.Equal("daily_quest_board_approach", approachCandidate.Kind);
        var approachPlan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), approach),
            approachSnapshot.StateHash);
        Assert.Equal(new[] { "move_to_tile" }, approachPlan.Steps.Select(step => step.Kind));

        var terminalSnapshot = Snapshot(StateJson(playerX: 42, canAccept: true));
        var terminal = Evaluate(terminalSnapshot);
        var terminalCandidate = Assert.Single(terminal.Options.Single().EventCandidates);
        Assert.True(terminalCandidate.Available, string.Join(";", terminalCandidate.BlockReasons));
        Assert.Equal("accept_daily_quest", terminalCandidate.Kind);

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), terminal),
            terminalSnapshot.StateHash);
        Assert.Equal(new[] { "interact", "accept_daily_quest" }, plan.Steps.Select(step => step.Kind));

        var queue = new ActionQueueCompiler().Compile(plan, terminalSnapshot);
        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.Select(item =>
                item.OptionId + ":missing=" + string.Join(",", item.MissingStateFactors) +
                ":blocked=" + string.Join(",", item.BlockingReasons))));
        var nativeAccept = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.accept_daily_quest"));
        Assert.Empty(nativeAccept.BlockingReasons);
        Assert.Equal("accept_daily_quest", Assert.Single(nativeAccept.NormalizedCommand.Steps).StepType);
        Assert.Contains(nativeAccept.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_offer_fingerprint" && parameter.Value == "fixture-fingerprint");
    }

    [Fact]
    public void AlreadyAcceptedDailyQuestIsExcludedUpstream()
    {
        var snapshot = Snapshot(StateJson(playerX: 42, canAccept: false));
        var candidate = Assert.Single(Evaluate(snapshot).Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("daily_quest_already_accepted_today", candidate.BlockReasons);
        Assert.Contains("daily_quest_native_can_accept_false", candidate.BlockReasons);
    }

    [Fact]
    public void BridgeScansLiveBoardAndRuntimeOnlyUsesNativeAcceptClick()
    {
        var bridge = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.DailyQuest.cs"));
        Assert.Contains("Game1.questOfTheDay", bridge, StringComparison.Ordinal);
        Assert.Contains("Game1.CanAcceptDailyQuest()", bridge, StringComparison.Ordinal);
        Assert.Contains("doesTileHaveProperty", bridge, StringComparison.Ordinal);
        Assert.Contains("\"Billboard\"", bridge, StringComparison.Ordinal);
        Assert.Contains("\"3\"", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("42, 56", bridge, StringComparison.Ordinal);

        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.DailyQuestAcceptance.cs"));
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("Game1.CanAcceptDailyQuest()", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("quest.accepted.Value = true", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("quest.daysLeft.Value = 2", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("questLog.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptedDailyQuest.Value =", runtime, StringComparison.Ordinal);
    }

    private static StardewAI.Contracts.Options.OptionAvailabilityEnvelope Evaluate(
        SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "quest.accept_daily" },
            includeExecutorCalibrationOptions: true);

    private static string StateJson(int playerX, bool canAccept) => $$"""
    {
      "player": {
        "location_id":{"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_x":{"value":{{playerX}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_y":{"value":57,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
        ,"facing_direction":{"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "current_location": {
        "route_context":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "locations": {
        "route_graph":{"value":{"edges":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_connectors":{"value":{"location_id":"Town","connectors":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "collision_grid":{"value":{"location_id":"Town","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_action_branch_coverage":{"value":{"rows":[{"tile_x":42,"tile_y":56,"branch":"Billboard","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "quests": {
        "daily_quest_offer":{"value":{"available":true,"can_accept":{{canAccept.ToString().ToLowerInvariant()}},"accepted_daily_quest":{{(!canAccept).ToString().ToLowerInvariant()}},"offer_fingerprint":"fixture-fingerprint","quest":{"id":"fixture-daily","title":"Help Wanted","description":"Bring wood.","current_objective":"Bring wood.","runtime_type":"ItemDeliveryQuest"},"board_location_id":"Town","board_action_tile_x":42,"board_action_tile_y":56,"board_action_raw":"Billboard 3","stand_tile_x":42,"stand_tile_y":57,"menu_clear":true,"status":"{{(canAccept ? "ready" : "blocked")}}","blocked_diagnostics":[{{(canAccept ? string.Empty : "\"daily_quest_already_accepted_today\"")}}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "menus": {
        "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      }
    }
    """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-11T00:00:00Z",
            Completeness = "complete",
            State = state
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

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
