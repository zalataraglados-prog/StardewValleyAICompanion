using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void SupportingTransitionPartiallyConsumesCurrencyAndRebindsClaims()
    {
        var before = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            Request(before, materialQuantity: 1, moneyAmount: 300),
            "2026-09-27T00:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-a:purchase",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-a:purchase"
            },
            "2026-09-27T00:01:00Z");
        var after = Snapshot();
        after.StateHash = new string('c', 64);
        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = after.StateHash,
                ExpectedLedgerRevision = 2,
                PortfolioId = "support:route-a:purchase",
                GoalId = "goal.grandpa_21",
                RouteSourceDecisionId = "route:a",
                SupportingTransitionReceiptSha256 = new string('d', 64),
                CurrencyConsumptions = new[]
                {
                    new ReservationPortfolioCurrencyConsumption
                    {
                        CurrencyReservationId = "route-a-money",
                        CurrencyId = NativeShopCurrencies.Money,
                        ConsumedAmount = 100
                    }
                },
                RebindActiveReservationIds = new[]
                {
                    "route-a-material",
                    "route-a-money"
                },
                Reason = "verified_machine_input_purchase"
            },
            "2026-09-27T00:02:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        var currency = Assert.Single(result.Ledger!.CurrencyReservations);
        Assert.Equal(StrategyCommitmentStatuses.Active, currency.Status);
        Assert.Equal(200, currency.Amount);
        Assert.Equal(2, currency.Revision);
        Assert.Equal(after.StateHash, currency.SourceStateHash);
        var material = Assert.Single(result.Ledger.MaterialReservations);
        Assert.Equal(StrategyCommitmentStatuses.Active, material.Status);
        Assert.Equal(2, material.Revision);
        Assert.Equal(after.StateHash, material.SourceStateHash);
        var settlement = Assert.Single(result.CurrencySettlements);
        Assert.Equal(100, settlement.ConsumedAmount);
        Assert.Equal(200, settlement.RemainingAmount);
        Assert.Equal(new[] { "route-a-money" },
            result.ActiveCurrencyReservationIds);
        Assert.Equal(new[] { "route-a-material", "route-a-money" },
            result.ReboundActiveReservationIds);
    }

    [Fact]
    public void SupportingTransitionCanRebindWithoutConsumingClaims()
    {
        var before = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            Request(before, materialQuantity: 1, moneyAmount: 300),
            "2026-09-27T00:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-a:route",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-a:route"
            },
            "2026-09-27T00:01:00Z");
        var after = Snapshot();
        after.StateHash = new string('e', 64);
        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = after.StateHash,
                ExpectedLedgerRevision = 2,
                PortfolioId = "support:route-a:route",
                GoalId = "goal.grandpa_21",
                RouteSourceDecisionId = "route:a",
                SupportingTransitionReceiptSha256 = new string('f', 64),
                RebindActiveReservationIds = new[]
                {
                    "route-a-material",
                    "route-a-money"
                },
                Reason = "verified_machine_input_purchase_route_progress"
            },
            "2026-09-27T00:02:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Empty(result.MaterialSettlements);
        Assert.Empty(result.CurrencySettlements);
        Assert.All(result.Ledger!.MaterialReservations,
            row => Assert.Equal(after.StateHash, row.SourceStateHash));
        Assert.All(result.Ledger.CurrencyReservations,
            row => Assert.Equal(after.StateHash, row.SourceStateHash));
        Assert.Equal(2, result.Ledger.History.Count(row =>
            row.Operation ==
                "reservation_rebound_after_supporting_transition"));
    }

    [Fact]
    public void SupportingTransitionCompletesCurrencyWithoutRebindingIt()
    {
        var before = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            Request(before, materialQuantity: 1, moneyAmount: 100),
            "2026-09-27T00:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-a:final-purchase",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-a:final-purchase"
            },
            "2026-09-27T00:01:00Z");
        var after = Snapshot();
        after.StateHash = new string('7', 64);
        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = after.StateHash,
                ExpectedLedgerRevision = 2,
                PortfolioId = "support:route-a:final-purchase",
                GoalId = "goal.grandpa_21",
                RouteSourceDecisionId = "route:a",
                SupportingTransitionReceiptSha256 = new string('8', 64),
                CurrencyConsumptions = new[]
                {
                    new ReservationPortfolioCurrencyConsumption
                    {
                        CurrencyReservationId = "route-a-money",
                        CurrencyId = NativeShopCurrencies.Money,
                        ConsumedAmount = 100
                    }
                },
                RebindActiveReservationIds = new[]
                {
                    "route-a-material"
                },
                Reason = "verified_machine_input_purchase"
            },
            "2026-09-27T00:02:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        var currency = Assert.Single(result.Ledger!.CurrencyReservations);
        Assert.Equal(StrategyCommitmentStatuses.Completed, currency.Status);
        Assert.Equal(new[] { "route-a-money" },
            result.CompletedCurrencyReservationIds);
        Assert.Empty(result.ActiveCurrencyReservationIds);
        Assert.Equal(new[] { "route-a-material" },
            result.ReboundActiveReservationIds);
    }

    [Fact]
    public void SupportingTransitionRejectsRebindingFullyConsumedCurrency()
    {
        var before = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            Request(before, materialQuantity: 1, moneyAmount: 100),
            "2026-09-27T00:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-a:invalid-final-purchase",
                GoalId = "goal.grandpa_21",
                SourceDecisionId =
                    "support:route-a:invalid-final-purchase"
            },
            "2026-09-27T00:01:00Z");
        var after = Snapshot();
        after.StateHash = new string('9', 64);
        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = after.StateHash,
                ExpectedLedgerRevision = 2,
                PortfolioId =
                    "support:route-a:invalid-final-purchase",
                GoalId = "goal.grandpa_21",
                RouteSourceDecisionId = "route:a",
                SupportingTransitionReceiptSha256 = new string('a', 64),
                CurrencyConsumptions = new[]
                {
                    new ReservationPortfolioCurrencyConsumption
                    {
                        CurrencyReservationId = "route-a-money",
                        CurrencyId = NativeShopCurrencies.Money,
                        ConsumedAmount = 100
                    }
                },
                RebindActiveReservationIds = new[]
                {
                    "route-a-material",
                    "route-a-money"
                },
                Reason = "verified_machine_input_purchase"
            },
            "2026-09-27T00:02:00Z");

        Assert.False(result.Accepted);
        Assert.Contains(
            "supporting_transition_rebind_claim_mismatch",
            result.Errors);
    }
}
