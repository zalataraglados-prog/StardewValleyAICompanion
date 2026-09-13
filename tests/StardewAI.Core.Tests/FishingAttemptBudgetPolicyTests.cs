using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class FishingAttemptBudgetPolicyTests
{
    [Fact]
    public void AggregatesBeforeRoundingToGameMinutes()
    {
        var one = FishingAttemptBudgetPolicy
            .ConservativeGameMinutesForAttempts(1, challengeBait: false);
        var fourteen = FishingAttemptBudgetPolicy
            .ConservativeGameMinutesForAttempts(14, challengeBait: false);

        Assert.Equal(51, one);
        Assert.Equal(708, fourteen);
        Assert.True(fourteen <= one * 14);
    }

    [Fact]
    public void ChallengeBaitUsesItsLowerInitialProgress()
    {
        Assert.Equal(351, FishingAttemptBudgetPolicy.OrdinaryPerfectLockUpdates);
        Assert.Equal(451, FishingAttemptBudgetPolicy.ChallengeBaitPerfectLockUpdates);
        Assert.True(
            FishingAttemptBudgetPolicy.ConservativeAttemptMilliseconds(
                challengeBait: true) >
            FishingAttemptBudgetPolicy.ConservativeAttemptMilliseconds(
                challengeBait: false));
    }

    [Theory]
    [InlineData(0, false, 8d)]
    [InlineData(5, false, 7.5d)]
    [InlineData(20, false, 6d)]
    [InlineData(5, true, 0d)]
    public void UsesNativeFishingEnergyFormula(
        int level,
        bool efficient,
        double expected)
    {
        Assert.Equal(
            expected,
            FishingAttemptBudgetPolicy.EnergyPerAttempt(level, efficient),
            6);
    }

    [Fact]
    public void RejectsNegativeCountsAndLevels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FishingAttemptBudgetPolicy.ConservativeGameMinutesForAttempts(
                -1,
                challengeBait: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FishingAttemptBudgetPolicy.EnergyPerAttempt(-1, false));
    }
}
