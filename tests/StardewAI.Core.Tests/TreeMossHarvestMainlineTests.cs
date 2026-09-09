using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class TreeMossHarvestMainlineTests
{
    [Fact]
    public void SeedlessMossTreeReusesClearObstacleNativeToolQueue()
    {
        var snapshot = Snapshot(StateJson("ready", hasSeed: false));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.harvest_tree_moss" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.Equal("(O)Moss", candidate.QualifiedItemId);
        Assert.Contains(candidate.Parameters, value => value.Name == "clear_completion_mode" && value.Value == "tree_moss_removed");
        Assert.Contains(candidate.Parameters, value => value.Name == "required_tool_kind" && value.Value == "scythe");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Contains(plan.Steps, step => step.Kind == "clear_obstacle");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items, value => value.OptionId == "executor.clear_obstacle");
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, value => value.Name == "expected_tree_growth_stage_after" && value.Value == "11");
        Assert.Contains(item.NormalizedCommand.Parameters, value => value.Name == "expected_moss_harvested_after" && value.Value == "8");
        Assert.Equal("clear_obstacle", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains("has_moss=false", item.NormalizedCommand.Steps[0].ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedBearingMossTreeIsExcludedUntilNativeSeedShakeCompletes()
    {
        var snapshot = Snapshot(StateJson("blocked_tree_seed_must_be_shaken_first", hasSeed: true));
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.harvest_tree_moss" },
            includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("blocked_tree_seed_must_be_shaken_first", candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsMossQuantityAndTreeStateDrift()
    {
        var initial = Snapshot(StateJson("ready", hasSeed: false));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "foraging.harvest_tree_moss" }, true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson(
            "ready",
            hasSeed: false,
            mossQuantity: 1,
            mossHarvestedAfter: 9,
            growthStageAfter: 10));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        var item = Assert.Single(queue.Items, value => value.OptionId == "executor.clear_obstacle");
        Assert.Contains("tree_moss_tree_state_projection_drifted", item.BlockingReasons);
        Assert.Contains("tree_moss_stat_or_experience_projection_drifted", item.BlockingReasons);
    }

    [Fact]
    public void TypedRuntimeFieldsRoundTrip()
    {
        var request = new TrainingExecutionRequest
        {
            ClearCompletionMode = "tree_moss_removed",
            ExpectedTreeHasMossBefore = true,
            ExpectedTreeGrowthStageAfter = 11,
            ExpectedTreeHealthAfter = 10,
            ExpectedMossHarvestedAfter = 8,
            MossHarvestProjectionStatus = "exact_seedless_native_scythe_moss_branch"
        };
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Equal("tree_moss_removed", roundTrip.ClearCompletionMode);
        Assert.True(roundTrip.ExpectedTreeHasMossBefore);
        Assert.Equal(11, roundTrip.ExpectedTreeGrowthStageAfter);
        Assert.Equal(10, roundTrip.ExpectedTreeHealthAfter);
        Assert.Equal(8, roundTrip.ExpectedMossHarvestedAfter);
    }

    private static string StateJson(
        string status,
        bool hasSeed,
        int mossQuantity = 2,
        int mossHarvestedAfter = 8,
        int growthStageAfter = 11) => $$$"""
    {
      "player": {
        "location_id":{"value":"Farm","status":"available"}, "tile_x":{"value":10,"status":"available"}, "tile_y":{"value":10,"status":"available"},
        "energy":{"value":270,"status":"available"}, "inventory":{"value":[],"status":"available"},
        "skills_detail":{"value":{"foraging":{"level":4,"experience":620}} ,"status":"available"}
      },
      "time":{"time":{"value":900,"status":"available"}},
      "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
      "current_location":{"map":{"value":{"id":"Farm"},"status":"available"},"debris":{"value":[],"status":"available"},"objects":{"value":[],"status":"available"},"terrain_features":{"value":[{
        "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.Tree","runtime_type":"StardewValley.TerrainFeatures.Tree",
        "growth_stage":14,"health":10,"stump":false,"tapped":false,"has_moss":true,"has_seed":{{{hasSeed.ToString().ToLowerInvariant()}}},"was_shaken_today":false,"max_shake":0,
        "moss_harvest_status":"{{{status}}}","moss_harvest_completion_mode":"tree_moss_removed","moss_harvest_tool_slot_index":2,"moss_harvest_required_tool_kind":"scythe",
        "moss_harvest_quantity":{{{mossQuantity}}},"moss_harvest_output_items":[{"runtimeType":"StardewValley.Object","qualifiedItemId":"(O)Moss","quality":0,"unitStateSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","quantity":{{{mossQuantity}}}}],
        "moss_harvest_output_projection_status":"exact_seedless_native_scythe_moss_branch","moss_harvest_moss_harvested_before":7,"moss_harvest_moss_harvested_after":{{{mossHarvestedAfter}}},
        "moss_harvest_foraging_experience_before":620,"moss_harvest_foraging_experience_after":{{{620 + mossQuantity}}},
        "moss_harvest_growth_stage_before":14,"moss_harvest_growth_stage_after":{{{growthStageAfter}}},"moss_harvest_health_before":10,"moss_harvest_health_after":10,
        "moss_harvest_has_moss_before":true,"moss_harvest_has_moss_after":false,"moss_harvest_has_seed_before":{{{hasSeed.ToString().ToLowerInvariant()}}},"moss_harvest_has_seed_after":false,
        "moss_harvest_was_shaken_today_before":false,"moss_harvest_was_shaken_today_after":true,
        "moss_harvest_native_contract":"MeleeWeapon(scythe) native tool lifecycle -> Tree.performToolAction -> Tree.CreateMossItem -> Game1.createMultipleItemDebris(Item.Stack=1 side effect) -> Tree.shake -> growthStage=11, seedless exact base Tree, no direct tree, RNG, debris, inventory, stat, or skill mutation"
      }],"status":"available"}},
      "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},"route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}}
    }
    """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-09T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
