namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateCurrencyBuilder
{
    private static AcquisitionRouteTargetDateCurrency NotRequired(
        AcquisitionRouteTargetDateResource route) => Result(
        route,
        "resolved_currency_not_required",
        true,
        true,
        NoCurrency,
        null,
        Array.Empty<string>(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateCurrency ResolvedMatch(
        AcquisitionRouteTargetDateResource route,
        string requirementKind,
        AcquisitionCurrencyEvaluation evaluation) => Result(
        route,
        "resolved_currency_budget_match",
        true,
        true,
        requirementKind,
        evaluation,
        Array.Empty<string>(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateCurrency ResolvedMiss(
        AcquisitionRouteTargetDateResource route,
        string requirementKind,
        AcquisitionCurrencyEvaluation evaluation,
        params string[] reasons) => Result(
        route,
        "resolved_currency_budget_miss",
        true,
        false,
        requirementKind,
        evaluation,
        reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateCurrency Blocked(
        AcquisitionRouteTargetDateResource route,
        string requirementKind,
        params string[] reasons) => Result(
        route,
        "blocked_currency_budget_evidence",
        false,
        null,
        requirementKind,
        null,
        Array.Empty<string>(),
        reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());

    private static AcquisitionRouteTargetDateCurrency Result(
        AcquisitionRouteTargetDateResource route,
        string status,
        bool resolved,
        bool? matches,
        string requirementKind,
        AcquisitionCurrencyEvaluation? evaluation,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            status,
            resolved,
            matches,
            requirementKind,
            evaluation,
            nonMatchingReasons,
            blockingReasons);
}
