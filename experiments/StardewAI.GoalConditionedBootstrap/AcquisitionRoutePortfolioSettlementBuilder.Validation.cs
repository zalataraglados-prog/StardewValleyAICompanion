using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSettlementBuilder
{
    private static string[] ValidateSettlement(
        SettlementContext context,
        ReservationPortfolioRouteSettlementRequest request,
        ReservationPortfolioRouteSettlementResult settlement,
        StrategyCommitmentLedger settled)
    {
        var reasons = new List<string>();
        if (!settlement.Accepted ||
            (settlement.Errors?.Length ?? 0) != 0 ||
            settlement.PortfolioId != request.PortfolioId ||
            settlement.RouteSourceDecisionId !=
                request.RouteSourceDecisionId ||
            settlement.FreshTerminalReceiptSha256 !=
                request.FreshTerminalReceiptSha256 ||
            settlement.CommittedLedgerRevision !=
                context.BaseLedger.Revision + 1 ||
            !SameSet(
                settlement.CompletedReservationIds ?? Array.Empty<string>(),
                request.ReservationIds) ||
            settlement.Ledger is null ||
            !EqualJson(settlement.Ledger, settled))
        {
            reasons.Add("route_settlement_result_mismatch");
        }
        if (settled.Revision != context.BaseLedger.Revision + 1)
            reasons.Add("route_settlement_revision_mismatch");
        var currentHistory = (settled.History ??
                Array.Empty<StrategyCommitmentHistoryEntry>())
            .Where(row => row.LedgerRevision == settled.Revision)
            .ToArray();
        var markers = currentHistory.Where(row =>
                row.CommitmentId == request.PortfolioId &&
                row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation == "reservation_portfolio_route_complete" &&
                row.Reason == request.FreshTerminalReceiptSha256)
            .ToArray();
        if (markers.Length != 1 ||
            currentHistory.Length != request.ReservationIds.Length + 1)
        {
            reasons.Add("route_settlement_marker_invalid");
        }
        foreach (var reservationId in request.ReservationIds)
        {
            var reservations = settled.MaterialReservations.Where(row =>
                    row.ReservationId == reservationId)
                .Select(row => (row.Status, row.CompletionReason,
                    row.CompletionEvidenceSha256, Operation:
                    "material_reservation_complete"))
                .Concat(settled.CurrencyReservations.Where(row =>
                        row.ReservationId == reservationId)
                    .Select(row => (row.Status, row.CompletionReason,
                        row.CompletionEvidenceSha256, Operation:
                        "currency_reservation_complete")))
                .ToArray();
            if (reservations.Length != 1 ||
                reservations[0].Status !=
                    StrategyCommitmentStatuses.Completed ||
                reservations[0].CompletionReason != request.Reason ||
                reservations[0].CompletionEvidenceSha256 !=
                    request.FreshTerminalReceiptSha256 ||
                currentHistory.Count(row =>
                    row.CommitmentId == reservationId &&
                    row.SourceDecisionId == request.RouteSourceDecisionId &&
                    row.Operation == reservations[0].Operation &&
                    row.Reason == request.Reason) != 1)
            {
                reasons.Add(
                    "route_settlement_completed_claim_invalid:" +
                    reservationId);
            }
        }
        if (settled.MaterialReservations.Any(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                row.SourceDecisionId == request.RouteSourceDecisionId) ||
            settled.CurrencyReservations.Any(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                row.SourceDecisionId == request.RouteSourceDecisionId))
        {
            reasons.Add("route_settlement_active_claim_leaked");
        }
        if (markers.Length == 1)
        {
            var replay = new ReservationPortfolioLedgerService()
                .SettleCompletedRoute(
                    context.BaseLedger,
                    context.AfterSnapshot,
                    request,
                    markers[0].RecordedAt);
            if (!replay.Accepted ||
                replay.Ledger is null ||
                !EqualJson(replay.Ledger, settled))
            {
                reasons.Add("route_settlement_exact_replay_mismatch");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SameSet(string[] left, string[] right) =>
        left.Order(StringComparer.Ordinal).SequenceEqual(
            right.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
}
