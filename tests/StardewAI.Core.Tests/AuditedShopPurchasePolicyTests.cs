using StardewAI.Contracts.Execution;
using Xunit;

namespace StardewAI.Core.Tests;

public sealed class AuditedShopPurchasePolicyTests
{
    [Fact]
    public void OnlyBambooPoleIsAnAuditedNonObjectPurchase()
    {
        Assert.True(AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
            "(T)BambooPole"));
        Assert.False(AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
            "(T)TrainingRod"));
        Assert.False(AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
            "(T)FiberglassRod"));
        Assert.False(AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
            "(T)IridiumRod"));
        Assert.False(AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
            "(T)AdvancedIridiumRod"));
        Assert.False(AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(null));
    }
}
