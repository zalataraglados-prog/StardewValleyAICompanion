using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private const string DecisionPrefix = "target-date-acquisition-route:";

    private static AcquisitionRouteTargetDateReservation Evaluate(
        AcquisitionRouteTargetDateCurrency route,
        string goalId,
        string stateHash,
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionResourceInputSnapshotState resources,
        AcquisitionShopQuoteSnapshotState currencies)
    {
        if (!route.CurrencyAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_currency_budget_axis",
                false,
                null,
                "upstream_blocked",
                null,
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_currency_budget_axis_unresolved" });
        }
        if (route.CurrencyBudgetMatchesTargetDate is null)
        {
            return Result(
                route,
                "not_applicable_upstream_currency_budget_axis",
                true,
                null,
                "not_applicable",
                null,
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (route.CurrencyBudgetMatchesTargetDate == false)
        {
            return Result(
                route,
                "not_applicable_upstream_currency_budget_miss",
                true,
                null,
                "not_applicable",
                null,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var decisionId = DecisionPrefix + route.RouteOccurrenceId;
        return EvaluateMatched(
            route,
            goalId,
            stateHash,
            decisionId,
            ledgerState,
            resources,
            currencies);
    }

    private static AcquisitionRouteTargetDateReservation EvaluateMatched(
        AcquisitionRouteTargetDateCurrency route,
        string goalId,
        string stateHash,
        string decisionId,
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionResourceInputSnapshotState resources,
        AcquisitionShopQuoteSnapshotState currencies)
    {
        var existingMaterial = ActiveForDecision(
            ledgerState.Ledger.MaterialReservations,
            decisionId);
        var existingCurrency = ActiveForDecision(
            ledgerState.Ledger.CurrencyReservations,
            decisionId);
        var positiveInputs = route.UpstreamRoute.InputEvaluations
            .Where(row => row.RequiredQuantity > 0)
            .ToArray();
        var requiredCurrency = route.CurrencyEvaluation?.RequiredAmount ?? 0;
        if (positiveInputs.Length == 0 && requiredCurrency == 0)
        {
            if (existingMaterial.Length == 0 && existingCurrency.Length == 0)
                return NotRequired(route);
            return Available(
                route,
                ClaimSet(
                    stateHash,
                    ledgerState.Ledger.Revision,
                    decisionId,
                    existingMaterial,
                    existingCurrency,
                    Array.Empty<MaterialReservationUpsertRequest>(),
                    Array.Empty<CurrencyReservationUpsertRequest>(),
                    replacementRequired: true),
                "claim_replacement_required");
        }

        var claimResult = BuildClaims(
            route,
            goalId,
            stateHash,
            decisionId,
            ledgerState,
            resources,
            currencies,
            positiveInputs,
            requiredCurrency);
        if (claimResult.BlockingReasons.Length > 0)
            return Blocked(route, claimResult.BlockingReasons);
        if (claimResult.NonMatchingReasons.Length > 0)
            return Conflict(route, claimResult.NonMatchingReasons);

        var exact = ExactMaterialClaims(
                existingMaterial,
                claimResult.MaterialClaims) &&
            ExactCurrencyClaims(
                existingCurrency,
                claimResult.CurrencyClaims);
        var existingCount = existingMaterial.Length + existingCurrency.Length;
        var disposition = existingCount == 0
            ? "claim_proposed"
            : exact
                ? "claim_already_committed"
                : "claim_replacement_required";
        return Available(
            route,
            ClaimSet(
                stateHash,
                ledgerState.Ledger.Revision,
                decisionId,
                existingMaterial,
                existingCurrency,
                claimResult.MaterialClaims,
                claimResult.CurrencyClaims,
                disposition == "claim_replacement_required"),
            disposition);
    }

    private static T[] ActiveForDecision<T>(
        IEnumerable<T> rows,
        string decisionId) where T : class => rows.Where(row => row switch
        {
            MaterialReservation material =>
                material.Status == StrategyCommitmentStatuses.Active &&
                material.SourceDecisionId == decisionId,
            CurrencyReservation currency =>
                currency.Status == StrategyCommitmentStatuses.Active &&
                currency.SourceDecisionId == decisionId,
            _ => false
        }).ToArray();
}
