using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed partial class StructuredPolicyTrainerTests
{
    [Fact]
    public void CandidatePolicyFeaturesReachBothTheTrainingVocabularyAndOriginalRuntimeEncoder()
    {
        var root = TestRoot();
        var row = Row(1, 100);
        foreach (var candidate in row.Candidates)
            candidate.SourceCandidate.PolicyFeatures = new()
            {
                Numeric = new[] { new NumericFeature { Name = "social.contact.arrival_budget_minutes", Value = candidate.Rank * 10 } },
                Boolean = new[] { new BooleanFeature { Name = "social.contact.budget_verified", Value = true } },
                Categorical = new[] { new CategoricalFeature { Name = "social.contact.budget_status", Value = "verified" } }
            };
        row = PolicySupervisionFixture.Attach(row, "talk", "gift");
        var path = Path.Combine(root, "candidate-policy.jsonl");
        WriteLines(path, new[] { Serialize(row) });
        var dataset = PolicySupervisionFixture.DatasetBuilder().Build(path, Path.Combine(root, "dataset"));
        var trained = PolicySupervisionFixture.Trainer().Train(dataset.ManifestPath, Path.Combine(root, "unit-only-model.json"),
            new StructuredPolicyHyperparameters { Epochs = 1 });
        const string feature = "candidate.policy.numeric:social.contact.arrival_budget_minutes";
        Assert.Contains(feature, trained.Checkpoint.Model.FeatureNames);
        Assert.Contains("candidate.policy.boolean:social.contact.budget_verified", trained.Checkpoint.Model.FeatureNames);
        Assert.Contains("candidate.policy.categorical:social.contact.budget_status=verified", trained.Checkpoint.Model.FeatureNames);
        var model = new StructuredPolicyLinearModel
        { FeatureNames = new[] { feature }, FeatureMeans = new double[1], FeatureScales = new[] { 1d }, Weights = new[] { 1d } };
        var ranked = new StructuredPolicyRanker().Rank(CheckpointWithModel(model), row.StateFeatures,
            row.Candidates.Select(candidate => candidate.SourceCandidate).ToArray());
        Assert.All(ranked, candidate => Assert.Equal(candidate.PolicyFeatures!.Numeric.Single().Value, candidate.ModelScore));
    }
}
