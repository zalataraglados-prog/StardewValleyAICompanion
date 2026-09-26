using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    public ReservationPortfolioSupportingTransitionSettlementResult
        SettleSupportingTransition(
            StrategyCommitmentLedger? current,
            SnapshotEnvelope snapshot,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        ValidateSupportingTransitionSettlementRequest(current, request, errors);
        if (errors.Count > 0)
            return SupportingTransitionSettlementRejected(
                current,
                request,
                errors);

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        ledger.MaterialReservations = ledger.MaterialReservations.Select(row =>
        {
            if (row.ReservationId != request.MaterialReservationId)
                return row;
            var completed = StrategyCommitmentLedgerSupport.CloneMaterial(row);
            completed.Status = StrategyCommitmentStatuses.Completed;
            completed.Revision++;
            completed.CompletionReason = request.Reason;
            completed.CompletionEvidenceSha256 =
                request.SupportingTransitionReceiptSha256;
            return completed;
        }).ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        var consumed = ledger.MaterialReservations.Single(row =>
            row.ReservationId == request.MaterialReservationId);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            consumed.ReservationId,
            consumed.Revision,
            consumed.SourceDecisionId,
            "material_reservation_consumed",
            updatedAt,
            request.Reason);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            request.PortfolioId,
            ledger.Revision,
            request.RouteSourceDecisionId,
            "reservation_portfolio_supporting_transition_complete",
            updatedAt,
            request.SupportingTransitionReceiptSha256);
        return new ReservationPortfolioSupportingTransitionSettlementResult
        {
            Accepted = true,
            PortfolioId = request.PortfolioId,
            RouteSourceDecisionId = request.RouteSourceDecisionId,
            SupportingTransitionReceiptSha256 =
                request.SupportingTransitionReceiptSha256,
            CommittedLedgerRevision = ledger.Revision,
            CompletedMaterialReservationId = request.MaterialReservationId,
            ConsumedQuantity = request.ConsumedQuantity,
            Ledger = ledger
        };
    }

    private static void ValidateSupportingTransitionSettlementRequest(
        StrategyCommitmentLedger? current,
        ReservationPortfolioSupportingTransitionSettlementRequest request,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.PortfolioId))
            errors.Add("reservation_portfolio_id_required");
        if (string.IsNullOrWhiteSpace(request.GoalId))
            errors.Add("goal_id_required");
        if (string.IsNullOrWhiteSpace(request.RouteSourceDecisionId))
            errors.Add("route_source_decision_id_required");
        if (!IsLowerSha256(request.SupportingTransitionReceiptSha256))
            errors.Add("supporting_transition_receipt_sha256_invalid");
        if (string.IsNullOrWhiteSpace(request.MaterialReservationId) ||
            string.IsNullOrWhiteSpace(request.NodeId) ||
            request.SlotIndex < 0 ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            request.ConsumedQuantity <= 0 ||
            string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("supporting_transition_consumed_claim_invalid");
        }
        if (current is null)
        {
            errors.Add("reservation_portfolio_ledger_required");
            return;
        }
        var history = current.History ??
            Array.Empty<StrategyCommitmentHistoryEntry>();
        if (history.Count(row =>
                row.CommitmentId == request.PortfolioId &&
                row.SourceDecisionId == request.PortfolioId &&
                row.Operation == "reservation_portfolio_commit") != 1)
        {
            errors.Add("supporting_transition_portfolio_commit_not_found");
        }
        if (history.Any(row =>
                row.CommitmentId == request.PortfolioId &&
                row.Operation ==
                    "reservation_portfolio_supporting_transition_complete"))
        {
            errors.Add("supporting_transition_portfolio_already_settled");
        }
        var claims = current.MaterialReservations.Where(row =>
                row.ReservationId == request.MaterialReservationId)
            .ToArray();
        if (claims.Length != 1 ||
            claims[0].Status != StrategyCommitmentStatuses.Active ||
            claims[0].GoalId != request.GoalId ||
            claims[0].SourceDecisionId != request.RouteSourceDecisionId ||
            claims[0].NodeId != request.NodeId ||
            claims[0].SlotIndex != request.SlotIndex ||
            claims[0].QualifiedItemId != request.QualifiedItemId ||
            claims[0].Quantity != request.ConsumedQuantity)
        {
            errors.Add("supporting_transition_consumed_claim_mismatch");
        }
    }

    private static ReservationPortfolioSupportingTransitionSettlementResult
        SupportingTransitionSettlementRejected(
            StrategyCommitmentLedger? ledger,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            IEnumerable<string> errors) => new()
            {
                Accepted = false,
                PortfolioId = request.PortfolioId,
                RouteSourceDecisionId = request.RouteSourceDecisionId,
                SupportingTransitionReceiptSha256 =
                    request.SupportingTransitionReceiptSha256,
                CompletedMaterialReservationId =
                    request.MaterialReservationId,
                ConsumedQuantity = request.ConsumedQuantity,
                Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
                Ledger = ledger
            };
}
