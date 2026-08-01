using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Tests;

internal static class MaterialTransferTestFixture
{
    public static SmallModelActionParameter[] Parameters(
        bool includeStand = true,
        bool withdraw = false)
    {
        var parameters = new List<SmallModelActionParameter>
        {
            new() { Name = "source_node_id", Value = withdraw ? "chest:Farm:4,5" : "player:1" },
            new() { Name = "destination_node_id", Value = withdraw ? "player:1" : "chest:Farm:4,5" },
            new() { Name = "source_slot_index", Value = withdraw ? "0" : "2" },
            new() { Name = "qualified_item_id", Value = "(O)390" },
            new() { Name = "quality", Value = "0" },
            new() { Name = "quantity", Value = withdraw ? "3" : "10" },
            new() { Name = "expected_source_stack", Value = withdraw ? "5" : "40" }
        };
        if (includeStand)
        {
            parameters.Add(new SmallModelActionParameter { Name = "stand_tile_x", Value = "4" });
            parameters.Add(new SmallModelActionParameter { Name = "stand_tile_y", Value = "6" });
        }

        return parameters.ToArray();
    }

    public static SnapshotEnvelope Snapshot(bool locked)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            StateJson(locked),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-01T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string StateJson(bool locked) =>
        """
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":6,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "material_inventory_graph": {
              "value": {
                "schema_version":"material_inventory_graph.v1",
                "status":"available",
                "player_id":1,
                "inventory_nodes":[
                  {"node_id":"player:1","inventory_kind":"player_inventory","supply_state":"available","location_id":"Farm","ownership_class":"actor_owned","actor_use_authorized":true,"capacity":12,"slots":[{"slot_index":2,"qualified_item_id":"(O)390","runtime_type":"StardewValley.Object","stack":40,"maximum_stack_size":999,"quality":0}]},
                  {"node_id":"chest:Farm:4,5","inventory_kind":"chest","supply_state":"available","location_id":"Farm","tile_x":4,"tile_y":5,"ownership_class":"actor_owned","actor_use_authorized":true,"capacity":36,"slots":[{"slot_index":0,"qualified_item_id":"(O)390","runtime_type":"StardewValley.Object","stack":5,"maximum_stack_size":999,"quality":0}]},
                  {"node_id":"chest:Farm:8,5","inventory_kind":"chest","supply_state":"available","location_id":"Farm","tile_x":8,"tile_y":5,"ownership_class":"actor_owned","actor_use_authorized":true,"capacity":36,"slots":[]}
                ],
                "access_points":[
                  {"access_point_id":"access:placed_chest:Farm:4,5","node_id":"chest:Farm:4,5","access_kind":"placed_chest","location_id":"Farm","location_is_current":true,"tile_x":4,"tile_y":5,"special_chest_type":"None","actor_use_authorized":true,"locked_by_other_player":LOCKED},
                  {"access_point_id":"access:placed_chest:Farm:8,5","node_id":"chest:Farm:8,5","access_kind":"placed_chest","location_id":"Farm","location_is_current":true,"tile_x":8,"tile_y":5,"special_chest_type":"None","actor_use_authorized":true,"locked_by_other_player":false}
                ]
              },
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1
            }
          },
          "current_location": {
            "map": {"value":{"location_id":"Farm","width":80,"height":65},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":80,"height":65,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("LOCKED", locked ? "true" : "false", StringComparison.Ordinal);
}
