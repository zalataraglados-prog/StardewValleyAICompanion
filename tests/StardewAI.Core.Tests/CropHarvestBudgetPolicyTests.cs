using StardewAI.Core.Execution;
using Xunit;

namespace StardewAI.Core.Tests;

public sealed class CropHarvestBudgetPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void ConvertsCompilerHarvestTicksToConservativeGameMinutes(
        int harvestCount,
        int expectedMinutes)
    {
        Assert.Equal(
            expectedMinutes,
            CropHarvestBudgetPolicy.ConservativeGameMinutesForHarvests(
                harvestCount));
    }

    [Fact]
    public void RejectsNegativeHarvestCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CropHarvestBudgetPolicy.ConservativeGameMinutesForHarvests(-1));
    }
}
