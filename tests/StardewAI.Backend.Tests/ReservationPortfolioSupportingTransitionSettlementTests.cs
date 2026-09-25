using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void SupportingTransitionSettlesOnlyExactConsumedMaterialClaim()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            snapshot,
            Request(snapshot, materialQuantity: 1, moneyAmount: 300),
            "2026-09-26T00:00:00Z");
        Assert.True(committed.Accepted, string.Join(";", committed.Errors));

        var supportCommit = service.Commit(
            committed.Ledger,
            snapshot,
            new ReservationPortfolioCommitRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-a",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-a"
            },
            "2026-09-26T00:01:00Z");
        Assert.True(supportCommit.Accepted,
            string.Join(";", supportCommit.Errors));

        var result = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            SupportingSettlementRequest(snapshot, revision: 2),
            "2026-09-26T00:02:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(3, result.CommittedLedgerRevision);
        Assert.Equal("route-a-material",
            result.CompletedMaterialReservationId);
        var material = Assert.Single(result.Ledger!.MaterialReservations);
        Assert.Equal(StrategyCommitmentStatuses.Completed, material.Status);
        Assert.Equal("verified_supporting_transition_consumption",
            material.CompletionReason);
        Assert.Equal(new string('b', 64),
            material.CompletionEvidenceSha256);
        Assert.Equal(StrategyCommitmentStatuses.Active,
            Assert.Single(result.Ledger.CurrencyReservations).Status);
        Assert.Contains(result.Ledger.History, row =>
            row.CommitmentId == "support:route-a" &&
            row.SourceDecisionId == "route:a" &&
            row.Operation ==
                "reservation_portfolio_supporting_transition_complete" &&
            row.Reason == new string('b', 64));
        Assert.DoesNotContain(result.Ledger.History, row =>
            row.Operation == "reservation_portfolio_route_complete");
    }

    [Fact]
    public void SupportingTransitionRejectsPartialClaimConsumption()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            snapshot,
            Request(snapshot, materialQuantity: 2, moneyAmount: 300),
            "2026-09-26T00:00:00Z");
        var supportCommit = service.Commit(
            committed.Ledger,
            snapshot,
            new ReservationPortfolioCommitRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-a",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-a"
            },
            "2026-09-26T00:01:00Z");

        var result = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            SupportingSettlementRequest(snapshot, revision: 2),
            "2026-09-26T00:02:00Z");

        Assert.False(result.Accepted);
        Assert.Contains(
            "supporting_transition_consumed_claim_mismatch",
            result.Errors);
        Assert.Same(supportCommit.Ledger, result.Ledger);
        Assert.Equal(StrategyCommitmentStatuses.Active,
            Assert.Single(result.Ledger!.MaterialReservations).Status);
    }

    private static ReservationPortfolioSupportingTransitionSettlementRequest
        SupportingSettlementRequest(
            StardewAI.Contracts.State.SnapshotEnvelope snapshot,
            int revision) => new()
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = revision,
                PortfolioId = "support:route-a",
                GoalId = "goal.grandpa_21",
                RouteSourceDecisionId = "route:a",
                SupportingTransitionReceiptSha256 = new string('b', 64),
                MaterialReservationId = "route-a-material",
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = "(O)388",
                ConsumedQuantity = 1,
                Reason = "verified_supporting_transition_consumption"
            };
}
