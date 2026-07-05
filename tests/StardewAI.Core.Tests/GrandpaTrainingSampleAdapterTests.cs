using StardewAI.Contracts.Goals;
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
