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
    public void SupportingTransitionPreservesPartialClaimConsumption()
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

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(3, result.CommittedLedgerRevision);
        Assert.Empty(result.CompletedMaterialReservationIds);
        Assert.Equal(new[] { "route-a-material" },
            result.ActiveMaterialReservationIds);
        Assert.Equal("", result.CompletedMaterialReservationId);
        var material = Assert.Single(result.Ledger!.MaterialReservations);
        Assert.Equal(StrategyCommitmentStatuses.Active, material.Status);
        Assert.Equal(1, material.Quantity);
        Assert.Equal(2, material.Revision);
        Assert.Equal("", material.CompletionReason);
        Assert.Equal("", material.CompletionEvidenceSha256);
        var settlement = Assert.Single(result.MaterialSettlements);
        Assert.Equal(1, settlement.ConsumedQuantity);
        Assert.Equal(1, settlement.RemainingQuantity);
        Assert.Equal(StrategyCommitmentStatuses.Active,
            settlement.ReservationStatus);
        Assert.Contains(result.Ledger.History, row =>
            row.CommitmentId == "route-a-material" &&
            row.Operation == "material_reservation_partially_consumed");
    }

    [Fact]
    public void SupportingTransitionRejectsOverConsumptionAtomically()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            snapshot,
            Request(snapshot, materialQuantity: 1, moneyAmount: 300),
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
        var request = SupportingSettlementRequest(snapshot, revision: 2);
        request.ConsumedQuantity = 2;

        var result = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            request,
            "2026-09-26T00:02:00Z");

        Assert.False(result.Accepted);
        Assert.Contains("supporting_transition_consumed_claim_mismatch",
            result.Errors);
        Assert.Same(supportCommit.Ledger, result.Ledger);
        Assert.Equal(2, result.Ledger!.Revision);
        Assert.Equal(StrategyCommitmentStatuses.Active,
            Assert.Single(result.Ledger.MaterialReservations).Status);
    }

    [Fact]
    public void SupportingTransitionSettlesMultipleConsumptionsAtOneRevision()
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
        supportCommit.Ledger!.MaterialReservations = supportCommit.Ledger
            .MaterialReservations.Append(new MaterialReservation
            {
                ReservationId = "route-a-additional",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = "route:a",
                SourceStateHash = snapshot.StateHash,
                GoalId = "goal.grandpa_21",
                OwnerPlayerId = 123,
                NodeId = "player:123",
                SlotIndex = 1,
                QualifiedItemId = "(O)382",
                Quantity = 1,
                Purpose = "test route additional material"
            }).ToArray();
        var request = SupportingSettlementRequest(snapshot, revision: 2);
        request.MaterialReservationId = string.Empty;
        request.NodeId = string.Empty;
        request.QualifiedItemId = string.Empty;
        request.ConsumedQuantity = 0;
        request.MaterialConsumptions = new[]
        {
            new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = "route-a-material",
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = "(O)388",
                ConsumedQuantity = 1
            },
            new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = "route-a-additional",
                NodeId = "player:123",
                SlotIndex = 1,
                QualifiedItemId = "(O)382",
                ConsumedQuantity = 1
            }
        };

        var result = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            request,
            "2026-09-26T00:02:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(3, result.CommittedLedgerRevision);
        Assert.Equal(new[] { "route-a-additional" },
            result.CompletedMaterialReservationIds);
        Assert.Equal(new[] { "route-a-material" },
            result.ActiveMaterialReservationIds);
        Assert.Equal(2, result.ConsumedQuantity);
        Assert.Equal(2, result.MaterialSettlements.Length);
        Assert.Equal(3, result.Ledger!.History.Count(row =>
            row.LedgerRevision == 3));
    }

    [Fact]
    public void SupportingTransitionRejectsAmbiguousOrOverflowingSets()
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
        var mixed = SupportingSettlementRequest(snapshot, revision: 2);
        mixed.MaterialConsumptions = new[]
        {
            new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = "route-a-material",
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = "(O)388",
                ConsumedQuantity = 1
            }
        };
        var mixedResult = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            mixed,
            "2026-09-26T00:02:00Z");
        Assert.False(mixedResult.Accepted);
        Assert.Contains("supporting_transition_consumption_forms_mixed",
            mixedResult.Errors);
        Assert.Same(supportCommit.Ledger, mixedResult.Ledger);

        var overflow = SupportingSettlementRequest(snapshot, revision: 2);
        overflow.MaterialReservationId = string.Empty;
        overflow.NodeId = string.Empty;
        overflow.QualifiedItemId = string.Empty;
        overflow.ConsumedQuantity = 0;
        overflow.MaterialConsumptions = new[]
        {
            new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = "overflow-a",
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = "(O)388",
                ConsumedQuantity = int.MaxValue
            },
            new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = "overflow-b",
                NodeId = "player:123",
                SlotIndex = 1,
                QualifiedItemId = "(O)382",
                ConsumedQuantity = int.MaxValue
            }
        };
        var overflowResult = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            overflow,
            "2026-09-26T00:02:00Z");
        Assert.False(overflowResult.Accepted);
        Assert.Contains("supporting_transition_consumed_claim_invalid",
            overflowResult.Errors);
        Assert.Equal(0, overflowResult.ConsumedQuantity);
        Assert.Same(supportCommit.Ledger, overflowResult.Ledger);
    }

    [Fact]
    public void SupportingTransitionRejectsNullMaterialRowsAtomically()
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

        var nullConsumption = SupportingSettlementRequest(snapshot, 2);
        nullConsumption.MaterialReservationId = string.Empty;
        nullConsumption.NodeId = string.Empty;
        nullConsumption.QualifiedItemId = string.Empty;
        nullConsumption.ConsumedQuantity = 0;
        nullConsumption.MaterialConsumptions =
            new ReservationPortfolioMaterialConsumption[] { null! };
        var consumptionResult = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            nullConsumption,
            "2026-09-26T00:02:00Z");

        Assert.False(consumptionResult.Accepted);
        Assert.Contains(
            "supporting_transition_consumed_claim_invalid",
            consumptionResult.Errors);
        Assert.Same(supportCommit.Ledger, consumptionResult.Ledger);

        var nullRelocation = SupportingSettlementRequest(snapshot, 2);
        nullRelocation.MaterialReservationId = string.Empty;
        nullRelocation.NodeId = string.Empty;
        nullRelocation.QualifiedItemId = string.Empty;
        nullRelocation.ConsumedQuantity = 0;
        nullRelocation.MaterialRelocations =
            new ReservationPortfolioMaterialRelocation[] { null! };
        var relocationResult = service.SettleSupportingTransition(
            supportCommit.Ledger,
            snapshot,
            nullRelocation,
            "2026-09-26T00:02:00Z");

        Assert.False(relocationResult.Accepted);
        Assert.Contains(
            "supporting_transition_relocated_claim_invalid",
            relocationResult.Errors);
        Assert.Same(supportCommit.Ledger, relocationResult.Ledger);
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
