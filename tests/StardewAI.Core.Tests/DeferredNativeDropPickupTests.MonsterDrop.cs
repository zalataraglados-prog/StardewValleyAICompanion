using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class DeferredNativeDropPickupTests
{
    [Fact]
    public void MonsterDropPickupReusesSharedDeferredDebrisPrimitive()
    {
        var snapshot = Snapshot(MonsterState());

        var queue = new ActionQueueCompiler().Compile(
            MonsterRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.pickup_debris", item.OptionId);
        Assert.Equal(
            "pickup_debris",
            Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MonsterDropPickupRejectsGuaranteedOutputDrift()
    {
        var snapshot = Snapshot(MonsterState(
            guaranteedDropQualifiedItemId: "(O)767"));

        var queue = new ActionQueueCompiler().Compile(
            MonsterRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_guaranteed_output_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    private static string MonsterState(
        string guaranteedDropQualifiedItemId = "(O)766") => $$"""
        {
          "player": {
            "location_id":{"value":"UndergroundMine45","status":"available"},
            "tile_x":{"value":3,"status":"available"},
            "tile_y":{"value":2,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "inventory_capacity":{"value":{"has_empty_slot":true,"empty_slots":12},"status":"available"}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}
          },
          "current_location": {
            "debris":{"value":[],"status":"available"}
          },
          "mining": {
            "monsters":{"value":[{
              "runtime_identity":"slime-1",
              "name":"Frost Jelly",
              "tile_x":7,
              "tile_y":7,
              "guaranteed_drop_qualified_item_ids":["{{guaranteedDropQualifiedItemId}}"],
              "authoritative_route_sources":[{
                "route_kind":"native_monster_drop_table",
                "source_id":"monster:Frost Jelly",
                "qualified_item_id":"(O)766"
              }]
            }],"status":"available"}
          }
        }
        """;

    private static SmallModelActionEnvelope MonsterRequest(
        string stateHash) => new()
        {
            ModelOutputId = "monster-drop-deferred-pickup-test",
            StateHash = stateHash,
            GoalId = "full_shipment:item:766",
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
                    ActionId = "pickup-monster-drop-after-combat",
                    OptionId = "executor.pickup_debris",
                    Parameters = new[]
                    {
                        Parameter("target_location", "UndergroundMine45"),
                        Parameter("target_tile_x", "7"),
                        Parameter("target_tile_y", "7"),
                        Parameter("qualified_item_id", "(O)766"),
                        Parameter("item_quality", "0"),
                        Parameter("inventory_item_total_before", "0"),
                        Parameter(
                            "deferred_pickup_source_kind",
                            "native_monster_drop_table"),
                        Parameter(
                            "deferred_pickup_debris_item_total_before",
                            "0"),
                        Parameter(
                            "deferred_pickup_guaranteed_minimum_quantity",
                            "1"),
                        Parameter(
                            "acquisition_route_kind",
                            "native_monster_drop_table"),
                        Parameter(
                            "acquisition_source_id",
                            "monster:Frost Jelly")
                    }
                }
            }
        };
}
