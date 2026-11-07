using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Tests;

public sealed partial class MoneyCropTeacherSupplyTests
{
    [Theory]
    [InlineData(350, 1, 80)]
    [InlineData(450, 0, 0)]
    public void CropSizeUsesUnreservedMoney(int reserved, int tiles, long cost)
    {
        using var fixture = new Fixture();
        fixture.AddCurrencyEvidence();
        var ledger = fixture.EmptyLedger();
        ledger.CurrencyReservations = new[] { MoneyReservation(ledger, reserved) };
        fixture.SetLedger(ledger);
        var decision = fixture.Build();
        var crop = decision.TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(tiles, crop.TileCount);
        Assert.Equal(cost, crop.SeedAcquisitionCost);
        Assert.Equal(tiles > 0, crop.Admitted);
        var receipt = fixture.Receipt(decision);
        Assert.True(receipt.AllCommitmentsAccepted);
        Assert.Equal(JsonSerializer.Serialize(ledger.CurrencyReservations, JsonOptions),
            JsonSerializer.Serialize(receipt.FinalLedger!.CurrencyReservations, JsonOptions));
    }

    [Fact]
    public void SeedAndCashReservationsComposeBeforeSelection()
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        var ledger = fixture.EmptyLedger();
        ledger.CurrencyReservations = new[] { MoneyReservation(ledger, 290) };
        ledger.MaterialReservations = new[] { SeedReservation(ledger, 2) };
        fixture.SetLedger(ledger);
        var decision = fixture.Build();
        var crop = decision.TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(1, crop.ExistingSeedQuantity);
        Assert.Equal(3, crop.TileCount);
        Assert.Equal(160, crop.SeedAcquisitionCost);
        var receipt = fixture.Receipt(decision);
        Assert.True(receipt.AllCommitmentsAccepted);
        Assert.Equal(JsonSerializer.Serialize(ledger.MaterialReservations, JsonOptions),
            JsonSerializer.Serialize(receipt.FinalLedger!.MaterialReservations, JsonOptions));
    }

    [Fact]
    public void MaterialOnlyReservationDoesNotRequireNewCurrencyEvidence()
    {
        using var fixture = new Fixture("474", 3);
        var ledger = fixture.EmptyLedger();
        ledger.MaterialReservations = new[] { SeedReservation(ledger, 2) };
        fixture.SetLedger(ledger);
        var crop = fixture.Build().TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(1, crop.ExistingSeedQuantity);
        Assert.Equal(6, crop.TileCount);
        Assert.Equal(400, crop.SeedAcquisitionCost);
    }

    [Theory]
    [InlineData(StrategyCommitmentStatuses.Cancelled)]
    [InlineData(StrategyCommitmentStatuses.Completed)]
    public void InactiveReservationsDoNotReduceCurrentSupply(string status)
    {
        using var fixture = new Fixture("474", 3);
        var ledger = fixture.EmptyLedger();
        var money = MoneyReservation(ledger, 450);
        var seed = SeedReservation(ledger, 3);
        money.Status = seed.Status = status;
        money.CancelReason = seed.CancelReason = "released";
        money.CompletionReason = seed.CompletionReason = "settled";
        money.CompletionEvidenceSha256 = seed.CompletionEvidenceSha256 = new string('a', 64);
        ledger.CurrencyReservations = new[] { money };
        ledger.MaterialReservations = new[] { seed };
        fixture.SetLedger(ledger);
        var crop = fixture.Build().TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(3, crop.ExistingSeedQuantity);
        Assert.Equal(8, crop.TileCount);
        Assert.Equal(400, crop.SeedAcquisitionCost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OvercommittedSharedSupplyFailsBeforeDecision(bool currency)
    {
        using var fixture = new Fixture("474", 3);
        fixture.AddCurrencyEvidence();
        var ledger = fixture.EmptyLedger();
        if (currency) ledger.CurrencyReservations = new[] { MoneyReservation(ledger, 451) };
        else ledger.MaterialReservations = new[] { SeedReservation(ledger, 4) };
        fixture.SetLedger(ledger);
        Assert.Contains(currency ? "currency_reservation_exceeds_balance" :
            "material_reservation_exceeds_stack",
            Assert.Throws<InvalidDataException>(() => fixture.Build()).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrDriftedCurrencyEvidenceCannotBorrowTheWallet(bool drifted)
    {
        using var fixture = new Fixture();
        if (drifted) fixture.AddCurrencyEvidence(451);
        var ledger = fixture.EmptyLedger();
        ledger.CurrencyReservations = new[] { MoneyReservation(ledger, 350) };
        fixture.SetLedger(ledger);
        Assert.Contains(drifted ? "shop_currency_money_balance_drifted" :
            "shop_currency_balances_missing_or_incomplete",
            Assert.Throws<InvalidDataException>(() => fixture.Build()).Message);
    }

    [Fact]
    public void MultipleClaimsIncludingSameDecisionAreAllCharged()
    {
        using var fixture = new Fixture();
        fixture.AddCurrencyEvidence();
        var ledger = fixture.EmptyLedger();
        var second = MoneyReservation(ledger, 150);
        second.ReservationId = "other-shared-budget";
        second.SourceDecisionId = "teacher.money.crop.supply";
        ledger.CurrencyReservations = new[] { MoneyReservation(ledger, 200), second };
        fixture.SetLedger(ledger);
        var crop = fixture.Build().TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(1, crop.TileCount);
        Assert.Equal(80, crop.SeedAcquisitionCost);
    }

    [Fact]
    public void NonMoneyCurrencyReservationDoesNotConsumeGold()
    {
        using var fixture = new Fixture();
        fixture.AddCurrencyEvidence(qiGems: 10);
        var ledger = fixture.EmptyLedger();
        var reservation = MoneyReservation(ledger, 7);
        reservation.CurrencyId = NativeShopCurrencies.QiGems;
        reservation.CurrencyKey = "qi_gems";
        ledger.CurrencyReservations = new[] { reservation };
        fixture.SetLedger(ledger);
        var crop = fixture.Build().TeacherCandidateEvaluations.Single(row => row.SeedId == "474");
        Assert.Equal(5, crop.TileCount);
        Assert.Equal(400, crop.SeedAcquisitionCost);
    }

    [Fact]
    public void UnavailableReservedSlotCannotBeTreatedAsUnreservedStock()
    {
        using var fixture = new Fixture("474", 3);
        var ledger = fixture.EmptyLedger();
        var reservation = SeedReservation(ledger, 1);
        reservation.SlotIndex = 99;
        ledger.MaterialReservations = new[] { reservation };
        fixture.SetLedger(ledger);
        Assert.Contains("material_reservation_slot_unavailable",
            Assert.Throws<InvalidDataException>(() => fixture.Build()).Message);
    }

    private static CurrencyReservation MoneyReservation(StrategyCommitmentLedger ledger, int amount) => new()
    {
        ReservationId = "house-upgrade-budget", Revision = 1,
        SourceDecisionId = "house-upgrade", SourceStateHash = ledger.SourceStateHash,
        GoalId = "marriage_and_house_upgrade", OwnerPlayerId = long.Parse(ledger.PlayerId),
        CurrencyId = NativeShopCurrencies.Money, CurrencyKey = "money", Amount = amount,
        Purpose = "preserve another root's construction budget"
    };

    private static MaterialReservation SeedReservation(StrategyCommitmentLedger ledger, int quantity) => new()
    {
        ReservationId = "collection-seed-budget", Revision = 1,
        SourceDecisionId = "collection-supply", SourceStateHash = ledger.SourceStateHash,
        GoalId = "complete_full_shipment", OwnerPlayerId = long.Parse(ledger.PlayerId),
        NodeId = "player:inventory", SlotIndex = 0, QualifiedItemId = "(O)474", Quantity = quantity,
        Purpose = "preserve another root's exact material allocation"
    };
}
