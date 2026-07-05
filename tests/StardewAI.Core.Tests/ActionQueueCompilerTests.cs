using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ActionQueueCompilerTests
{
    [Fact]
    public void CompileTurnsRegisteredSmallModelActionIntoPendingQueueItem()
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

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("action_queue.v1", queue.SchemaVersion);
        Assert.Equal("pending", queue.Status);
        Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Items[0].Status);
        Assert.Equal("farm.maintain_crops", queue.Items[0].NormalizedCommand.OptionId);
    }

    [Fact]
    public void CompileBlocksUnknownOptionBeforeExecutor()
    {
        var snapshot = Snapshot("{}");
        var request = Request(snapshot.StateHash, "raw.keyboard.click");

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("unknown_option_id", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void DryRunExecutorDoesNotMutateButReturnsExecutionShape()
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
        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "farm.maintain_crops"), snapshot);

        var result = new DryRunExecutorPort().Execute(queue);

        Assert.False(new DryRunExecutorPort().ExecutionEnabled);
        Assert.Equal("execution_batch_result.v1", result.SchemaVersion);
        Assert.Equal("dry_run_ready", result.Status);
        Assert.Equal("dry_run_ready", result.Results[0].Status);
    }

    private static SmallModelActionEnvelope Request(string stateHash, string optionId)
    {
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output.test",
            SourceModel = "small-model.test",
            StateHash = stateHash,
            GoalId = "goal.test",
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
            RealTimestamp = "2026-07-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
