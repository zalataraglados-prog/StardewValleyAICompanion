using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class GameClockBudgetPolicyTests
{
    [Fact]
    public void ClockAdditionPreservesExtendedStardewHours()
    {
        Assert.Equal(2615, GameClockBudgetPolicy.AddClockMinutes(2555, 20));
        Assert.Equal(2510, GameClockBudgetPolicy.AddClockMinutes(2350, 80));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameClockBudgetPolicy.AddClockMinutes(600, -1));
    }
}
