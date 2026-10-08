using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private const string DecisionPrefix = "target-date-acquisition-route:";

    internal static AcquisitionRouteTargetDateReservation Evaluate(
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
        var positiveInputs = ReservationMaterialInputs(
            route,
            out var inputReasons);
        if (inputReasons.Length > 0)
            return Blocked(route, inputReasons);
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

    private static AcquisitionResourceInputEvaluation[]
        ReservationMaterialInputs(
            AcquisitionRouteTargetDateCurrency route,
            out string[] blockingReasons)
    {
        var purchase = route.CurrencyEvaluation?.PurchasePrerequisite;
        if (purchase is null)
        {
            blockingReasons = Array.Empty<string>();
            return route.UpstreamRoute.InputEvaluations
                .Where(row => row.RequiredQuantity > 0)
                .ToArray();
        }

        var matchingInputs = route.UpstreamRoute.InputEvaluations.Where(input =>
                input.InputKind == purchase.InputKind &&
                input.QualifiedItemId == purchase.QualifiedItemId &&
                input.Status == "resolved_resource_input_miss" &&
                input.AvailableQuantity == purchase.CurrentAvailableQuantity &&
                input.RequiredQuantity - purchase.CurrentAvailableQuantity ==
                    purchase.RemainingRequiredQuantity)
            .ToArray();
        if (route.CurrencyRequirementKind !=
                "current_native_machine_input_purchase_quote" ||
            route.UpstreamRoute.ResourceInputsMatchTargetDate != false ||
            matchingInputs.Length != 1 ||
            purchase.RequiredPurchaseCount <= 0 ||
            purchase.OutputStackPerPurchase <= 0 ||
            purchase.UnitPrice < 0 ||
            route.CurrencyEvaluation?.RequiredAmount !=
                AcquisitionQuantityMath.Multiply(
                    purchase.UnitPrice,
                    purchase.RequiredPurchaseCount))
        {
            blockingReasons = new[]
            {
                "machine_input_purchase_reservation_binding_invalid"
            };
            return Array.Empty<AcquisitionResourceInputEvaluation>();
        }

        var purchasedInput = matchingInputs[0];
        var unresolvedOtherInputs = route.UpstreamRoute.InputEvaluations
            .Where(input => input.RequiredQuantity > 0 &&
                input != purchasedInput &&
                (input.Status != "resolved_resource_input_match" ||
                 !input.AvailableQuantity.HasValue ||
                 input.AvailableQuantity.Value < input.RequiredQuantity))
            .ToArray();
        if (unresolvedOtherInputs.Length > 0)
        {
            blockingReasons = unresolvedOtherInputs
                .Select(input =>
                    "machine_input_purchase_other_input_unresolved:" +
                    input.InputKind + ":" + input.QualifiedItemId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Array.Empty<AcquisitionResourceInputEvaluation>();
        }

        blockingReasons = Array.Empty<string>();
        return route.UpstreamRoute.InputEvaluations
            .Select(input => input with
            {
                RequiredQuantity = input == purchasedInput
                    ? Math.Min(
                        input.RequiredQuantity,
                        Math.Max(0, input.AvailableQuantity ?? 0))
                    : input.RequiredQuantity
            })
            .Where(input => input.RequiredQuantity > 0)
            .ToArray();
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
