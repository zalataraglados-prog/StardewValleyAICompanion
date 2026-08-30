using System.Text.Json;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DartsGameMainlineTests
{
    [Fact]
    public void NextLimitedWalnutFlowsFromTransparentCandidateToFreshNativeExecutor()
    {
        var snapshot = Snapshot("darts-a", droppedBefore: 0, startingDarts: 20);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "minigame.play_darts" });
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_darts", candidate.Kind);
        AssertParameter(candidate.Parameters, "darts_limited_nut_dropped_before", "0");
        AssertParameter(candidate.Parameters, "darts_starting_dart_count", "20");
        AssertParameter(candidate.Parameters, "darts_perfect_score_plan", "T20,T20,T20,T20,T17,D5");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_darts", step.Kind);
        Assert.Contains("native_mouse_position_and_left_button_charge_release_only", step.SafetyConstraints);
        Assert.Contains("no_direct_score_dart_count_timer_rng_reward_or_progress_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_darts", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_darts", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void ThreeIssuedWalnutsAreExcludedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("darts-complete", gateStatus: "complete_three_darts_walnuts_dropped",
                droppedBefore: 3, droppedAfter: 3, startingDarts: 20), new[] { "minigame.play_darts" });
        var option = Assert.Single(availability.Options);
        Assert.False(option.Available);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Contains("complete_three_darts_walnuts_dropped", candidate.BlockReasons);
    }

    [Fact]
    public void NonPirateNightIsExcludedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("darts-day", gateStatus: "blocked_not_pirate_night", pirateNight: false),
                new[] { "minigame.play_darts" });
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("blocked_not_pirate_night", candidate.BlockReasons);
    }

    [Fact]
    public void FreshCompilerRejectsRewardProjectionDrift()
    {
        var original = Snapshot("darts-a", droppedBefore: 0, startingDarts: 20);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "minigame.play_darts" });
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("darts-b", droppedBefore: 1, startingDarts: 15);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("darts_game_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CapabilityAndRuntimeOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { "minigame.play_darts", "executor.play_darts" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-306" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-306" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-306" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-306" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-306" }, capability.OutputEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.DartsGame.cs"));
        var runtimeEntry = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var adapter = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.DartsGame.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeDartsGameSmoke.ps1"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("Game1.setMousePosition", runtime, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiLeftButtonOverride", runtime, StringComparison.Ordinal);
        Assert.Contains("ApplyDartsGameInput(activeDartsGame", runtimeEntry, StringComparison.Ordinal);
        Assert.Contains("cave?.IsRainingHere()", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("IslandSouthEastCave.isPirateNight()", adapter, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SUPPRESS_LOCAL_RENDER = \"1\"", smoke, StringComparison.Ordinal);
        Assert.Contains("evidence_id = \"EVD-306\"", smoke, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\.(points|dartCount|throwsCount|chargeTime|stateTimer)\s*="), runtime);
        Assert.DoesNotMatch(new Regex(@"limitedNutDrops\s*\["), runtime);
        Assert.DoesNotContain("Game1.random", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestLimitedNutDrops", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string fingerprint,
        string gateStatus = "ready",
        bool pirateNight = true,
        int droppedBefore = 0,
        int? droppedAfter = null,
        int startingDarts = 20)
    {
        droppedAfter ??= Math.Min(3, droppedBefore + 1);
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"IslandSouthEastCave","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":30,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "darts_game":{"value":{
              "schema_version":"darts_game.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"{{{fingerprint}}}000000000000000000000000000000000000000000000000000000000",
              "gate_status":"{{{gateStatus}}}","invocation_policy":"autonomous_progression",
              "location_id":"IslandSouthEastCave","is_current_location":true,"pirate_night":{{{pirateNight.ToString().ToLowerInvariant()}}},
              "limited_nut_key":"Darts","limited_nut_limit":3,
              "limited_nut_dropped_before":{{{droppedBefore}}},"limited_nut_dropped_after":{{{droppedAfter}}},
              "starting_dart_count":{{{startingDarts}}},"starting_points":301,"perfect_victory_max_throws":6,
              "perfect_score_plan":"T20,T20,T20,T20,T17,D5","charge_release_threshold":0.02,
              "interaction_tiles":[{"tile_x":30,"tile_y":8,"action_raw":"DartsGame","action_token":"DartsGame"}],
              "native_contract":"IslandSouthEastCave_DartsGame_checkAction_then_yes_then_native_Darts_mouse_aim_charge_release_then_native_limited_nut_drop"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"IslandSouthEastCave","width":40,"height":32,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "golden_walnuts":{"value":{"current":0,"found":0,"perfection_target":130},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
