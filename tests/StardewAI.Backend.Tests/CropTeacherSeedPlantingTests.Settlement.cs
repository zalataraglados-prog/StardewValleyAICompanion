using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Theory]
    [InlineData(8, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void NativePrefixConsumesOnlyExactOwnedSeedsThroughOriginalRepository(int count, bool failedSuffix)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, count, failedSuffix);
        var context = f.Verify(marker, transition);
        Assert.Equal(count, context.Proof.ConsumedQuantity);
        Assert.Equal(count == 8, context.Proof.BatchCompleted);
        var ledger = context.VerifySettlementReply(f.Repository.SettleReservationPortfolioSupportingTransition(f.After, context.SettlementRequest));
        var claim = ledger.MaterialReservations.Single(row => row.ReservationId == f.OwnedSlotOne);
        Assert.Equal(9 - count, claim.Quantity);
        Assert.Equal(StrategyCommitmentStatuses.Active, claim.Status);
        Assert.Equal(10 - count, CropTeacherOwnedSeedSupply.Read(f.After, ledger, new[] { f.Id }).Commitments.Single().PlayerQuantity);
        Assert.Equal(ledger.Revision, f.Repository.Get(f.After).Revision);
        Assert.Equal(JsonSerializer.Serialize(marker.Ledger.MaterialReservations.Where(row => row.SourceDecisionId == "other-root"), JsonOptions),
            JsonSerializer.Serialize(ledger.MaterialReservations.Where(row => row.SourceDecisionId == "other-root"), JsonOptions));
        Assert.False(context.Proof.FormalProductTrainingAuthorized);
        Assert.Equal(64, context.Proof.MarkerLedgerSha256.Length);
        Assert.All(new[] { f.RequestPath, f.SourcePath, f.LedgerPath }, path => Assert.Contains(path, context.Proof.ArtifactHashes.Keys));
    }

    [Fact]
    public void ExhaustionNullsTheActualPlayerSlotWithoutCompactionAndCompletesOnlyItsClaim()
    {
        using var f = new Fixture(ownedStack: 1);
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, 1);
        var context = f.Verify(marker, transition);
        var ledger = context.VerifySettlementReply(f.Repository.SettleReservationPortfolioSupportingTransition(f.After, context.SettlementRequest));
        Assert.Equal(StrategyCommitmentStatuses.Completed, ledger.MaterialReservations.Single(row => row.ReservationId == f.OwnedSlotOne).Status);
        Assert.Equal(9, CropTeacherOwnedSeedSupply.Read(f.After, ledger, new[] { f.Id }).Commitments.Single().PlayerQuantity);
    }
}
