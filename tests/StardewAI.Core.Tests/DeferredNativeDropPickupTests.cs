using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class DeferredNativeDropPickupTests
{
    [Fact]
    public void RadioactiveOrePickupReusesSharedDeferredDebrisPrimitive()
    {
        var snapshot = Snapshot(RadioactiveNodeState());

        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot.StateHash),
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
    public void RadioactiveOrePickupRejectsGuaranteedOutputDrift()
    {
        var snapshot = Snapshot(RadioactiveNodeState(
            guaranteedDropQualifiedItemId: "(O)390"));

        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_guaranteed_output_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RadioactiveOrePickupRejectsAuthoritativeSourceDrift()
    {
        var snapshot = Snapshot(RadioactiveNodeState(
            sourceId: "MineShaft.checkStoneForItems"));

        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_authoritative_source_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    private static string RadioactiveNodeState(
        string guaranteedDropQualifiedItemId = "(O)909",
        string sourceId = "GameLocation.breakStone") => $$"""
        {
          "player": {
            "location_id":{"value":"UndergroundMine99","status":"available"},
            "tile_x":{"value":5,"status":"available"},
            "tile_y":{"value":8,"status":"available"},
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
            "objects":{"value":[{
              "tile_x":5,
              "tile_y":9,
              "item_id":"95",
              "qualified_item_id":"(O)95",
              "is_breakable_stone":true,
              "drop_rule_branch":"game_location_break_stone_direct_node",
              "guaranteed_drop_qualified_item_ids":["{{guaranteedDropQualifiedItemId}}"],
              "authoritative_route_sources":[{
                "route_kind":"native_radioactive_ore_node",
                "source_id":"{{sourceId}}",
                "qualified_item_id":"(O)909"
              }]
            }],"status":"available"}
          }
        }
        """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionEnvelope Request(string stateHash) => new()
    {
        ModelOutputId = "radioactive-ore-deferred-pickup-test",
        StateHash = stateHash,
        GoalId = "full_shipment:item:909",
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
                ActionId = "pickup-radioactive-ore-after-node",
                OptionId = "executor.pickup_debris",
                Parameters = new[]
                {
                    Parameter("target_location", "UndergroundMine99"),
                    Parameter("target_tile_x", "5"),
                    Parameter("target_tile_y", "9"),
                    Parameter("qualified_item_id", "(O)909"),
                    Parameter("item_quality", "0"),
                    Parameter("inventory_item_total_before", "0"),
                    Parameter(
                        "deferred_pickup_source_kind",
                        "native_radioactive_ore_node"),
                    Parameter(
                        "deferred_pickup_debris_item_total_before",
                        "0"),
                    Parameter(
                        "deferred_pickup_guaranteed_minimum_quantity",
                        "1"),
                    Parameter(
                        "acquisition_route_kind",
                        "native_radioactive_ore_node"),
                    Parameter(
                        "acquisition_source_id",
                        "GameLocation.breakStone")
                }
            }
        }
    };

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
