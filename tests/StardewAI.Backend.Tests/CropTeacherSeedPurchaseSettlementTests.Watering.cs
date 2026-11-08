using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(20, false, false, false)]
    [InlineData(0, false, false, false)]
    [InlineData(20, true, false, false)]
    [InlineData(0, false, true, false)]
    [InlineData(20, false, false, true)]
    [InlineData(0, true, false, false)]
    public async Task WateringMaintenanceUsesOriginalBatchReceiptAndPreservesAllResourceOwners(int water, bool bottomless, bool enchanted, bool efficient)
    {
        using var f = new WateringFixture(water, bottomless, enchanted, efficient);
        await f.Emit();
        var scope = f.Open();
        Assert.Equal(2, scope.AppliedTileCount);
        Assert.True(scope.CompleteBatch);
        Assert.False(scope.NativeCropLifecycleVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Shared.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var accepted = scope.VerifyRebindReply(reply);
        AssertOtherRootsUnchanged(f.Shared.Source.Ledger, accepted);
        Assert.Equal(f.After.StateHash, accepted.SourceStateHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WateringMaintenanceAcceptsOnlyVerifiedPrefixAndNeverCreditsBlockedSuffix(bool failedSuffix)
    {
        using var f = new WateringFixture();
        await f.Emit(applied: 1, failedSuffix: failedSuffix);
        var scope = f.Open();
        Assert.Equal(1, scope.AppliedTileCount);
        Assert.Single(scope.AppliedTiles);
        Assert.False(scope.CompleteBatch);
    }

    [Fact]
    public async Task WateringMaintenanceUsesNativeSinglePrecisionEnergyAndAccountsBlockedRefillWithoutCropCredit()
    {
        using var f = new WateringFixture(water: 1, farmingLevel: 9);
        await f.Emit(applied: 1, failedSuffix: true, refillOnlySuffix: true);
        var scope = f.Open();
        Assert.Equal(1, scope.AppliedTileCount);
        Assert.False(scope.CompleteBatch);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Shared.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted);
        scope.VerifyRebindReply(reply);
    }
}
