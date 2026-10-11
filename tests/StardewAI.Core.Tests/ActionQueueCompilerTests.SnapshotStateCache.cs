using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ActionQueueSnapshotStateCacheTests
{
    [Fact]
    public void ClearObstacleValidationRebuildsAfterStateHashChanges()
    {
        var compiler = new ActionQueueCompiler();
        var snapshot = ClearObstacleValidationSnapshot(
            includeBlockedObject: true);

        var blocked = compiler.Compile(
            ClearObstacleRequest(snapshot.StateHash),
            snapshot);
        Assert.Equal("blocked", blocked.Status);
        Assert.Contains(
            "test_clear_projection_blocked",
            blocked.Items[0].BlockingReasons);

        var replacement = ClearObstacleValidationSnapshot(
            includeBlockedObject: false);
        snapshot.State = replacement.State;
        snapshot.StateHash = replacement.StateHash;
        var cleared = compiler.Compile(
            ClearObstacleRequest(snapshot.StateHash),
            snapshot);

        Assert.Equal("pending", cleared.Status);
        Assert.DoesNotContain(
            "test_clear_projection_blocked",
            cleared.Items[0].BlockingReasons);
        Assert.Equal(2, compiler.ClearObstacleValidationBuildCount);
    }

    private static SmallModelActionEnvelope ClearObstacleRequest(
        string stateHash)
    {
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output.snapshot-cache-test",
            SourceModel = "small-model.test",
            StateHash = stateHash,
            GoalId = "goal.test",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Actions =
            [
                new SmallModelAction
                {
                    ActionId = "action.snapshot-cache-test",
                    OptionId = "executor.clear_obstacle",
                    Rationale = "test",
                    Parameters =
                    [
                        new SmallModelActionParameter
                        {
                            Name = "target_tile_x",
                            Value = "11"
                        },
                        new SmallModelActionParameter
                        {
                            Name = "target_tile_y",
                            Value = "10"
                        },
                        new SmallModelActionParameter
                        {
                            Name = "max_tool_swings",
                            Value = "4"
                        }
                    ]
                }
            ]
        };
    }

    private static SnapshotEnvelope ClearObstacleValidationSnapshot(
        bool includeBlockedObject)
    {
        var stateJson = """
        {
          "player": {
            "location_id": {"value":"Farm","status":"available"},
            "tile_x": {"value":10,"status":"available"},
            "tile_y": {"value":10,"status":"available"},
            "energy": {"value":270,"status":"available"},
            "inventory": {"value":[],"status":"available"}
          },
          "current_location": {
            "objects": {"value":OBJECTS,"status":"available"},
            "terrain_features": {"value":[],"status":"available"},
            "map": {"value":{"width":80,"height":65},"status":"available"}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available"}
          }
        }
        """.Replace(
            "OBJECTS",
            includeBlockedObject
                ? """
                  [{
                    "tile_x":11,
                    "tile_y":10,
                    "qualified_item_id":"(O)343",
                    "clear_kind":"twig",
                    "clear_obstacle_executor_status":"test_clear_projection_blocked",
                    "expected_tool_hits_to_clear":1,
                    "tool_slot_index":0,
                    "required_tool_kind":"axe"
                  }]
                  """
                : "[]");
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(stateJson)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            Completeness = "complete",
            State = state
        };
    }
}
