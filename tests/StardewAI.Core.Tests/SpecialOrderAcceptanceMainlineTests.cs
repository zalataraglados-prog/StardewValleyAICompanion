using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class SpecialOrderAcceptanceMainlineTests
{
    [Fact]
    public void TownBoardUsesRollingApproachOpenAndExactOfferAcceptance()
    {
        var approach = Snapshot(StateJson(playerX: 58, menuOpen: false, accepted: false));
        AssertStage(approach, "special_order_board_approach", "move_to_tile");

        var open = Snapshot(StateJson(playerX: 61, menuOpen: false, accepted: false));
        AssertStage(open, "special_order_board_open", "interact");

        var terminal = Snapshot(StateJson(playerX: 61, menuOpen: true, accepted: false));
        var availability = Evaluate(terminal);
        var candidates = availability.Options.Single().EventCandidates;
        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, candidate => Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons)));
        var selected = candidates.Single(candidate => candidate.DisplayName == "Robin's Project");
        var prediction = new PolicyEventCandidatePrediction
        {
            OptionId = "quest.accept_special_order",
            CandidateId = selected.CandidateId,
            Kind = selected.Kind,
            Available = true,
            LocationId = selected.LocationId,
            DisplayName = selected.DisplayName,
            Parameters = selected.Parameters
        };
        var plan = new DailyPlanCompiler().Compile(new[] { prediction }, terminal.StateHash);
        Assert.Equal(new[] { "accept_special_order" }, plan.Steps.Select(step => step.Kind));
        var queue = new ActionQueueCompiler().Compile(plan, terminal);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.accept_special_order", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("accept_special_order", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_offer_fingerprint" && parameter.Value == "right-fingerprint");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "special_order_generation_seed" && parameter.Value == "222");
    }

    [Fact]
    public void AcceptedBoardTypeIsExcludedUpstream()
    {
        var snapshot = Snapshot(StateJson(playerX: 61, menuOpen: false, accepted: true));
        var candidate = Assert.Single(Evaluate(snapshot).Options.Single().EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("special_order_type_already_accepted_this_cycle", candidate.BlockReasons);
    }

    [Fact]
    public void BridgeCoversAllNativeBoardTokensAndRuntimeOnlyClicksNativeButton()
    {
        var bridge = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.SpecialOrderBoards.cs"));
        Assert.Contains("\"SpecialOrders\"", bridge, StringComparison.Ordinal);
        Assert.Contains("\"QiChallengeBoard\"", bridge, StringComparison.Ordinal);
        Assert.Contains("\"DesertMarlon\"", bridge, StringComparison.Ordinal);
        Assert.Contains("availableSpecialOrders", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSpecialOrder", bridge, StringComparison.Ordinal);

        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SpecialOrderAcceptance.cs"));
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain(".AddSpecialOrder(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptedSpecialOrderTypes.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("specialOrders.Add", runtime, StringComparison.Ordinal);
    }

    private static void AssertStage(SnapshotEnvelope snapshot, string expectedCandidateKind, string expectedStepKind)
    {
        var availability = Evaluate(snapshot);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(expectedCandidateKind, candidate.Kind);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        Assert.Equal(new[] { expectedStepKind }, plan.Steps.Select(step => step.Kind));
    }

    private static StardewAI.Contracts.Options.OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "quest.accept_special_order" },
            includeExecutorCalibrationOptions: true);

    private static string StateJson(int playerX, bool menuOpen, bool accepted) => $$$"""
    {
      "player": {
        "location_id":{"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_x":{"value":{{{playerX}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_y":{"value":94,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "facing_direction":{"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "current_location": {"route_context":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
      "locations": {
        "route_graph":{"value":{"edges":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_connectors":{"value":{"location_id":"Town","connectors":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "collision_grid":{"value":{"location_id":"Town","width":140,"height":120,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "route_action_branch_coverage":{"value":{"rows":[{"tile_x":61,"tile_y":93,"branch":"SpecialOrders","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "quests": {
        "special_orders":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "accepted_special_order_types":{"value":{{{(accepted ? "[\"\"]" : "[]")}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "special_order_boards":{"value":[{
          "board_type":"","location_id":"Town","action_token":"SpecialOrders","action_raw":"SpecialOrders",
          "action_tile_x":61,"action_tile_y":93,"stand_tile_x":61,"stand_tile_y":94,
          "unlocked":true,"accepted_this_cycle":{{{accepted.ToString().ToLowerInvariant()}}},"menu_open":{{{menuOpen.ToString().ToLowerInvariant()}}},"dialogue_ready_for_board":false,
          "offers":[
            {"selection_index":0,"selection_side":"left","offer_fingerprint":"left-fingerprint","order":{"quest_key":"Caroline","quest_name":"Island Ingredients","requester":"Caroline","order_type":"","generation_seed":111,"due_date":20,"duration":"Week"}},
            {"selection_index":1,"selection_side":"right","offer_fingerprint":"right-fingerprint","order":{"quest_key":"Robin","quest_name":"Robin's Project","requester":"Robin","order_type":"","generation_seed":222,"due_date":20,"duration":"Week"}}
          ],"status":"{{{(accepted ? "blocked" : "ready")}}}","blocked_diagnostics":[{{{(accepted ? "\"special_order_type_already_accepted_this_cycle\"" : string.Empty)}}}]
        }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "menus": {"active_menu":{"value":{"is_open":{{{menuOpen.ToString().ToLowerInvariant()}}},"type":"{{{(menuOpen ? "SpecialOrdersBoard" : "none")}}}"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."), Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
