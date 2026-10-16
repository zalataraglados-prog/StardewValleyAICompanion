using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

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

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "executor.break_farm_resource_clump" },
                includeExecutorCalibrationOptions: true)
            .Options);
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
