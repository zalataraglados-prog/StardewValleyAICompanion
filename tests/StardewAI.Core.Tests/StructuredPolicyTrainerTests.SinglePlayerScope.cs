using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed partial class StructuredPolicyTrainerTests
{
    [Fact]
    public void RerankingKeepsSinglePlayerDonationAndUsesSnapshotScopeOverFeatureMode()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "scope-input.jsonl");
        WriteLines(input, Enumerable.Range(1, 28).Select(day => Serialize(Row(day, 100))));
        var dataset = PolicySupervisionFixture.DatasetBuilder().Build(input, Path.Combine(root, "dataset"));
        var checkpoint = PolicySupervisionFixture.Trainer().Train(dataset.ManifestPath, Path.Combine(root, "model.json")).Checkpoint;
        var state = Features(100);
        state.Categorical = state.Categorical.Concat(new[]
            { new CategoricalFeature { Name = "world.mode", Value = "training_singleplayer" } }).ToArray();
        var candidates = new[]
        {
            new PolicyEventCandidatePrediction
            {
                CandidateId = "donate", OptionId = "museum.donate_items", Kind = "donate_museum_item", Available = true
            }
        };
        var ranker = new StructuredPolicyRanker();
        var solo = ranker.Rank(checkpoint, state, candidates, ScopeSnapshot(false)).Single();
        Assert.True(solo.Available);
        Assert.NotNull(solo.ModelScore);
        var coop = ranker.Rank(checkpoint, state, candidates, ScopeSnapshot(true)).Single();
        Assert.False(coop.Available);
        Assert.Null(coop.ModelScore);
        var unknown = ranker.Rank(checkpoint, state, candidates, new SnapshotEnvelope()).Single();
        Assert.False(unknown.Available);
        Assert.True(candidates.Single().Available);
    }

    private static SnapshotEnvelope ScopeSnapshot(bool multiplayer) => new()
    {
        State = new Dictionary<string, JsonElement>
        {
            ["player"] = JsonSerializer.SerializeToElement(new
            {
                multiplayer_runtime = new { status = "available", value = new { context_is_multiplayer = multiplayer } }
            })
        }
    };
}
