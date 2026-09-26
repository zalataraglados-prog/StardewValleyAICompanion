using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionSettlementBuilder
{
    private static string[] ValidateSettlement(
        SettlementContext context,
        ReservationPortfolioSupportingTransitionSettlementRequest request,
        ReservationPortfolioSupportingTransitionSettlementResult result,
        StrategyCommitmentLedger settled)
    {
        var reasons = new List<string>();
        if (!EqualJson(request, CanonicalRequest(context)))
            reasons.Add("support_settlement_request_mismatch");
        if (!result.Accepted ||
            (result.Errors?.Length ?? 0) != 0 ||
            result.PortfolioId != request.PortfolioId ||
            result.RouteSourceDecisionId != request.RouteSourceDecisionId ||
            result.SupportingTransitionReceiptSha256 !=
                request.SupportingTransitionReceiptSha256 ||
            result.CommittedLedgerRevision != context.BaseLedger.Revision + 1 ||
            result.CompletedMaterialReservationId !=
                request.MaterialReservationId ||
            result.ConsumedQuantity != request.ConsumedQuantity ||
            result.Ledger is null ||
            !EqualJson(result.Ledger, settled))
        {
            reasons.Add("support_settlement_result_mismatch");
        }
        if (settled.Revision != context.BaseLedger.Revision + 1)
            reasons.Add("support_settlement_revision_mismatch");
        var consumed = settled.MaterialReservations.Where(row =>
                row.ReservationId == request.MaterialReservationId &&
                row.Status == StrategyCommitmentStatuses.Completed &&
                row.Revision == context.BaseLedger.MaterialReservations.Single(
                    baseRow => baseRow.ReservationId ==
                        request.MaterialReservationId).Revision + 1 &&
                row.CompletionReason == request.Reason &&
                row.CompletionEvidenceSha256 ==
                    request.SupportingTransitionReceiptSha256)
            .ToArray();
        if (consumed.Length != 1)
            reasons.Add("support_settlement_consumed_claim_mismatch");
        var currentHistory = settled.History.Where(row =>
                row.LedgerRevision == settled.Revision)
            .ToArray();
        var markers = currentHistory.Where(row =>
                row.CommitmentId == request.PortfolioId &&
                row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation ==
                    "reservation_portfolio_supporting_transition_complete" &&
                row.Reason == request.SupportingTransitionReceiptSha256)
            .ToArray();
        if (markers.Length != 1 || currentHistory.Length != 2 ||
            !currentHistory.Any(row =>
                row.CommitmentId == request.MaterialReservationId &&
                row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation == "material_reservation_consumed" &&
                row.Reason == request.Reason))
        {
            reasons.Add("support_settlement_history_mismatch");
        }
        if (settled.History.Any(row =>
                row.CommitmentId == request.PortfolioId &&
                row.Operation == "reservation_portfolio_route_complete"))
        {
            reasons.Add("support_settlement_recorded_terminal_completion");
        }
        if (markers.Length == 1)
        {
            var replay = new ReservationPortfolioLedgerService()
                .SettleSupportingTransition(
                    context.BaseLedger,
                    context.AfterSnapshot,
                    request,
                    markers[0].RecordedAt);
            if (!replay.Accepted || replay.Ledger is null ||
                !EqualJson(replay, result) ||
                !EqualJson(replay.Ledger, settled))
            {
                reasons.Add("support_settlement_exact_replay_mismatch");
            }
        }
        else
        {
            reasons.Add("support_settlement_exact_replay_mismatch");
        }
        return reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }


}
