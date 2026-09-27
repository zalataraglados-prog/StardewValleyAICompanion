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
        var consumptions = MaterialConsumptions(request, errors);
        ValidateSupportingTransitionSettlementRequest(
            current,
            request,
            consumptions,
            errors);
        if (errors.Count > 0)
            return SupportingTransitionSettlementRejected(
                current,
                request,
                consumptions,
                errors);

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        var byReservation = consumptions.ToDictionary(
            value => value.MaterialReservationId,
            StringComparer.Ordinal);
        ledger.MaterialReservations = ledger.MaterialReservations.Select(row =>
        {
            if (!byReservation.TryGetValue(
                    row.ReservationId,
                    out var consumption))
            {
                return row;
            }
            var settled = StrategyCommitmentLedgerSupport.CloneMaterial(row);
            settled.Revision++;
            if (consumption.ConsumedQuantity == row.Quantity)
            {
                settled.Status = StrategyCommitmentStatuses.Completed;
                settled.CompletionReason = request.Reason;
                settled.CompletionEvidenceSha256 =
                    request.SupportingTransitionReceiptSha256;
            }
            else
            {
                settled.Quantity -= consumption.ConsumedQuantity;
                settled.Status = StrategyCommitmentStatuses.Active;
                settled.CompletionReason = string.Empty;
                settled.CompletionEvidenceSha256 = string.Empty;
            }
            return settled;
        }).ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        var settlements = consumptions.Select(consumption =>
        {
            var settled = ledger.MaterialReservations.Single(row =>
                row.ReservationId == consumption.MaterialReservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                settled.ReservationId,
                settled.Revision,
                settled.SourceDecisionId,
                settled.Status == StrategyCommitmentStatuses.Completed
                    ? "material_reservation_consumed"
                    : "material_reservation_partially_consumed",
                updatedAt,
                request.Reason);
            return new ReservationPortfolioMaterialSettlement
            {
                MaterialReservationId = settled.ReservationId,
                ConsumedQuantity = consumption.ConsumedQuantity,
                RemainingQuantity =
                    settled.Status == StrategyCommitmentStatuses.Completed
                        ? 0
                        : settled.Quantity,
                ReservationStatus = settled.Status
            };
        }).ToArray();
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
            CompletedMaterialReservationId = settlements.Length == 1 &&
                settlements[0].ReservationStatus ==
                    StrategyCommitmentStatuses.Completed
                    ? settlements[0].MaterialReservationId
                    : string.Empty,
            CompletedMaterialReservationIds = settlements.Where(value =>
                    value.ReservationStatus ==
                        StrategyCommitmentStatuses.Completed)
                .Select(value => value.MaterialReservationId)
                .ToArray(),
            ActiveMaterialReservationIds = settlements.Where(value =>
                    value.ReservationStatus ==
                        StrategyCommitmentStatuses.Active)
                .Select(value => value.MaterialReservationId)
                .ToArray(),
            ConsumedQuantity = settlements.Sum(value =>
                value.ConsumedQuantity),
            MaterialSettlements = settlements,
            Ledger = ledger
        };
    }

    private static void ValidateSupportingTransitionSettlementRequest(
        StrategyCommitmentLedger? current,
        ReservationPortfolioSupportingTransitionSettlementRequest request,
        ReservationPortfolioMaterialConsumption[] consumptions,
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
        if (consumptions.Length == 0 ||
            consumptions.Any(value =>
                string.IsNullOrWhiteSpace(value.MaterialReservationId) ||
                string.IsNullOrWhiteSpace(value.NodeId) ||
                value.SlotIndex < 0 ||
                string.IsNullOrWhiteSpace(value.QualifiedItemId) ||
                value.ConsumedQuantity <= 0) ||
            consumptions.Select(value => value.MaterialReservationId)
                .Distinct(StringComparer.Ordinal).Count() !=
                consumptions.Length ||
            consumptions.Select(value => value.NodeId + ":" + value.SlotIndex)
                .Distinct(StringComparer.Ordinal).Count() !=
                consumptions.Length ||
            consumptions.Sum(value => (long)value.ConsumedQuantity) >
                int.MaxValue || string.IsNullOrWhiteSpace(request.Reason))
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
        foreach (var consumption in consumptions)
        {
            var claims = current.MaterialReservations.Where(row =>
                    row.ReservationId ==
                        consumption.MaterialReservationId)
                .ToArray();
            if (claims.Length != 1 ||
                claims[0].Status != StrategyCommitmentStatuses.Active ||
                claims[0].GoalId != request.GoalId ||
                claims[0].SourceDecisionId !=
                    request.RouteSourceDecisionId ||
                claims[0].NodeId != consumption.NodeId ||
                claims[0].SlotIndex != consumption.SlotIndex ||
                claims[0].QualifiedItemId !=
                    consumption.QualifiedItemId ||
                claims[0].Quantity < consumption.ConsumedQuantity)
            {
                errors.Add("supporting_transition_consumed_claim_mismatch");
            }
        }
    }

    private static ReservationPortfolioMaterialConsumption[]
        MaterialConsumptions(
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            ICollection<string> errors)
    {
        var values = request.MaterialConsumptions ??
            Array.Empty<ReservationPortfolioMaterialConsumption>();
        var legacyPresent =
            !string.IsNullOrWhiteSpace(request.MaterialReservationId) ||
            !string.IsNullOrWhiteSpace(request.NodeId) ||
            request.SlotIndex != 0 ||
            !string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            request.ConsumedQuantity != 0;
        if (values.Length > 0)
        {
            if (legacyPresent)
                errors.Add("supporting_transition_consumption_forms_mixed");
            return values;
        }
        return legacyPresent
            ? new[]
            {
                new ReservationPortfolioMaterialConsumption
                {
                    MaterialReservationId = request.MaterialReservationId,
                    NodeId = request.NodeId,
                    SlotIndex = request.SlotIndex,
                    QualifiedItemId = request.QualifiedItemId,
                    ConsumedQuantity = request.ConsumedQuantity
                }
            }
            : Array.Empty<ReservationPortfolioMaterialConsumption>();
    }

    private static ReservationPortfolioSupportingTransitionSettlementResult
        SupportingTransitionSettlementRejected(
            StrategyCommitmentLedger? ledger,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            ReservationPortfolioMaterialConsumption[] consumptions,
            IEnumerable<string> errors) => new()
            {
                Accepted = false,
                PortfolioId = request.PortfolioId,
                RouteSourceDecisionId = request.RouteSourceDecisionId,
                SupportingTransitionReceiptSha256 =
                    request.SupportingTransitionReceiptSha256,
                CompletedMaterialReservationId =
                    consumptions.Length == 1
                        ? consumptions[0].MaterialReservationId
                        : string.Empty,
                ConsumedQuantity = SafeConsumedQuantity(consumptions),
                Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
                Ledger = ledger
            };

    private static int SafeConsumedQuantity(
        IEnumerable<ReservationPortfolioMaterialConsumption> consumptions)
    {
        var total = consumptions.Sum(value =>
            (long)value.ConsumedQuantity);
        return total is >= 0 and <= int.MaxValue ? (int)total : 0;
    }
}
