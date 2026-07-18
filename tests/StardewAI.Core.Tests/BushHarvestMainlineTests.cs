using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class BushHarvestMainlineTests
{
    [Fact]
    public void ExactBerryBushFlowsThroughNativeShakeQueue()
    {
        var snapshot = Snapshot(StateJson("ready", "ordinary_berry", "(O)410", 3, 4, 3));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "foraging.harvest_bushes" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_bush", candidate.Kind);
        Assert.Equal(3, candidate.Quantity);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "interaction_tile_x");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("harvest_bush", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_bush", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("harvest_bush", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsBushReadinessDrift()
    {
        var initial = Snapshot(StateJson("ready", "tea_leaf", "(O)815", 1, 0, 0));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "foraging.harvest_bushes" }, true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson("bush_not_ready", "tea_leaf", "(O)815", 1, 0, 0));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_bush_not_ready_by_transparent_state", queue.Items.Single().BlockingReasons);
    }

    private static string StateJson(string status, string branch, string outputId, int quantity, int quality, int xp)
    {
        return $$$"""
        {
          "player": {
            "location_id":{"value":"Forest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "skills":{"value":{"foraging":{"level":8}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{
            "debris":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "large_terrain_features":{"value":[{"tile_x":12,"tile_y":10,"runtime_type":"StardewValley.TerrainFeatures.Bush","bounding_tile_width":2,"bounding_tile_height":1,
              "is_bush":true,"bush_size":1,"bush_kind":"{{{branch}}}","ready_for_harvest":true,"in_bloom":true,"tile_sheet_offset_before":1,"tile_sheet_offset_expected_after":0,
              "bush_harvest_status":"{{{status}}}","bush_projection_status":"exact_from_native_bush_shake","bush_output_qualified_item_id":"{{{outputId}}}",
              "bush_output_quantity_min":{{{quantity}}},"bush_output_quantity_max":{{{quantity}}},"bush_output_quality":{{{quality}}},
              "bush_foraging_experience_on_success_min":{{{xp}}},"bush_foraging_experience_on_success_max":{{{xp}}},
              "bush_nut_key":"","bush_nut_collected_before":false,"bush_nut_collected_expected_after":false}],
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Forest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
