using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void MarriageModelInputsReuseContactAndDoNotEnterActionParameters()
    {
        var files = MarriageComparisonFiles();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var preference = CurrentMarriageTeacherPreferenceBuilder.Build(files.Intake, files.Calibration, files.Ledger);
        var original = JsonSerializer.Serialize(preference.DecisionCandidates);
        var candidates = new MarriagePolicyInputProjector(snapshot, File.ReadAllText(files.Calibration)).ProjectCandidates(preference.DecisionCandidates);
        Assert.Equal(original, JsonSerializer.Serialize(preference.DecisionCandidates));
        Assert.Equal(preference.DecisionCandidates.Length, candidates.Length);
        foreach (var candidate in candidates)
        {
            var prior = preference.DecisionCandidates.Single(row => row.CandidateId == candidate.CandidateId).Parameters;
            foreach (var parameter in prior)
                Assert.Contains(candidate.Parameters, row => row.Name == parameter.Name && row.Value == parameter.Value);
            Assert.All(candidate.Parameters.Where(row => !prior.Any(value => value.Name == row.Name)),
                row => Assert.StartsWith("friendship_current_admission_", row.Name, StringComparison.Ordinal));
        }
        var selected = candidates.Single(row => row.CandidateId == preference.SelectedCandidate!.CandidateId).PolicyFeatures!;
        Assert.Equal(100, selected.Numeric.Single(row => row.Name == "marriage.preparation.current_sale_value").Value);
        Assert.True(selected.Boolean.Single(row => row.Name == "social.contact.budget_verified").Value);
        Assert.DoesNotContain(selected.Numeric, row => row.Name.Contains("qualifying_count", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public void MarriageModelRequiresItsOriginalContactCalibration(string? calibration)
    {
        var files = MarriageComparisonFiles();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        Assert.Throws<InvalidOperationException>(() => new MarriagePolicyInputProjector(snapshot, calibration));
    }

    [Fact]
    public void MarriageProjectionCertifiesOtherFamiliesWithoutMakingThemGiftLabels()
    {
        var files = MarriageComparisonFiles();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var preference = CurrentMarriageTeacherPreferenceBuilder.Build(files.Intake, files.Calibration, files.Ledger);
        var original = JsonSerializer.Serialize(preference.DecisionCandidates);
        var inputs = new MarriagePolicyInputProjector(snapshot, File.ReadAllText(files.Calibration));
        var candidates = inputs.ProjectCandidates(preference.DecisionCandidates);
        var talk = Assert.Single(candidates.Where(row => row.OptionId == "social.talk_npc"));
        Assert.True(talk.PolicyFeatures!.Boolean.Single(row => row.Name == "social.contact.budget_verified").Value);
        Assert.Equal("other_original_family_not_a_gift_label", talk.PolicyFeatures.Categorical.Single(row => row.Name == "marriage.preparation.role").Value);
        Assert.Equal(original, JsonSerializer.Serialize(preference.DecisionCandidates));
        Assert.Equal(JsonSerializer.Serialize(candidates), JsonSerializer.Serialize(inputs.ProjectCandidates(candidates)));
    }

    [Fact]
    public void MarriageCurrentStateFeaturesDoNotChangeOtherGoalsOrUseFriendshipThresholds()
    {
        var snapshot = CurrentMarriageHouseStateTests.Snapshot(false, 3, days: -1);
        var projector = new PolicyStateFeatureProjector();
        var world = new WorldModelProjector().Project(snapshot, MarriagePolicyInputProjector.GoalId, "training_singleplayer");
        var features = projector.Project(world, snapshot);
        Assert.Equal(0, features.Numeric.Single(row => row.Name == "marriage.house_levels_missing").Value);
        Assert.Equal(-1, features.Numeric.Single(row => row.Name == "marriage.house_days_remaining").Value);
        Assert.False(features.Boolean.Single(row => row.Name == "marriage.current_joint_satisfied").Value);
        Assert.DoesNotContain(features.Numeric, row => row.Name.Contains("npcs.friendships", StringComparison.Ordinal));
        world.UserGoal = "goal.collection.master_angler";
        Assert.Equal(JsonSerializer.Serialize(projector.Project(world)), JsonSerializer.Serialize(projector.Project(world, snapshot)));
    }

    [Fact]
    public void MarriageMissingPriceDoesNotBecomeAFreeGift()
    {
        var files = MarriageComparisonFiles();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var preference = CurrentMarriageTeacherPreferenceBuilder.Build(files.Intake, files.Calibration, files.Ledger);
        CurrentMarriageHouseStateTests.Mutate(snapshot, "player", node => node["inventory"]!["value"]![0]!.AsObject().Remove("sell_to_store_price"));
        var selected = new MarriagePolicyInputProjector(snapshot, File.ReadAllText(files.Calibration)).ProjectCandidates(preference.DecisionCandidates)
            .Single(row => row.CandidateId == preference.SelectedCandidate!.CandidateId).PolicyFeatures!;
        Assert.False(selected.Boolean.Single(row => row.Name == "marriage.preparation.current_sale_value_known").Value);
        Assert.DoesNotContain(selected.Numeric, row => row.Name == "marriage.preparation.current_sale_value");
    }
}
