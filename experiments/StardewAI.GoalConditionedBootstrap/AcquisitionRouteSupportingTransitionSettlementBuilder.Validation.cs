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
        var expectedSettlements = request.MaterialConsumptions.Select(
                consumption =>
                {
                    var before = context.BaseLedger.MaterialReservations.Single(
                        row => row.ReservationId ==
                            consumption.MaterialReservationId);
                    var remaining = before.Quantity -
                        consumption.ConsumedQuantity;
                    return new ReservationPortfolioMaterialSettlement
                    {
                        MaterialReservationId =
                            consumption.MaterialReservationId,
                        ConsumedQuantity = consumption.ConsumedQuantity,
                        RemainingQuantity = remaining,
                        ReservationStatus = remaining == 0
                            ? StrategyCommitmentStatuses.Completed
                            : StrategyCommitmentStatuses.Active
                    };
                })
            .ToArray();
        var completedIds = expectedSettlements.Where(value =>
                value.ReservationStatus ==
                    StrategyCommitmentStatuses.Completed)
            .Select(value => value.MaterialReservationId)
            .ToArray();
        var activeIds = expectedSettlements.Where(value =>
                value.ReservationStatus == StrategyCommitmentStatuses.Active)
            .Select(value => value.MaterialReservationId)
            .ToArray();
        if (!result.Accepted ||
            (result.Errors?.Length ?? 0) != 0 ||
            result.PortfolioId != request.PortfolioId ||
            result.RouteSourceDecisionId != request.RouteSourceDecisionId ||
            result.SupportingTransitionReceiptSha256 !=
                request.SupportingTransitionReceiptSha256 ||
            result.CommittedLedgerRevision != context.BaseLedger.Revision + 1 ||
            result.CompletedMaterialReservationId !=
                (expectedSettlements.Length == 1 && completedIds.Length == 1
                    ? completedIds[0]
                    : string.Empty) ||
            result.CompletedMaterialReservationIds is null ||
            !result.CompletedMaterialReservationIds.SequenceEqual(
                completedIds, StringComparer.Ordinal) ||
            result.ActiveMaterialReservationIds is null ||
            !result.ActiveMaterialReservationIds.SequenceEqual(
                activeIds, StringComparer.Ordinal) ||
            result.ConsumedQuantity != request.MaterialConsumptions.Sum(
                value => value.ConsumedQuantity) ||
            result.MaterialSettlements is null ||
            !EqualJson(result.MaterialSettlements, expectedSettlements) ||
            result.Ledger is null ||
            !EqualJson(result.Ledger, settled))
        {
            reasons.Add("support_settlement_result_mismatch");
        }
        if (settled.Revision != context.BaseLedger.Revision + 1)
            reasons.Add("support_settlement_revision_mismatch");
        foreach (var expected in expectedSettlements)
        {
            var before = context.BaseLedger.MaterialReservations.Single(
                row => row.ReservationId == expected.MaterialReservationId);
            var rows = settled.MaterialReservations.Where(row =>
                    row.ReservationId == expected.MaterialReservationId &&
                    row.Status == expected.ReservationStatus &&
                    row.Quantity == (expected.RemainingQuantity == 0
                        ? before.Quantity
                        : expected.RemainingQuantity) &&
                    row.Revision == before.Revision + 1 &&
                    (expected.ReservationStatus ==
                        StrategyCommitmentStatuses.Completed
                            ? row.CompletionReason == request.Reason &&
                              row.CompletionEvidenceSha256 ==
                                  request.SupportingTransitionReceiptSha256
                            : string.IsNullOrEmpty(row.CompletionReason) &&
                              string.IsNullOrEmpty(
                                  row.CompletionEvidenceSha256)))
                .ToArray();
            if (rows.Length != 1)
                reasons.Add("support_settlement_consumed_claim_mismatch");
        }
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
        var materialHistoryMatches = expectedSettlements.All(expected =>
            currentHistory.Count(row =>
                row.CommitmentId == expected.MaterialReservationId &&
                row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation == (expected.ReservationStatus ==
                    StrategyCommitmentStatuses.Completed
                        ? "material_reservation_consumed"
                        : "material_reservation_partially_consumed") &&
                row.Reason == request.Reason) == 1);
        if (markers.Length != 1 ||
            currentHistory.Length != expectedSettlements.Length + 1 ||
            !materialHistoryMatches)
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
