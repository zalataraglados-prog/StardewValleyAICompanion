using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MoneyCropTeacherSupplyTests
{
    [Fact]
    public void MixedSeedAndMoneyClaimsCommitTogetherAndPreserveOtherRoots()
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        var prior = fixture.EmptyLedger();
        prior.CurrencyReservations = new[] { MoneyReservation(prior, 290) };
        prior.MaterialReservations = new[] { SeedReservation(prior, 2) };
        fixture.SetLedger(prior);
        var receipt = MoneyRevenueCropResourceReservationBuilder.Build(fixture.SaveDecision(fixture.Build()));
        Assert.Equal(1, receipt.SeedReservedQuantity);
        Assert.Equal(2, receipt.SeedAcquisitionQuantity);
        Assert.Equal(160, receipt.SeedAcquisitionCost);
        Assert.Single(receipt.ReservationRequest.MaterialClaims);
        Assert.Single(receipt.ReservationRequest.CurrencyClaims);
        var ledger = receipt.ReservationResult.Ledger!;
        Assert.Equal(receipt.ReservationRequest.ExpectedLedgerRevision + 1, ledger.Revision);
        Assert.Equal(JsonSerializer.Serialize(prior.MaterialReservations[0], JsonOptions),
            JsonSerializer.Serialize(ledger.MaterialReservations.Single(row =>
                row.ReservationId == prior.MaterialReservations[0].ReservationId), JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(prior.CurrencyReservations[0], JsonOptions),
            JsonSerializer.Serialize(ledger.CurrencyReservations.Single(row =>
                row.ReservationId == prior.CurrencyReservations[0].ReservationId), JsonOptions));
        var plan = fixture.ResourcePlan(receipt);
        var commitment = Assert.Single(plan.Commitments);
        Assert.Equal(1, commitment.CurrentReservedSeedQuantity);
        Assert.Equal(0, commitment.CurrentUnreservedSeedQuantity);
        Assert.Equal(2, commitment.SeedQuantityShortfall);
        Assert.False(plan.FreshSaveRevenueScheduleReady);
        Assert.False(receipt.RuntimeLedgerInstalled);
        Assert.False(receipt.FormalProductTrainingAuthorized);

        var competing = new CurrencyReservationLedgerService().Upsert(ledger, fixture.SourceSnapshot,
            new CurrencyReservationUpsertRequest
            {
                StateHash = ledger.SourceStateHash, ExpectedLedgerRevision = ledger.Revision,
                ReservationId = "competing-root", SourceDecisionId = "other", GoalId = "other",
                CurrencyId = NativeShopCurrencies.Money, Amount = 1, Purpose = "another root"
            }, "2026-11-07T00:00:00Z");
        Assert.False(competing.Accepted);
        Assert.Equal(450, ledger.CurrencyReservations.Sum(row => row.Amount));
    }

    [Theory]
    [InlineData(false, 0, 5, 400)]
    [InlineData(true, 8, 0, 0)]
    public void OnlyReservesCurrentSeedsAndExactPurchaseShortfall(
        bool allOwned, int reserved, int acquire, int cost)
    {
        using var fixture = allOwned ? new Fixture("474", 10) : new Fixture();
        if (!allOwned) fixture.AddCurrencyEvidence();
        var receipt = MoneyRevenueCropResourceReservationBuilder.Build(fixture.SaveDecision(fixture.Build()));
        Assert.Equal(reserved, receipt.SeedReservedQuantity);
        Assert.Equal(acquire, receipt.SeedAcquisitionQuantity);
        Assert.Equal(cost, receipt.SeedAcquisitionCost);
        Assert.Equal(reserved, receipt.ReservationRequest.MaterialClaims.Sum(row => row.Quantity));
        Assert.Equal(cost, receipt.ReservationRequest.CurrencyClaims.Sum(row => row.Amount));
        var commitment = Assert.Single(fixture.ResourcePlan(receipt).Commitments);
        Assert.Equal(reserved, commitment.CurrentReservedSeedQuantity);
        Assert.Equal(acquire, commitment.SeedQuantityShortfall);
        var verified = MoneyRevenueCropResourceReservationBuilder.Verify(fixture.SaveResourceReceipt(receipt));
        Assert.Equal(JsonSerializer.Serialize(receipt, JsonOptions), JsonSerializer.Serialize(verified, JsonOptions));
    }

    [Theory]
    [InlineData("cost")]
    [InlineData("claim")]
    [InlineData("ledger")]
    [InlineData("source")]
    [InlineData("runtime")]
    [InlineData("formal")]
    [InlineData("schema")]
    public void ReservationReceiptCannotPromoteOrAlterItsEvidence(string mutation)
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        var receipt = MoneyRevenueCropResourceReservationBuilder.Build(fixture.SaveDecision(fixture.Build()));
        switch (mutation)
        {
            case "cost": receipt.SeedAcquisitionCost++; break;
            case "claim": receipt.ReservationRequest.CurrencyClaims[0].Amount++; break;
            case "ledger": receipt.ReservationResult.Ledger!.MaterialReservations[0].Quantity++; break;
            case "source": receipt.SourceStateHash = new string('f', 64); break;
            case "runtime": receipt.RuntimeLedgerInstalled = true; break;
            case "formal": receipt.FormalProductTrainingAuthorized = true; break;
            case "schema": receipt.SchemaVersion = string.Empty; break;
        }
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropResourceReservationBuilder.Verify(
            fixture.SaveResourceReceipt(receipt)));
    }

    [Fact]
    public void AtomicReservationStillRequiresRealCurrencyEvidence()
    {
        using var fixture = new Fixture();
        var decisionPath = fixture.SaveDecision(fixture.Build());
        Assert.Contains("atomic commit rejected",
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropResourceReservationBuilder.Build(decisionPath)).Message);
    }

    [Fact]
    public void ManualLegacyDecisionCannotAcquireTeacherResourceAuthority()
    {
        using var fixture = new Fixture();
        var decision = fixture.Build();
        decision.SchemaVersion = "money_revenue_crop_production_decision_request.v1";
        Assert.Contains("v2 Teacher decision",
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropResourceReservationBuilder.Build(
                fixture.SaveDecision(decision))).Message);
    }

    [Fact]
    public void ForgedTeacherCostIsRejectedByOriginalDecisionReplay()
    {
        using var fixture = new Fixture();
        fixture.AddCurrencyEvidence();
        var decision = fixture.Build();
        decision.TeacherCandidateEvaluations = decision.TeacherCandidateEvaluations
            .Select(row => row.Selected ? row with { SeedAcquisitionCost = 0 } : row).ToArray();
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropResourceReservationBuilder.Build(
            fixture.SaveDecision(decision)));
    }

    [Fact]
    public void ExistingReservationIdentityCannotBeOverwritten()
    {
        using var fixture = new Fixture();
        fixture.AddCurrencyEvidence();
        var commitmentId = Assert.Single(fixture.Build().Commitments).CommitmentId;
        var prior = fixture.EmptyLedger();
        var existing = MoneyReservation(prior, 1);
        existing.ReservationId = commitmentId + ":seed-purchase:money";
        prior.CurrencyReservations = new[] { existing };
        fixture.SetLedger(prior);
        Assert.Contains("cannot overwrite",
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropResourceReservationBuilder.Build(
                fixture.SaveDecision(fixture.Build()))).Message);
    }
}
