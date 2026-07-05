using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.MockModel;

namespace StardewAI.Core.Tests;

public sealed class MockSmallModelPolicyTests
{
    [Fact]
    public void ClassifierSeparatesMechanicalAndParameterizedMechanicalTasks()
    {
        var classifier = new TaskIntentClassifier();

        var crops = classifier.Classify("water crops");
        var mining = classifier.Classify("mine to level 40");

        Assert.Equal(TaskIntentCategory.Mechanical, crops.Category);
        Assert.Equal("farm.maintain_crops", crops.OptionId);
        Assert.Equal(TaskIntentCategory.ParameterizedMechanical, mining.Category);
        Assert.Equal("exploration.visit_location", mining.OptionId);
        Assert.Contains(mining.Parameters, item => item.Name == "target_depth" && item.Value == "40");
    }

    [Fact]
    public void MockPolicyEmitsOfficialSmallModelActionContract()
    {
        var snapshot = Snapshot();

        var output = new MockSmallModelPolicy().Generate(snapshot, "water crops", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);

        Assert.Equal("small_model_action.v1", output.SchemaVersion);
        Assert.Equal("mock-small-model.rule.v1", output.SourceModel);
        Assert.Equal(snapshot.StateHash, output.StateHash);
        Assert.Equal("farm.maintain_crops", output.Actions[0].OptionId);
        Assert.Contains(output.Actions[0].Parameters, item =>
            item.Name == "intent_category" &&
            item.Value == TaskIntentCategory.Mechanical);
        Assert.Equal("pending", queue.Status);
    }

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
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
