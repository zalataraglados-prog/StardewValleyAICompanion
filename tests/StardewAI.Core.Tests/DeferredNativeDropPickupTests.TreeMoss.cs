using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class DeferredNativeDropPickupTests
{
    [Fact]
    public void TreeMossPickupReusesSharedDeferredDebrisPrimitive()
    {
        var snapshot = Snapshot(TreeMossState());

        var queue = new ActionQueueCompiler().Compile(
            TreeMossRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.pickup_debris", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(
            "pickup_debris",
            Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void TreeMossPickupRejectsGuaranteedOutputDrift()
    {
        var snapshot = Snapshot(TreeMossState(quantity: 2));

        var queue = new ActionQueueCompiler().Compile(
            TreeMossRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_guaranteed_output_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void TreeMossPickupRejectsAuthoritativeSourceDrift()
    {
        var snapshot = Snapshot(TreeMossState(
            sourceId: "Tree.performToolAction"));

        var queue = new ActionQueueCompiler().Compile(
            TreeMossRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_authoritative_source_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    private static string TreeMossState(
        int quantity = 1,
        string sourceId = "Tree.CreateMossItem") => $$"""
        {
          "player": {
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":65,"status":"available"},
            "tile_y":{"value":15,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "inventory_capacity":{"value":{"has_empty_slot":true,"empty_slots":12},"status":"available"}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}
          },
          "current_location": {
            "debris":{"value":[],"status":"available"},
            "terrain_features":{"value":[{
              "tile_x":64,
              "tile_y":15,
              "runtime_type":"StardewValley.TerrainFeatures.Tree",
              "moss_harvest_status":"ready",
              "moss_harvest_output_projection_status":"exact_seedless_native_scythe_moss_branch",
              "moss_harvest_output_items":[{
                "qualifiedItemId":"(O)Moss",
                "quality":0,
                "quantity":{{quantity}}
              }],
              "moss_harvest_authoritative_route_sources":[{
                "route_kind":"native_tree_moss_harvest",
                "source_id":"{{sourceId}}",
                "qualified_item_id":"(O)Moss"
              }]
            }],"status":"available"}
          }
        }
        """;

    private static SmallModelActionEnvelope TreeMossRequest(
        string stateHash) => new()
        {
            ModelOutputId = "tree-moss-deferred-pickup-test",
            StateHash = stateHash,
            GoalId = "full_shipment:item:Moss",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "pickup-moss-after-native-scythe",
                    OptionId = "executor.pickup_debris",
                    Parameters = new[]
                    {
                        Parameter("target_location", "Farm"),
                        Parameter("target_tile_x", "64"),
                        Parameter("target_tile_y", "15"),
                        Parameter("qualified_item_id", "(O)Moss"),
                        Parameter("item_quality", "0"),
                        Parameter("inventory_item_total_before", "0"),
                        Parameter(
                            "deferred_pickup_source_kind",
                            "native_tree_moss_harvest"),
                        Parameter(
                            "deferred_pickup_debris_item_total_before",
                            "0"),
                        Parameter(
                            "deferred_pickup_guaranteed_minimum_quantity",
                            "1"),
                        Parameter("acquisition_required_amount", "1"),
                        Parameter(
                            "acquisition_route_kind",
                            "native_tree_moss_harvest"),
                        Parameter(
                            "acquisition_source_id",
                            "Tree.CreateMossItem")
                    }
                }
            }
        };
}
