using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GreenRainResourceClumpMainlineTests
{
    [Fact]
    public void ExactGreenRainResourceClumpFlowsThroughNativeAxeQueue()
    {
        var snapshot = Snapshot(StateJson(0.016));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.clear_green_rain_bushes" },
            true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear_green_rain_resource_clump", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "secret_note_combined_probability" && parameter.Value == "0.016");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("break_current_location_resource_clump", planStep.Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var queueItem = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.break_current_location_resource_clump", queueItem.OptionId);
        Assert.Empty(queueItem.BlockingReasons);
        Assert.Equal("break_resource_clump", Assert.Single(queueItem.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsSecretNoteProbabilityDrift()
    {
        var initial = Snapshot(StateJson(0.016));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            initial,
            new[] { "foraging.clear_green_rain_bushes" },
            true);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson(0.012));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("green_rain_resource_clump_output_projection_drifted", queue.Items.Single().BlockingReasons);
    }

    private static string StateJson(double combinedProbability)
    {
        return $$$"""
        {
          "player": {
            "location_id":{"value":"Forest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":200,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{
            "resource_clumps":{"value":[{
              "location_id":"Forest","tile_x":10,"tile_y":10,"runtime_type":"StardewValley.TerrainFeatures.ResourceClump",
              "parent_sheet_index":44,"width":2,"height":2,"health":4,"clear_kind":"green_rain_bush",
              "clear_obstacle_executor_status":"ready","required_tool_kind":"axe","minimum_tool_upgrade_level":0,
              "tool_slot_index":0,"tool_upgrade_level":1,"damage_per_hit":1.5,"expected_tool_hits_to_clear":3,
              "expected_foraging_experience_delta":15,"foraging_experience_projection_status":"exact_from_resource_clump_destroy",
              "core_output_projection_status":"exact_from_day_save_coordinate_rng",
              "expected_core_output_items_json":"[{\"runtimeType\":\"StardewValley.Object\",\"qualifiedItemId\":\"(O)Moss\",\"quality\":0,\"unitStateSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"quantity\":2},{\"runtimeType\":\"StardewValley.Object\",\"qualifiedItemId\":\"(O)771\",\"quality\":0,\"unitStateSha256\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"quantity\":3}]",
              "possible_secret_note_qualified_item_id":"(O)79","unseen_secret_note_count":4,"total_secret_note_count":25,
              "secret_note_outer_roll_probability":0.05,"secret_note_inner_roll_probability":0.32,
              "secret_note_combined_probability":{{{combinedProbability.ToString(System.Globalization.CultureInfo.InvariantCulture)}}},
              "secret_note_projection_status":"bounded_probability_global_rng_not_consumed",
              "output_distribution_status":"exact_seeded_core_plus_bounded_secret_note_probability",
              "native_contract":"axe_DoFunction_to_GameLocation.performToolAction_then_ResourceClump.destroy"
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "debris":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Forest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
