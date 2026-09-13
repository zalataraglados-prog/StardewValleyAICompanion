using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private static ReservationClaimBuildResult BuildCurrencyClaims(
        AcquisitionRouteTargetDateCurrency route,
        string goalId,
        string stateHash,
        string decisionId,
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionShopQuoteSnapshotState currencies,
        int requiredAmount)
    {
        if (requiredAmount == 0)
            return ReservationClaimBuildResult.Empty;
        if (route.CurrencyEvaluation is null || requiredAmount < 0 ||
            !NativeShopCurrencies.TryGetKey(
                route.CurrencyEvaluation.CurrencyId,
                out var expectedKey) ||
            route.CurrencyEvaluation.CurrencyKey != expectedKey)
        {
            return ReservationClaimBuildResult.Blocked(
                "currency_reservation_requirement_contract_invalid");
        }

        var balances = new Dictionary<int, int>();
        var reasons = new List<string>();
        foreach (var definition in NativeShopCurrencies.All)
        {
            var lookup = currencies.CurrencyBalance(definition.Id);
            if (!lookup.EvidenceAvailable || !lookup.Balance.HasValue)
                reasons.AddRange(lookup.BlockingReasons);
            else
                balances[definition.Id] = lookup.Balance.Value;
        }
        if (reasons.Count > 0)
        {
            return ReservationClaimBuildResult.Blocked(
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        var otherReservations = ledgerState.Ledger.CurrencyReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.SourceDecisionId != decisionId)
            .ToArray();
        var supply = new NativeCurrencySupplyProjection().Project(
            balances,
            ledgerState.ActorPlayerId,
            otherReservations);
        if (supply.Status != "available")
        {
            return ReservationClaimBuildResult.Blocked(
                supply.BlockingReasons.Length > 0
                    ? supply.BlockingReasons
                    : new[] { "native_currency_supply_projection_blocked" });
        }
        var available = supply.Balances.Single(row =>
            row.CurrencyId == route.CurrencyEvaluation.CurrencyId);
        if (available.AvailableAmount < requiredAmount)
        {
            return ReservationClaimBuildResult.Conflict(
                "unreserved_currency_amount_unavailable:" + expectedKey);
        }
        var claim = new CurrencyReservationUpsertRequest
        {
            StateHash = stateHash,
            ExpectedLedgerRevision = ledgerState.Ledger.Revision,
            ReservationId = ReservationId(
                route.RouteOccurrenceId,
                "currency",
                0,
                0),
            SourceDecisionId = decisionId,
            GoalId = goalId,
            CurrencyId = route.CurrencyEvaluation.CurrencyId,
            Amount = requiredAmount,
            Purpose = "reserve target-date acquisition currency input"
        };
        return new ReservationClaimBuildResult(
            Array.Empty<MaterialReservationUpsertRequest>(),
            new[] { claim },
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
