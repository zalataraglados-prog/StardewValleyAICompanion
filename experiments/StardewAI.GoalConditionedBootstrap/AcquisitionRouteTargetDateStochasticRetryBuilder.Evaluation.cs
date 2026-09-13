namespace StardewAI.GoalConditionedBootstrap;

using StardewAI.Core.Infrastructure;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    private const string DeterministicReceipt =
        "deterministic_fresh_receipt";
    private const string SourceResolvedDownstream =
        "source_resolved_downstream";
    private const string NativeRetryBound =
        "native_outcome_domain_and_retry_bound";

    private static AcquisitionRouteTargetDateStochasticRetry Evaluate(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute)
    {
        if (!route.ProcessingLeadTimeAxisResolved)
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_upstream_processing_lead_time_axis",
                false,
                null,
                "upstream_processing_lead_time",
                null,
                null,
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_processing_lead_time_axis_unresolved" });
        }
        if (route.ProcessingLeadTimeMatchesTargetDate is null)
        {
            return NotApplicable(
                route,
                staticRoute.UncertaintyMode,
                "not_applicable_upstream_processing_lead_time_axis");
        }
        if (route.ProcessingLeadTimeMatchesTargetDate == false)
        {
            return NotApplicable(
                route,
                staticRoute.UncertaintyMode,
                "not_applicable_upstream_processing_lead_time_miss");
        }

        if (staticRoute.UncertaintyMode is DeterministicReceipt or
            SourceResolvedDownstream)
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "resolved_stochastic_retry_not_required",
                true,
                true,
                "stochastic_retry_not_required",
                null,
                0,
                false,
                true,
                route.Evaluations.SelectMany(value => value.EvidencePaths)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        if (CurrentOutputAlreadyMaterialized(route, staticRoute))
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "resolved_current_output_already_materialized",
                true,
                true,
                "current_output_already_materialized",
                null,
                0,
                false,
                true,
                route.Evaluations.SelectMany(value => value.EvidencePaths)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        return Result(
            route,
            staticRoute.UncertaintyMode,
            "blocked_stochastic_probability_evidence",
            false,
            null,
            "native_retry_budget_requires_exact_probability",
            null,
            null,
            false,
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                "target_location_terminal_probability_evidence_missing:" +
                staticRoute.RouteKind
            });
    }

    private static bool CurrentOutputAlreadyMaterialized(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute) =>
        AcquisitionOutputProof.ReadyQuantity(
            route.Evaluations,
            staticRoute.MinimumQuality) >= staticRoute.RequiredAmount;

    private static AcquisitionRouteTargetDateStochasticRetry NotApplicable(
        AcquisitionRouteTargetDateProcessing route,
        string uncertaintyMode,
        string status) => Result(
            route,
            uncertaintyMode,
            status,
            true,
            null,
            "not_applicable",
            null,
            null,
            false,
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateStochasticRetry Result(
        AcquisitionRouteTargetDateProcessing route,
        string uncertaintyMode,
        string status,
        bool resolved,
        bool? matches,
        string budgetKind,
        double? singleAttemptProbability,
        int? requiredAttempts,
        bool expandsReservedConsumables,
        bool expandedReservationRevalidated,
        string[] evidencePaths,
        string[] nonMatchingReasons,
        string[] blockingReasons)
    {
        var requirement = RequirementRoute(route);
        return new AcquisitionRouteTargetDateStochasticRetry(
            route.RouteOccurrenceId,
            route,
            uncertaintyMode,
            status,
            resolved,
            matches,
            budgetKind,
            requirement.RequiredAmount,
            requirement.MinimumQuality,
            StochasticRetryPolicy.TargetSuccessProbability,
            singleAttemptProbability,
            requiredAttempts,
            requiredAttempts.HasValue
                ? Math.Max(0, requiredAttempts.Value - 1)
                : null,
            expandsReservedConsumables,
            expandedReservationRevalidated,
            evidencePaths,
            nonMatchingReasons,
            blockingReasons);
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateProcessing route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute;
}
