using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GingerHarvestMainlineTests
{
    [Fact]
    public void ExactGingerProjectionFlowsThroughNativeHoeQueue()
    {
        var snapshot = Snapshot(StateJson(energyCost: 1.4, isGinger: true));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "foraging.harvest_ginger" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_ginger", candidate.Kind);
        Assert.Equal("(O)829", candidate.QualifiedItemId);
        Assert.Equal(7, int.Parse(candidate.Parameters.Single(value => value.Name == "expected_foraging_experience_delta").Value));
        Assert.Contains(candidate.Parameters, value => value.Name == "skill_experience_projection_status" && value.Value.StartsWith("exact_"));

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("harvest_ginger", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_ginger", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("harvest_ginger", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsGingerOrEnergyProjectionDrift()
    {
        var initial = Snapshot(StateJson(energyCost: 1.4, isGinger: true));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "foraging.harvest_ginger" }, true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson(energyCost: 1.5, isGinger: false));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_ginger_target_not_found_or_drifted", queue.Items.Single().BlockingReasons);
    }

    private static string StateJson(double energyCost, bool isGinger)
    {
        return """
        {
          "player": {
            "location_id":{"value":"IslandWest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":100.0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{
            "terrain_features":{"value":[{"tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.HoeDirt","hoe_dirt_state":1,
              "has_crop":GINGER,"crop_is_forage":GINGER,"forage_crop_id":"2","is_ginger":GINGER,
              "ginger_harvest_status":"ready","ginger_required_tool_kind":"Hoe","ginger_tool_slot_index":3,
              "ginger_energy_cost":ENERGY_COST,"ginger_output_qualified_item_id":"(O)829","ginger_output_quality":0,"ginger_output_quantity_min":1,"ginger_output_quantity_max":1,
              "ginger_foraging_experience_on_success_min":7,"ginger_foraging_experience_on_success_max":7,
              "ginger_hoe_dirt_state_expected_after":0,"ginger_projection_status":"exact_from_native_crop_hit_with_hoe"}],
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"IslandWest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("GINGER", isGinger.ToString().ToLowerInvariant())
        .Replace("ENERGY_COST", energyCost.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
