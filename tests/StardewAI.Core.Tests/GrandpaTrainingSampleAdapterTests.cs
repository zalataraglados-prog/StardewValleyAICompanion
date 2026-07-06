using StardewAI.Contracts.Goals;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GrandpaTrainingSampleAdapterTests
{
    [Fact]
    public void BuildCreatesCandidateDirectionsWithoutExecutorFeedback()
    {
        var model = new WorldModelEnvelope
        {
            StateHash = "hash-before",
            SchemaVersion = "world_model.v1"
        };
        var report = new GrandpaEvaluationGoalReport
        {
            CurrentScore = 9,
            TargetScore = 12,
            PointsNeeded = 3,
            TargetMet = false,
            Factors = new[]
            {
                Factor("money_500000", "economy", known: true, satisfied: false, maxPoints: 1),
                Factor("money_1000000", "economy", known: true, satisfied: false, maxPoints: 2),
                Factor("pet_love", "farm", known: true, satisfied: false, maxPoints: 1)
            }
        };

        var sample = new GrandpaTrainingSampleAdapter().Build(model, report);

        Assert.Equal("training_sample.v1", sample.SchemaVersion);
        Assert.Equal("hash-before", sample.SourceStateHash);
        Assert.False(sample.Target.Complete);
        Assert.False(sample.Feedback.ExecutorRequired);
        Assert.False(sample.Feedback.AvailableNow);
        Assert.Equal("hash-before", sample.Feedback.ObservedDelta.BeforeStateHash);
        Assert.Contains(sample.CandidateDirections, direction => direction.DirectionId == "earn_money" && direction.PotentialPoints == 3);
        Assert.Contains(sample.CandidateDirections, direction => direction.DirectionId == "earn_pet_love" && direction.PotentialPoints == 1);
    }

    [Fact]
    public void BuildBlocksWhenTransparentFactsAreMissing()
    {
        var report = new GrandpaEvaluationGoalReport
        {
            TargetMet = false,
            MissingFactPaths = new[] { "player.total_money_earned" },
            Factors = new[]
            {
                Factor("money_50000", "economy", known: false, satisfied: false, maxPoints: 1)
            }
        };

        var sample = new GrandpaTrainingSampleAdapter().Build(new WorldModelEnvelope(), report);

        Assert.True(sample.PlannerState.Blocked);
        Assert.Contains("missing_required_transparent_facts", sample.PlannerState.BlockReasons);
        Assert.Contains(sample.CandidateDirections, direction => direction.Blocked);
    }

    [Fact]
    public void BuildDoesNotBlockDirectionsWhenOnlyReevaluationItemContextIsMissing()
    {
        var report = new GrandpaEvaluationGoalReport
        {
            TargetMet = false,
            MissingFactPaths = new[] { "player.active_object_qualified_id" },
            Factors = new[]
            {
                Factor("friendships_5", "social", known: true, satisfied: false, maxPoints: 1)
            }
        };

        var sample = new GrandpaTrainingSampleAdapter().Build(new WorldModelEnvelope(), report);

        Assert.False(sample.PlannerState.Blocked);
        Assert.Contains("player.active_object_qualified_id", sample.PlannerState.MissingFactPaths);
        Assert.Contains(sample.CandidateDirections, direction =>
            direction.DirectionId == "raise_friendships" &&
            !direction.Blocked);
    }

    [Fact]
    public void StrategyFeatureRowsEnterPolicyTraining()
    {
        var model = new WorldModelEnvelope
        {
            StateHash = "hash-before",
            SchemaVersion = "world_model.v1",
            Mode = "strategic",
            Completeness = new WorldModelCompleteness
            {
                RequiredFactCount = 10,
                ReadableRequiredFactCount = 10,
                AllRequiredFactsReadable = true
            },
            PlannerInputs = new PlannerInputSummary
            {
                Blocked = false
            }
        };
        var report = new GrandpaEvaluationGoalReport
        {
            CurrentScore = 9,
            TargetScore = 12,
            PointsNeeded = 3,
            TargetMet = false,
            Factors = new[]
            {
                Factor("money_500000", "economy", known: true, satisfied: false, maxPoints: 1),
                Factor("money_1000000", "economy", known: true, satisfied: false, maxPoints: 2)
            }
        };
        var sample = new GrandpaTrainingSampleAdapter().Build(model, report);

        var rows = new GrandpaStrategyFeatureRowBuilder().Build(model, sample, 1);

        var row = Assert.Single(rows);
        Assert.Equal("strategy.grandpa_progress", row.ActionFeatures.OptionIds[0]);
        Assert.Equal("strategy_value", row.ActionFeatures.TrainingRole);
        Assert.Equal("policy_ranker", row.ActionFeatures.LearningScope);
        Assert.False(row.ActionFeatures.ExcludeFromPolicyTraining);
        Assert.Contains(row.ActionFeatures.Features.Categorical, item =>
            item.Name == "action.grandpa_direction_id" &&
            item.Value == "earn_money");

        var datasetPath = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"), "grandpa-rows.jsonl");
        new JsonlTrainingDatasetWriter().AppendMany(datasetPath, rows);
        var reportOut = new BaselineFeatureRowTrainer().Train(datasetPath);

        Assert.Equal(1, reportOut.IncludedRowCount);
        Assert.Equal(0, reportOut.ExcludedCalibrationRowCount);
        var score = Assert.Single(reportOut.OptionScores);
        Assert.Equal("strategy.grandpa_progress", score.OptionId);
        Assert.Equal(1, score.ExampleCount);
    }

    private static GrandpaEvaluationFactor Factor(string id, string domain, bool known, bool satisfied, int maxPoints)
    {
        return new GrandpaEvaluationFactor
        {
            Id = id,
            FactPath = domain,
            Known = known,
            Satisfied = satisfied,
            MaxPoints = maxPoints
        };
    }
}
