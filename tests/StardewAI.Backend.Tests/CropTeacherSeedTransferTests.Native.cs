using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public void VerifiedNativeProjectionSettlesAllOwnedSlotsAndPassiveCompaction(bool split, bool deplete, bool emptyChest)
    {
        using var fixture = new Fixture(split, nativeEvidence: true, depleteSource: deplete,
            emptyChest: emptyChest, noPlayerSeed: emptyChest);
        var scope = fixture.Open();
        var prepared = fixture.Prepare(scope);
        var marker = scope.VerifyPreparationReply(prepared);
        var transition = fixture.NativeTransition(marker);
        var context = fixture.VerifyNative(marker, transition);
        var beforeLedger = JsonSerializer.Serialize(marker.Ledger);
        var result = new ReservationPortfolioLedgerService().SettleSupportingTransition(marker.Ledger,
            fixture.After, context.SettlementRequest, "2026-11-07T06:10:00Z");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var settled = context.VerifySettlementReply(result);
        Assert.Equal(beforeLedger, JsonSerializer.Serialize(marker.Ledger));
        Assert.Null(settled.PreparedMaterialTransfer);
        Assert.Equal(marker.Ledger.Revision + 1, settled.Revision);
        var supply = CropTeacherOwnedSeedSupply.Read(fixture.After, settled, new[] { Id }).Commitments.Single();
        Assert.Equal(10, supply.PlayerQuantity);
        Assert.Equal(0, supply.ExternalQuantity);
        Assert.Equal(split ? 2 : 1, supply.Slots.Count);
        Assert.Equal(prepared.Ledger!.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity,
            settled.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity);
        foreach (var original in prepared.Ledger.MaterialReservations.Where(row => row.SourceDecisionId != Owner))
        {
            var current = settled.MaterialReservations.Single(row => row.ReservationId == original.ReservationId);
            Assert.Equal(original.SourceDecisionId, current.SourceDecisionId);
            Assert.Equal(original.Quantity, current.Quantity);
            Assert.Equal(original.GoalId, current.GoalId);
            Assert.Equal(original.Purpose, current.Purpose);
            if (original.ReservationId == "outside") Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(current));
        }
        if (!emptyChest)
            Assert.Equal(deplete ? 0 : 1, settled.MaterialReservations.Single(row => row.ReservationId == "other-stone").SlotIndex);
        Assert.True(context.Proof.NativeOutcomeVerified);
        Assert.False(context.Proof.FormalProductTrainingAuthorized);
        var hashes = context.Proof.ArtifactHashes;
        Assert.Equal(4, hashes.Count);
        Assert.DoesNotContain(fixture.NativeProofPath, hashes.Keys);
        var resumed = marker.ResumeTransition(transition, fixture.NativeProofPath, marker.ValidateQueue(transition.QueuePath),
            context.SettlementRequest.SupportingTransitionReceiptSha256);
        Assert.Equal(JsonSerializer.Serialize(context.Proof), JsonSerializer.Serialize(resumed.Proof));
        Assert.Equal(JsonSerializer.Serialize(context.SettlementRequest), JsonSerializer.Serialize(resumed.SettlementRequest));
        Assert.Throws<IOException>(() => fixture.VerifyNative(marker, transition));
    }

    [Fact]
    public void DurableNativeSettlementReplaysOriginalLookupAfterRestartWithoutResendingMutation()
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var repository = new FileStrategyCommitmentRepository(fixture.RepositoryPath);
        Assert.True(repository.Upsert(fixture.Before, fixture.CropRequest).Accepted);
        Assert.True(repository.CommitReservationPortfolio(fixture.Before, fixture.ResourceRequest).Accepted);
        fixture.AdoptLedger(repository.Get(fixture.Before));
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(repository.CommitReservationPortfolio(fixture.Before, scope.PreparationRequest));
        var transition = fixture.NativeTransition(marker);
        var context = fixture.VerifyNative(marker, transition);
        var request = context.SettlementRequest;
        var result = repository.SettleReservationPortfolioSupportingTransition(fixture.After, request);
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var settled = context.VerifySettlementReply(result);
        var file = Assert.Single(Directory.GetFiles(fixture.RepositoryPath));
        var bytes = File.ReadAllBytes(file);
        var restarted = new FileStrategyCommitmentRepository(fixture.RepositoryPath);
        Assert.Equal(JsonSerializer.Serialize(settled), JsonSerializer.Serialize(restarted.Get(fixture.After)));
        var lookup = restarted.LookupPortfolioMutationReply(fixture.After, "settle-supporting-transition", JsonSerializer.SerializeToElement(request, JsonOptions));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.False(lookup["execution_performed"]!.GetValue<bool>());
        Assert.False(lookup["ledger_mutation_performed"]!.GetValue<bool>());
        Assert.False(lookup["native_outcome_verified"]!.GetValue<bool>());
        var reply = lookup["reply"]!.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions)!;
        Assert.Equal(JsonSerializer.Serialize(settled), JsonSerializer.Serialize(context.VerifySettlementReply(reply)));
        Assert.False(restarted.SettleReservationPortfolioSupportingTransition(fixture.After, request).Accepted);
        Assert.Equal(bytes, File.ReadAllBytes(file));
    }

    [Fact]
    public void TransferOnlyRebindsItsOwnCashAndDoesNotSpendReleaseOrRewriteAnotherOwner()
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var ownId = Id + ":seed-purchase:money";
        var cash = new ReservationPortfolioLedgerService().Commit(fixture.Ledger, fixture.Before, new ReservationPortfolioCommitRequest
        {
            StateHash = fixture.Before.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            PortfolioId = "fixture.cash", SourceDecisionId = Owner, GoalId = "earn_money",
            CurrencyClaims = new[]
            {
                new CurrencyReservationUpsertRequest { StateHash = fixture.Before.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
                    ReservationId = ownId, SourceDecisionId = Owner, GoalId = "earn_money", CurrencyId = 0,
                    Amount = 80, Purpose = "crop_seed_purchase:" + Id },
                new CurrencyReservationUpsertRequest { StateHash = fixture.Before.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
                    ReservationId = "other-money", SourceDecisionId = "other-root", GoalId = "earn_money", CurrencyId = 0,
                    Amount = 100, Purpose = "other-budget" }
            }
        }, Timestamp);
        Assert.True(cash.Accepted, string.Join(",", cash.Errors));
        fixture.AdoptLedger(cash.Ledger!);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var context = fixture.VerifyNative(marker, fixture.NativeTransition(marker));
        context.Proof.MaterialRelocations[0].Quantity = 999;
        context.SettlementRequest.MaterialRelocations[0].Quantity = 999;
        var request = context.SettlementRequest;
        Assert.Equal(new[] { ownId }, request.RebindActiveReservationIds);
        Assert.Empty(request.CurrencyConsumptions);
        Assert.NotEqual(999, request.MaterialRelocations[0].Quantity);
        var result = new ReservationPortfolioLedgerService().SettleSupportingTransition(marker.Ledger, fixture.After, request, Timestamp);
        var ledger = context.VerifySettlementReply(result);
        var own = ledger.CurrencyReservations.Single(row => row.ReservationId == ownId);
        Assert.Equal(80, own.Amount);
        Assert.Equal(StrategyCommitmentStatuses.Active, own.Status);
        Assert.Equal(fixture.After.StateHash, own.SourceStateHash);
        Assert.Equal(JsonSerializer.Serialize(cash.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money")),
            JsonSerializer.Serialize(ledger.CurrencyReservations.Single(row => row.ReservationId == "other-money")));
    }
}
