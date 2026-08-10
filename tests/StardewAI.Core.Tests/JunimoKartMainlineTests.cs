using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class JunimoKartMainlineTests
{
    [Fact]
    public void JkScoreObjectiveCompilesThroughNativeEndlessModeChain()
    {
        var snapshot = Snapshot(StateJson(hasSkullKey: true));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "quest.advance" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_junimo_kart", candidate.Kind);
        Assert.Equal("Saloon", candidate.LocationId);
        Assert.Equal(14, candidate.TileX);
        Assert.Equal(4, candidate.TileY);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "minigame_mode" && parameter.Value == "2");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "minigame_target_score" && parameter.Value == "50000");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_objective_index" && parameter.Value == "0");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal(
            new[] { "move_to_tile", "interact", "choose_dialogue_response", "play_junimo_kart" },
            plan.Steps.Select(step => step.Kind));

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var terminal = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.play_junimo_kart"));
        Assert.Empty(terminal.BlockingReasons);
        Assert.Contains(terminal.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "minigame_target_score" && parameter.Value == "50000");
        Assert.Equal("play_junimo_kart", Assert.Single(terminal.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void JkScoreObjectiveFailsClosedWithoutNativeSkullKeyAccess()
    {
        var snapshot = Snapshot(StateJson(hasSkullKey: false));
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options
            .Single()
            .EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("junimo_kart_skull_key_required", candidate.BlockReasons);
    }

    [Fact]
    public void RuntimeUsesNativeInputAndObservationWithoutDirectScoreSubmissionOrMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.JunimoKart.cs"));

        Assert.Contains("TryApplySmapiLeftButtonOverride", source, StringComparison.Ordinal);
        Assert.Contains("MineCart.Bubble", source, StringComparison.Ordinal);
        Assert.Contains("objective.GetCount()", source, StringComparison.Ordinal);
        Assert.Contains("result.QuestProgressAfter = active.Objective.GetCount()", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".submitHighScore(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Die(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".SetCount(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MineCartScoreField.SetValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MineCartPlayerField.SetValue", source, StringComparison.Ordinal);

        var interact = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Interact.cs"));
        Assert.Contains("Arcade_Minecart", interact, StringComparison.Ordinal);

        var dialogue = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.DialogueChoice.cs"));
        Assert.Contains("MinecartGame", dialogue, StringComparison.Ordinal);
        Assert.Contains("expected_native_minigame_started", dialogue, StringComparison.Ordinal);
    }

    private static string StateJson(bool hasSkullKey) => """
    {
      "player": {
        "location_id":{"value":"Saloon","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "tile_y":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "has_skull_key":{"value":__SKULL_KEY__,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "current_location": {
        "arcade_action_tiles":{"value":[{"tile_x":14,"tile_y":4,"action":"Arcade_Minecart","action_type":"Arcade_Minecart","unlocked":__SKULL_KEY__,"unlock_requirement":"player.has_skull_key"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "locations": {
        "collision_grid":{"value":{"location_id":"Saloon","width":50,"height":30,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "quests": {
        "active_quests":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "special_orders":{"value":[{"quest_key":"QiChallenge3","quest_name":"Let's Play A Game","quest_state":"InProgress","special_rule":"","is_island_order":1,"objectives":[{"description":"Score 50,000 points in Junimo Kart endless mode.","current_count":0,"max_count":50000,"runtime_type":"JKScoreObjective","fail_on_completion":false,"complete":false,"per_type_fields":{"available":true}}],"rewards":[]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "completed_special_orders":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "mail_received":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "menus": {
        "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "time": {
        "time":{"value":1200,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      },
      "world_progress": {
        "community_center":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "achievements":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
      }
    }
    """.Replace(
        "__SKULL_KEY__",
        hasSkullKey ? "true" : "false",
        StringComparison.Ordinal);

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-10T00:00:00Z",
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
