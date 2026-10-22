using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FarmSitePreparationCandidateTests
{
    [Fact]
    public void BoundedContextEmitsOnlyReadyToTillTiles()
    {
        var snapshot = Snapshot(
            """
            {
              "player": {
                "location_id":{"value":"Farm","status":"available"},
                "tile_x":{"value":5,"status":"available"},
                "tile_y":{"value":5,"status":"available"},
                "energy":{"value":270,"status":"available"},
                "inventory":{"value":[],"status":"available"}
              },
              "time":{"time":{"value":900,"status":"available"}},
              "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
              "current_location": {
                "map":{"value":{"id":"Farm"},"status":"available"},
                "site_preparation_context":{"value":{
                  "projection_status":"complete_bounded_current_farm_region",
                  "location_id":"Farm",
                  "hoe_tool_slot_index":2,
                  "hoe_tool_qualified_item_id":"(T)Hoe",
                  "hoe_energy_cost_per_single_tile_action":1.7,
                  "tiles":[
                    {"tile_x":7,"tile_y":5,"status":"ready_to_till"},
                    {"tile_x":8,"tile_y":5,"status":"already_prepared"},
                    {"tile_x":9,"tile_y":5,"status":"requires_single_clearance_then_till"}
                  ]
                },"status":"available"}
              },
              "locations":{
                "collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available"},
                "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
              }
            }
            """);

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "executor.till_soil" },
                includeExecutorCalibrationOptions: true)
            .Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("till:Farm:7,5", candidate.CandidateId);
        Assert.Equal("till_soil_tile", candidate.Kind);
        Assert.Equal(2, candidate.EnergyCost);
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "tool_slot_index" && value.Value == "2");
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "fresh_snapshot_rebind_required" && value.Value == "true");
    }

    [Fact]
    public void ReadyToTillCandidateFailsClosedAtZeroStamina()
    {
        var snapshot = Snapshot(
            """
            {
              "player": {
                "location_id":{"value":"Farm","status":"available"},
                "tile_x":{"value":5,"status":"available"},
                "tile_y":{"value":5,"status":"available"},
                "energy":{"value":0,"status":"available"},
                "inventory":{"value":[],"status":"available"}
              },
              "time":{"time":{"value":900,"status":"available"}},
              "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
              "current_location": {
                "map":{"value":{"id":"Farm"},"status":"available"},
                "site_preparation_context":{"value":{
                  "projection_status":"complete_bounded_current_farm_region",
                  "location_id":"Farm",
                  "hoe_tool_slot_index":2,
                  "hoe_tool_qualified_item_id":"(T)Hoe",
                  "hoe_energy_cost_per_single_tile_action":1.7,
                  "tiles":[{"tile_x":7,"tile_y":5,"status":"ready_to_till"}]
                },"status":"available"}
              },
              "locations":{
                "collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available"},
                "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
              }
            }
            """);

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "executor.till_soil" },
                includeExecutorCalibrationOptions: true)
            .Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(
            "till_soil_insufficient_stamina",
            candidate.BlockReasons);
    }

    [Fact]
    public void FarmResourceClumpCandidateCarriesTheFullCompilerBinding()
    {
        var snapshot = Snapshot(
            """
            {
              "player": {
                "location_id":{"value":"Farm","status":"available"},
                "tile_x":{"value":5,"status":"available"},
                "tile_y":{"value":5,"status":"available"},
                "energy":{"value":270,"status":"available"},
                "inventory":{"value":[],"status":"available"}
              },
              "time":{"time":{"value":900,"status":"available"}},
              "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
              "farm":{"resource_clumps":{"value":[{
                "tile_x":7,"tile_y":5,"width":2,"height":2,
                "parent_sheet_index":600,"clear_kind":"resource_stump",
                "clear_obstacle_executor_status":"ready","expected_tool_hits_to_clear":5,
                "clear_expected_output_items_json":"[{\"runtimeType\":\"StardewValley.Object\",\"qualifiedItemId\":\"(O)709\",\"quality\":0,\"unitStateSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"quantity\":2}]",
                "clear_output_projection_status":"exact_vanilla_conservative_minimum",
                "clear_output_native_contract":"native_resource_clump_destroy_contract",
                "tool_slot_index":1,"harvest_experience_projection_status":"exact",
                "harvest_experience_skill_id":"foraging","harvest_experience_on_success_min":25,
                "harvest_experience_on_success_max":25,
                "harvest_experience_condition":"native_player_axe_destroys_resource_stump_or_hollow_log"
              }],"status":"available"}},
              "locations":{
                "collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[
                  {"tile_x":7,"tile_y":5,"collision_blocked":true},
                  {"tile_x":8,"tile_y":5,"collision_blocked":true},
                  {"tile_x":7,"tile_y":6,"collision_blocked":true},
                  {"tile_x":8,"tile_y":6,"collision_blocked":true}
                ]},"status":"available"},
                "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
              }
            }
            """);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "executor.break_farm_resource_clump" },
                includeExecutorCalibrationOptions: true);
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "resource_clump_tile_x" && value.Value == "7");
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "resource_clump_width" && value.Value == "2");
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "tool_slot_index" && value.Value == "1");
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "max_tool_swings" && value.Value == "5");
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "expected_output_items_json" &&
            value.Value.Contains("(O)709", StringComparison.Ordinal));

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var queueItem = Assert.Single(queue.Items);
        Assert.Equal("pending", queueItem.Status);
        Assert.Contains(queueItem.NormalizedCommand.Parameters, value =>
            value.Name == "expected_output_items_json" &&
            value.Value.Contains("\"quantity\":2", StringComparison.Ordinal));

        var outputParameter = Assert.Single(
            plan.Steps[0].Parameters,
            value => value.Name == "expected_output_items_json");
        outputParameter.Value = outputParameter.Value.Replace(
            "\"quantity\":2",
            "\"quantity\":8",
            StringComparison.Ordinal);
        var tamperedQueue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("blocked", tamperedQueue.Status);
        Assert.Contains(
            "farm_resource_clump_output_projection_drifted",
            Assert.Single(tamperedQueue.Items).BlockingReasons);
    }

    [Fact]
    public void FarmResourceClumpRejectsCollisionFreeButUnreachableStandTiles()
    {
        var snapshot = Snapshot(
            """
            {
              "player": {
                "location_id":{"value":"Farm","status":"available"},
                "tile_x":{"value":5,"status":"available"},
                "tile_y":{"value":5,"status":"available"},
                "energy":{"value":270,"status":"available"},
                "inventory":{"value":[],"status":"available"}
              },
              "time":{"time":{"value":900,"status":"available"}},
              "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
              "farm":{"resource_clumps":{"value":[{
                "tile_x":8,"tile_y":5,"width":2,"height":2,
                "parent_sheet_index":600,"clear_kind":"resource_stump",
                "clear_obstacle_executor_status":"ready","expected_tool_hits_to_clear":5,
                "clear_expected_output_items_json":"[{\"runtimeType\":\"StardewValley.Object\",\"qualifiedItemId\":\"(O)709\",\"quality\":0,\"unitStateSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"quantity\":2}]",
                "clear_output_projection_status":"exact_vanilla_conservative_minimum",
                "clear_output_native_contract":"native_resource_clump_destroy_contract",
                "tool_slot_index":1,"harvest_experience_projection_status":"exact",
                "harvest_experience_skill_id":"foraging","harvest_experience_on_success_min":25,
                "harvest_experience_on_success_max":25,
                "harvest_experience_condition":"native_player_axe_destroys_resource_stump_or_hollow_log"
              }],"status":"available"}},
              "locations":{
                "collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[
                  {"tile_x":4,"tile_y":5,"collision_blocked":true},
                  {"tile_x":6,"tile_y":5,"collision_blocked":true},
                  {"tile_x":5,"tile_y":4,"collision_blocked":true},
                  {"tile_x":5,"tile_y":6,"collision_blocked":true}
                ]},"status":"available"},
                "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
              }
            }
            """);

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "executor.break_farm_resource_clump" },
                includeExecutorCalibrationOptions: true)
            .Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(
            "resource_clump_no_adjacent_route_stand_tile",
            candidate.BlockReasons);
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-16T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
