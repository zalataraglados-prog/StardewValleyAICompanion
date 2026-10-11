using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class DeferredNativeDropPickupTests
{
    [Fact]
    public void ArtifactSpotPickupReusesSharedDeferredDebrisPrimitive()
    {
        var snapshot = Snapshot(ArtifactSpotState());

        var queue = new ActionQueueCompiler().Compile(
            ArtifactSpotRequest(snapshot.StateHash),
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
    public void ArtifactSpotPickupRejectsGuaranteedOutputDrift()
    {
        var snapshot = Snapshot(ArtifactSpotState(quantity: 2));

        var queue = new ActionQueueCompiler().Compile(
            ArtifactSpotRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_guaranteed_output_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void ArtifactSpotPickupRejectsAuthoritativeSourceDrift()
    {
        var snapshot = Snapshot(ArtifactSpotState(
            sourceId: "location:Default:11"));

        var queue = new ActionQueueCompiler().Compile(
            ArtifactSpotRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "deferred_pickup_authoritative_source_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    private static string ArtifactSpotState(
        int quantity = 3,
        string sourceId = "location:Default:10") => $$"""
        {
          "player": {
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":61,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "inventory_capacity":{"value":{"has_empty_slot":true,"empty_slots":12},"status":"available"}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}
          },
          "current_location": {
            "debris":{"value":[],"status":"available"},
            "objects":{"value":[{
              "tile_x":62,
              "tile_y":10,
              "qualified_item_id":"(O)590",
              "clear_kind":"artifact_spot",
              "clear_obstacle_executor_status":"ready",
              "clear_output_projection_status":"exact",
              "clear_output_items":[{
                "qualifiedItemId":"(O)330",
                "quality":0,
                "quantity":{{quantity}}
              }],
              "clear_authoritative_route_sources":[{
                "route_kind":"native_location_artifact_spot",
                "source_id":"{{sourceId}}",
                "qualified_item_id":"(O)330"
              }]
            }],"status":"available"}
          }
        }
        """;

    private static SmallModelActionEnvelope ArtifactSpotRequest(
        string stateHash) => new()
        {
            ModelOutputId = "artifact-spot-deferred-pickup-test",
            StateHash = stateHash,
            GoalId = "full_shipment:item:330",
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
                    ActionId = "pickup-clay-after-artifact-spot",
                    OptionId = "executor.pickup_debris",
                    Parameters = new[]
                    {
                        Parameter("target_location", "Farm"),
                        Parameter("target_tile_x", "62"),
                        Parameter("target_tile_y", "10"),
                        Parameter("qualified_item_id", "(O)330"),
                        Parameter("item_quality", "0"),
                        Parameter("inventory_item_total_before", "0"),
                        Parameter(
                            "deferred_pickup_source_kind",
                            "native_location_artifact_spot"),
                        Parameter(
                            "deferred_pickup_debris_item_total_before",
                            "0"),
                        Parameter(
                            "deferred_pickup_guaranteed_minimum_quantity",
                            "3"),
                        Parameter(
                            "acquisition_route_kind",
                            "native_location_artifact_spot"),
                        Parameter(
                            "acquisition_source_id",
                            "location:Default:10")
                    }
                }
            }
        };
}
