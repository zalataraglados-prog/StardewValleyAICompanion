using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed class TrainingStateTransitionSimulatorTests
{
    [Fact]
    public void MaintainCropsProducesFactChangesAndEnergyCost()
    {
        var snapshot = Snapshot("""
        {
          "identity": {
            "save_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"123","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":610,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[{"tile_x":1,"tile_y":2,"needs_watering":true,"watered":false},{"tile_x":3,"tile_y":4,"needs_watering":false,"watered":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":{"endpoint":"ws://127.0.0.1:8766/api/v1/events/ws"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "farm.maintain_crops"), snapshot);
        var model = new WorldModelProjector().Project(snapshot, "maintain crops", "training");

        var result = new TrainingStateTransitionSimulator().Simulate(model, queue);

        Assert.False(result.Blocked);
        Assert.Equal(snapshot.StateHash, result.BeforeStateHash);
        Assert.StartsWith("sim.", result.AfterStateHash);
        Assert.Contains("farm.maintain_crops", result.AppliedOptionIds);
        Assert.Contains(result.ChangedFacts, item =>
            item.Path == "farm.crops[1,2].needs_watering" &&
            item.Before == "true" &&
            item.After == "false");
        Assert.Contains(result.ChangedFacts, item =>
            item.Path == "farm.crops[1,2].watered" &&
            item.Before == "false" &&
            item.After == "true");
        Assert.Contains(result.ResourceCosts, item => item.Resource == "player.energy" && item.Amount == 2);
    }

    [Fact]
    public void BlocksNonTrainingSingleplayerQueue()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "farm.maintain_crops");
        request.ExecutionMode = "coop_companion";
        request.Actor = new ActionActorRef
        {
            ActorId = "ai_companion.main",
            ActorType = "ai_companion",
            ControlSurface = "companion_actor"
        };
        var queue = new ActionQueueCompiler().Compile(request, snapshot);
        var model = new WorldModelProjector().Project(snapshot, "maintain crops", "training");

        var result = new TrainingStateTransitionSimulator().Simulate(model, queue);

        Assert.True(result.Blocked);
        Assert.Contains("only_training_singleplayer_supported", result.BlockReasons);
        Assert.Equal(string.Empty, result.AfterStateHash);
    }

    private static SmallModelActionEnvelope Request(string stateHash, string optionId)
    {
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output.test",
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
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "action.test",
                    OptionId = optionId,
                    Rationale = "test"
                }
            }
        };
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            InGameTime = new FieldEnvelope<int?>
            {
                Value = 610,
                Status = FieldStatus.Available,
                Source = new SourceRef { Kind = "game_object", Path = "test" },
                Adapter = "test",
                ReadAtTick = 1,
                Confidence = 1
            },
            RealTimestamp = "2026-07-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
