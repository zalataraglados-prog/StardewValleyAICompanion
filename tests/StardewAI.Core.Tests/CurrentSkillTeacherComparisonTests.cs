using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class CurrentSkillTeacherComparisonTests
{
    [Fact]
    public void SameNativeEffectAndXpPreferLowerCostNotCandidateId()
    {
        var preferred = Row("z", 30);
        var alternative = Row("a", 210);
        var pair = CurrentSkillTeacherComparisonPolicy.Compare(preferred, alternative);
        Assert.NotNull(pair);
        Assert.Equal("z", pair.PreferredCandidateId);
        Assert.Equal("a", pair.AlternativeCandidateId);
        Assert.Equal("same_native_effect_lower_verified_estimated_ticks", pair.FirstDifferingAuthoritativeCriterion);
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(alternative, preferred));
    }

    [Fact]
    public void TiesRemainUnlabelledInBothOrders()
    {
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(Row("a", 30), Row("z", 30)));
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(Row("z", 30), Row("a", 30)));
    }

    [Fact]
    public void TimeEnergyTradeoffIsNotArbitrarilyWeighted()
    {
        var first = Row("a", 30);
        first.EnergyCost = 2;
        var second = Row("b", 210);
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(first, second));
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(second, first));
    }

    [Fact]
    public void UncertainXpCannotBeTurnedIntoGuaranteedPreference()
    {
        var first = Row("a", 30);
        first.UsefulXp[1] = new("fishing", 1, 10);
        var second = Row("b", 210);
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(first, second));
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(second, first));
    }

    [Theory]
    [InlineData("effect")]
    [InlineData("alias")]
    [InlineData("excluded")]
    [InlineData("cost")]
    [InlineData("family")]
    [InlineData("zero")]
    [InlineData("luck")]
    public void RequiresTwoDistinctCurrentPositiveXpChoices(string mutation)
    {
        var first = Row("a", 30);
        var second = Row("b", 210);
        switch (mutation)
        {
            case "effect": second.NativeEffectSignature = "different-consumption"; break;
            case "alias": second.NativeActionIdentity = first.NativeActionIdentity; break;
            case "excluded": second.Status = "excluded"; break;
            case "cost": first.EstimatedTicks = 0; break;
            case "family": second.OptionId = "skills.read_books"; break;
            case "zero": first.UsefulXp[1] = new("fishing", 0, 0); break;
            case "luck": first.UsefulXp[1] = new("luck", 5, 5); break;
        }
        Assert.Null(CurrentSkillTeacherComparisonPolicy.Compare(first, second));
    }

    [Fact]
    public void FingerprintKeepsUnknownEffectsAndIgnoresOnlyKnownGeometryAndLearnerSignals()
    {
        var first = Crab(51);
        var second = Crab(56);
        second.Score = 999999;
        second.ModelScore = -999999;
        second.Rank = 999;
        Assert.Equal(CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(first),
            CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(second));
        second.Parameters = second.Parameters.Append(new SmallModelActionParameter
            { Name = "future_consumed_material", Value = "(O)388:1" }).ToArray();
        Assert.NotEqual(CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(first),
            CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(second));
    }

    [Fact]
    public void FingerprintSeparatesQuantityItemAndUnmodelledEffectText()
    {
        var first = Crab(51);
        var changed = Crab(56);
        changed.Quantity = 2;
        Assert.NotEqual(CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(first),
            CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(changed));
        changed = Crab(56);
        changed.ExpectedEffect += ";unknown_native_callback=true";
        Assert.NotEqual(CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(first),
            CurrentSkillTeacherPreferenceBuilder.NativeEffectSignature(changed));
    }

    private static CurrentSkillTeacherCandidate Row(string id, int ticks) => new()
    {
        CandidateId = id, OptionId = "fishing.collect_crab_pots", Kind = "collect_crab_pot",
        NativeActionIdentity = "native:" + id, NativeEffectSignature = "same-typed-native-effect",
        Status = "admitted_current_xp", EstimatedTicks = ticks, CompiledMinutes = 1,
        UsefulXp = new[] { new SkillExperienceProjectionRange("farming", 0, 0), new("fishing", 5, 5),
            new("foraging", 0, 0), new("mining", 0, 0), new("combat", 0, 0) }
    };

    private static PolicyEventCandidatePrediction Crab(int y) => new()
    {
        CandidateId = "crab:" + y, OptionId = "fishing.collect_crab_pots", Kind = "collect_crab_pot",
        LocationId = "Farm", TileX = 39, TileY = y, Quantity = 1, QualifiedItemId = "(O)716",
        ExpectedEffect = $"crab_pot_stand_tile=39,{y + 1};output=(O)716;xp=5",
        Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "39" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = y.ToString() },
            new SmallModelActionParameter { Name = "stand_tile_x", Value = "39" },
            new SmallModelActionParameter { Name = "stand_tile_y", Value = (y + 1).ToString() },
            new SmallModelActionParameter { Name = "expected_skill_experience_delta", Value = "5" }
        }
    };
}
