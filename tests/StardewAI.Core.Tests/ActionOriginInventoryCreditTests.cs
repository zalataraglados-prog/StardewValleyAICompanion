using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class ActionOriginInventoryCreditTests
{
    [Theory]
    [InlineData(11, 11, false, 0)]
    [InlineData(11, 12, false, 1)]
    [InlineData(11, 17, false, 6)]
    [InlineData(11, 17, true, 0)]
    [InlineData(11, 10, false, 0)]
    [InlineData(0, int.MaxValue, false, int.MaxValue)]
    public void CreditUsesProducerBaselineAndPreservesOriginalAmbiguityRejection(int before, int now, bool ambiguous, int expected)
    {
        Assert.Equal(expected, ActionOriginInventoryCredit.LowerBound(before, now, ambiguous));
    }

    [Fact]
    public void CreditsAndAbsoluteThresholdsCannotWrap()
    {
        Assert.Equal(0, ActionOriginInventoryCredit.LowerBound(int.MaxValue, 0, false));
        Assert.Equal(4294967294L, ActionOriginInventoryCredit.RequiredInventoryThreshold(int.MaxValue, int.MaxValue));
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 6, false)]
    [InlineData(6, 1, true)]
    public void CompletionRequiresOnlyRequestedAttributedQuantity(int credited, int required, bool expected)
    {
        Assert.Equal(expected, ActionOriginInventoryCredit.MeetsRequirement(credited, required));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void InvalidPhysicalCountsCannotCreateCredit(int before, int now)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionOriginInventoryCredit.LowerBound(before, now, false));
    }

    [Fact]
    public void InvalidRequirementsFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionOriginInventoryCredit.RequiredInventoryThreshold(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionOriginInventoryCredit.MeetsRequirement(1, 0));
    }
}
