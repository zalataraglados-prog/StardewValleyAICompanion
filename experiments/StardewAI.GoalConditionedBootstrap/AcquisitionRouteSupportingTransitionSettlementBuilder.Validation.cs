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
            .Concat(request.MaterialRelocations.Select(value =>
                value.MaterialReservationId))
            .ToArray();
        var relocatedIds = request.MaterialRelocations.Select(value =>
                value.MaterialReservationId)
            .ToArray();
        var expectedCurrencySettlements = request.CurrencyConsumptions.Select(
                consumption =>
                {
                    var before = context.BaseLedger.CurrencyReservations.Single(
                        row => row.ReservationId ==
                            consumption.CurrencyReservationId);
                    var remaining = before.Amount - consumption.ConsumedAmount;
                    return new ReservationPortfolioCurrencySettlement
                    {
                        CurrencyReservationId =
                            consumption.CurrencyReservationId,
                        ConsumedAmount = consumption.ConsumedAmount,
                        RemainingAmount = remaining,
                        ReservationStatus = remaining == 0
                            ? StrategyCommitmentStatuses.Completed
                            : StrategyCommitmentStatuses.Active
                    };
                })
            .ToArray();
        var completedCurrencyIds = expectedCurrencySettlements.Where(value =>
                value.ReservationStatus ==
                    StrategyCommitmentStatuses.Completed)
            .Select(value => value.CurrencyReservationId)
            .ToArray();
        var activeCurrencyIds = expectedCurrencySettlements.Where(value =>
                value.ReservationStatus == StrategyCommitmentStatuses.Active)
            .Select(value => value.CurrencyReservationId)
            .ToArray();
        var expectedMachineIntentId =
            context.MachineSupportIntent?.IntentId ?? string.Empty;
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
            result.RelocatedMaterialReservationIds is null ||
            !result.RelocatedMaterialReservationIds.SequenceEqual(
                relocatedIds, StringComparer.Ordinal) ||
            result.ConsumedQuantity != request.MaterialConsumptions.Sum(
                value => value.ConsumedQuantity) ||
            result.MaterialSettlements is null ||
            !EqualJson(result.MaterialSettlements, expectedSettlements) ||
            result.MaterialRelocations is null ||
            !EqualJson(
                result.MaterialRelocations,
                request.MaterialRelocations) ||
            result.CompletedCurrencyReservationIds is null ||
            !result.CompletedCurrencyReservationIds.SequenceEqual(
                completedCurrencyIds,
                StringComparer.Ordinal) ||
            result.ActiveCurrencyReservationIds is null ||
            !result.ActiveCurrencyReservationIds.SequenceEqual(
                activeCurrencyIds,
                StringComparer.Ordinal) ||
            result.CurrencySettlements is null ||
            !EqualJson(
                result.CurrencySettlements,
                expectedCurrencySettlements) ||
            result.ReboundActiveReservationIds is null ||
            !result.ReboundActiveReservationIds.SequenceEqual(
                request.RebindActiveReservationIds,
                StringComparer.Ordinal) ||
            result.ReboundMachineSupportIntentId !=
                expectedMachineIntentId ||
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
        foreach (var relocation in request.MaterialRelocations)
        {
            var before = context.BaseLedger.MaterialReservations.Single(
                row => row.ReservationId ==
                    relocation.MaterialReservationId);
            var rows = settled.MaterialReservations.Where(row =>
                    row.ReservationId == relocation.MaterialReservationId &&
                    row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceStateHash == context.AfterSnapshot.StateHash &&
                    row.NodeId == relocation.DestinationNodeId &&
                    row.SlotIndex == relocation.DestinationSlotIndex &&
                    row.QualifiedItemId == relocation.QualifiedItemId &&
                    row.Quantity == relocation.Quantity &&
                    row.Revision == before.Revision + 1 &&
                    string.IsNullOrEmpty(row.CompletionReason) &&
                    string.IsNullOrEmpty(
                        row.CompletionEvidenceSha256))
                .ToArray();
            if (rows.Length != 1)
                reasons.Add("support_settlement_relocated_claim_mismatch");
        }
        foreach (var expected in expectedCurrencySettlements)
        {
            var before = context.BaseLedger.CurrencyReservations.Single(row =>
                row.ReservationId == expected.CurrencyReservationId);
            var rows = settled.CurrencyReservations.Where(row =>
                    row.ReservationId == expected.CurrencyReservationId &&
                    row.Status == expected.ReservationStatus &&
                    row.Amount == (expected.RemainingAmount == 0
                        ? before.Amount
                        : expected.RemainingAmount) &&
                    row.SourceStateHash == context.AfterSnapshot.StateHash &&
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
                reasons.Add("support_settlement_currency_claim_mismatch");
        }
        var mutatedIds = request.MaterialConsumptions.Select(value =>
                value.MaterialReservationId)
            .Concat(request.MaterialRelocations.Select(value =>
                value.MaterialReservationId))
            .Concat(request.CurrencyConsumptions.Select(value =>
                value.CurrencyReservationId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var reservationId in request.RebindActiveReservationIds
                     .Where(value => !mutatedIds.Contains(value)))
        {
            var beforeMaterial = context.BaseLedger.MaterialReservations
                .SingleOrDefault(row => row.ReservationId == reservationId);
            var beforeCurrency = context.BaseLedger.CurrencyReservations
                .SingleOrDefault(row => row.ReservationId == reservationId);
            var rebound = beforeMaterial is not null
                ? settled.MaterialReservations.Count(row =>
                    row.ReservationId == reservationId &&
                    row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceStateHash == context.AfterSnapshot.StateHash &&
                    row.Revision == beforeMaterial.Revision + 1)
                : settled.CurrencyReservations.Count(row =>
                    row.ReservationId == reservationId &&
                    row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceStateHash == context.AfterSnapshot.StateHash &&
                    row.Revision == beforeCurrency!.Revision + 1);
            if (rebound != 1)
                reasons.Add("support_settlement_rebound_claim_mismatch");
        }
        if (context.MachineSupportIntent is not null)
        {
            var expectedIntent = JsonSerializer.Deserialize<
                MachineSupportIntent>(JsonSerializer.Serialize(
                    context.MachineSupportIntent))!;
            expectedIntent.Revision++;
            expectedIntent.SourceStateHash =
                context.AfterSnapshot.StateHash;
            var rebound = settled.MachineSupportIntents.Where(row =>
                    row.IntentId == expectedIntent.IntentId &&
                    EqualJson(row, expectedIntent))
                .ToArray();
            if (rebound.Length != 1)
            {
                reasons.Add(
                    "support_settlement_machine_intent_rebind_mismatch");
            }
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
        var mutationHistoryMatches = expectedSettlements.All(expected =>
            currentHistory.Count(row =>
                row.CommitmentId == expected.MaterialReservationId &&
                row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation == (expected.ReservationStatus ==
                    StrategyCommitmentStatuses.Completed
                        ? "material_reservation_consumed"
                        : "material_reservation_partially_consumed") &&
                row.Reason == request.Reason) == 1) &&
            request.MaterialRelocations.All(relocation =>
                currentHistory.Count(row =>
                    row.CommitmentId ==
                        relocation.MaterialReservationId &&
                    row.SourceDecisionId ==
                        request.RouteSourceDecisionId &&
                    row.Operation == "material_reservation_relocated" &&
                    row.Reason == request.Reason) == 1) &&
            expectedCurrencySettlements.All(expected =>
                currentHistory.Count(row =>
                    row.CommitmentId == expected.CurrencyReservationId &&
                    row.SourceDecisionId ==
                        request.RouteSourceDecisionId &&
                    row.Operation == (expected.ReservationStatus ==
                        StrategyCommitmentStatuses.Completed
                            ? "currency_reservation_consumed"
                            : "currency_reservation_partially_consumed") &&
                    row.Reason == request.Reason) == 1) &&
            request.RebindActiveReservationIds
                .Where(value => !mutatedIds.Contains(value))
                .All(reservationId => currentHistory.Count(row =>
                    row.CommitmentId == reservationId &&
                    row.SourceDecisionId ==
                        request.RouteSourceDecisionId &&
                    row.Operation ==
                        "reservation_rebound_after_supporting_transition" &&
                    row.Reason == request.Reason) == 1) &&
            (context.MachineSupportIntent is null ||
             currentHistory.Count(row =>
                 row.CommitmentId ==
                    context.MachineSupportIntent.IntentId &&
                 row.SourceDecisionId ==
                    request.RouteSourceDecisionId &&
                 row.Operation ==
                    "machine_support_intent_rebound_after_supporting_transition" &&
                 row.Reason == request.Reason) == 1);
        var reboundOnlyCount = request.RebindActiveReservationIds.Count(value =>
            !mutatedIds.Contains(value));
        if (markers.Length != 1 ||
            currentHistory.Length != expectedSettlements.Length +
                request.MaterialRelocations.Length +
                expectedCurrencySettlements.Length +
                reboundOnlyCount +
                (context.MachineSupportIntent is null ? 0 : 1) + 1 ||
            !mutationHistoryMatches)
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
