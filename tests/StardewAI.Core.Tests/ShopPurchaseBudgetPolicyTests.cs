using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ShopPurchaseBudgetPolicyTests
{
    [Fact]
    public void AggregatesWorstCaseRollingPurchaseStepsBeforeRounding()
    {
        Assert.Equal(17,
            ShopPurchaseBudgetPolicy.ConservativeGameMinutesForPurchases(1));
        Assert.Equal(33,
            ShopPurchaseBudgetPolicy.ConservativeGameMinutesForPurchases(2));
        Assert.Equal(0,
            ShopPurchaseBudgetPolicy.ConservativeGameMinutesForPurchases(0));
    }

    [Fact]
    public void RejectsNegativePurchaseCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShopPurchaseBudgetPolicy.ConservativeGameMinutesForPurchases(-1));
    }
}
