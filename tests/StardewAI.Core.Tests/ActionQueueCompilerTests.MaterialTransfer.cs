using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileMaterialTransferCarriesExactProjectionToRuntime()
    {
        var snapshot = MaterialTransferSnapshot(locked: false);
        var request = Request(snapshot.StateHash, "executor.transfer_material");
        request.Actions[0].Parameters = MaterialTransferParameters();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("transfer_material", step.StepType);
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "location_id" &&
                parameter.Value == "Farm");
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "target_tile_x" &&
                parameter.Value == "4");
        var intentJson = Assert.Single(
            item.NormalizedCommand.Parameters,
            parameter => parameter.Name == "material_transfer_intent_json").Value;
        var projectionJson = Assert.Single(
            item.NormalizedCommand.Parameters,
            parameter => parameter.Name == "material_transfer_projection_json").Value;
        var intent = System.Text.Json.JsonSerializer.Deserialize<MaterialTransferIntent>(intentJson)!;
        var projection = System.Text.Json.JsonSerializer.Deserialize<MaterialTransferProjection>(projectionJson)!;
        Assert.Equal(10, intent.Quantity);
        Assert.Equal("projected", projection.Status);
        Assert.Equal(10, projection.DestinationQuantityAfter - projection.DestinationQuantityBefore);
    }

    [Fact]
    public void CompileMaterialTransferBlocksLockedChestUpstream()
    {
        var snapshot = MaterialTransferSnapshot(locked: true);
        var request = Request(snapshot.StateHash, "executor.transfer_material");
        request.Actions[0].Parameters = MaterialTransferParameters();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "material_transfer_chest_locked_by_other_player",
            queue.Items[0].BlockingReasons);
    }

    private static SmallModelActionParameter[] MaterialTransferParameters() =>
        new[]
        {
            new SmallModelActionParameter { Name = "source_node_id", Value = "player:1" },
            new SmallModelActionParameter { Name = "destination_node_id", Value = "chest:Farm:4,5" },
            new SmallModelActionParameter { Name = "source_slot_index", Value = "2" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)390" },
            new SmallModelActionParameter { Name = "quality", Value = "0" },
            new SmallModelActionParameter { Name = "quantity", Value = "10" },
            new SmallModelActionParameter { Name = "expected_source_stack", Value = "40" },
            new SmallModelActionParameter { Name = "stand_tile_x", Value = "4" },
            new SmallModelActionParameter { Name = "stand_tile_y", Value = "6" },
            new SmallModelActionParameter { Name = "MATERIAL_TRANSFER_PROJECTION_JSON", Value = """{"status":"projected","destination_quantity_after":999999}""" },
            new SmallModelActionParameter { Name = "LOCATION_ID", Value = "forged" },
            new SmallModelActionParameter { Name = "TARGET_TILE_X", Value = "999" }
        };

    private static StardewAI.Contracts.State.SnapshotEnvelope MaterialTransferSnapshot(bool locked) =>
        Snapshot(
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
                      {"node_id":"chest:Farm:4,5","inventory_kind":"chest","supply_state":"available","location_id":"Farm","tile_x":4,"tile_y":5,"ownership_class":"actor_owned","actor_use_authorized":true,"capacity":36,"slots":[{"slot_index":0,"qualified_item_id":"(O)390","runtime_type":"StardewValley.Object","stack":5,"maximum_stack_size":999,"quality":0}]}
                    ],
                    "access_points":[{"access_point_id":"access:placed_chest:Farm:4,5","node_id":"chest:Farm:4,5","access_kind":"placed_chest","location_id":"Farm","tile_x":4,"tile_y":5,"special_chest_type":"None","locked_by_other_player":LOCKED}]
                  },
                  "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1
                }
              },
              "locations": {
                "collision_grid": {"value":{"width":80,"height":65,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus": {
                "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """.Replace("LOCKED", locked ? "true" : "false", StringComparison.Ordinal));
}
