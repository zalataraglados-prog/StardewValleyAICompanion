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

    [Fact]
    public void BuildCoversAllRequiredGrandpaDirections()
    {
        var report = new GrandpaEvaluationGoalReport
        {
            TargetMet = false,
            Factors = new[]
            {
                Factor("community_center_access_or_completion", "world_progress", known: true, satisfied: false, maxPoints: 1),
                Factor("community_center_accessible_bonus", "world_progress", known: true, satisfied: false, maxPoints: 2),
                Factor("friendships_5", "social", known: true, satisfied: false, maxPoints: 1),
                Factor("friendships_10", "social", known: true, satisfied: false, maxPoints: 1),
                Factor("achievement_full_shipment", "economy", known: true, satisfied: false, maxPoints: 1),
                Factor("player_level_15", "skills", known: true, satisfied: false, maxPoints: 1),
                Factor("player_level_25", "skills", known: true, satisfied: false, maxPoints: 1),
                Factor("married_or_roommate_house_2", "social", known: true, satisfied: false, maxPoints: 1),
                Factor("achievement_master_angler", "world_progress", known: true, satisfied: false, maxPoints: 1),
                Factor("achievement_complete_collection", "world_progress", known: true, satisfied: false, maxPoints: 1),
                Factor("rusty_key", "world_progress", known: true, satisfied: false, maxPoints: 1),
                Factor("skull_key", "exploration", known: true, satisfied: false, maxPoints: 1),
                Factor("money_50000", "economy", known: true, satisfied: false, maxPoints: 1),
                Factor("pet_love", "farm", known: true, satisfied: false, maxPoints: 1)
            }
        };

        var sample = new GrandpaTrainingSampleAdapter().Build(new WorldModelEnvelope(), report);

        var directionIds = sample.CandidateDirections.Select(direction => direction.DirectionId).ToArray();
        Assert.Contains("complete_community_center", directionIds);
        Assert.Contains("raise_friendships", directionIds);
        Assert.Contains("complete_full_shipment", directionIds);
        Assert.Contains("raise_skill_levels", directionIds);
        Assert.Contains("marriage_and_house_upgrade", directionIds);
        Assert.Contains("complete_master_angler", directionIds);
        Assert.Contains("complete_museum_collection", directionIds);
        Assert.Contains("obtain_rusty_key", directionIds);
        Assert.Contains("obtain_skull_key", directionIds);
        Assert.Contains("earn_money", directionIds);
        Assert.Contains("earn_pet_love", directionIds);
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
