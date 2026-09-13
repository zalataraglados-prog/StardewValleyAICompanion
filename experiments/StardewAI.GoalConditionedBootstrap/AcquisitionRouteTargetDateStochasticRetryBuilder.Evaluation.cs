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
    private const string NativeLocationFishSpawn =
        "native_location_fish_spawn";

    private static AcquisitionRouteTargetDateStochasticRetry Evaluate(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionRouteTargetDateFishingProbability fishingRoute,
        string fishingProbabilityPath)
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

        if (staticRoute.RouteKind == NativeLocationFishSpawn)
        {
            return EvaluateFishing(
                route,
                staticRoute,
                fishingRoute,
                fishingProbabilityPath);
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

    private static AcquisitionRouteTargetDateStochasticRetry EvaluateFishing(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionRouteTargetDateFishingProbability fishingRoute,
        string fishingProbabilityPath)
    {
        if (!fishingRoute.ProbabilityAxisResolved ||
            !fishingRoute.SingleAttemptProbabilityLowerBound.HasValue)
        {
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
                new[] { fishingProbabilityPath },
                Array.Empty<string>(),
                fishingRoute.BlockingReasons.Length > 0
                    ? fishingRoute.BlockingReasons
                    : new[] { "fishing_terminal_probability_unresolved" });
        }

        var probability = fishingRoute.SingleAttemptProbabilityLowerBound.Value;
        if (fishingRoute.IndependentRetryLowerBoundProven != true)
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_stochastic_probability_evidence",
                false,
                null,
                "native_retry_budget_requires_independence",
                probability,
                null,
                false,
                false,
                new[] { fishingProbabilityPath },
                Array.Empty<string>(),
                fishingRoute.RetryBlockingReasons.Length > 0
                    ? fishingRoute.RetryBlockingReasons
                    : new[] { "fishing_independent_retry_proof_missing" });
        }

        if (probability <= 0d)
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "resolved_stochastic_probability_zero",
                true,
                false,
                "independent_retry_probability_zero",
                probability,
                null,
                false,
                true,
                new[] { fishingProbabilityPath },
                new[] { "single_attempt_probability_lower_bound_zero" },
                Array.Empty<string>());
        }

        var requiredAttempts =
            StochasticRetryPolicy.RequiredIndependentAttemptCount(
                staticRoute.RequiredAmount,
                probability);
        if (!requiredAttempts.HasValue)
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_stochastic_probability_evidence",
                false,
                null,
                "independent_retry_budget_exceeds_policy_limit",
                probability,
                null,
                false,
                false,
                new[] { fishingProbabilityPath },
                Array.Empty<string>(),
                new[] { "independent_retry_attempt_limit_exceeded" });
        }

        return Result(
            route,
            staticRoute.UncertaintyMode,
            "resolved_independent_stochastic_retry_budget",
            true,
            true,
            "independent_binomial_retry_budget",
            probability,
            requiredAttempts.Value,
            false,
            true,
            new[] { fishingProbabilityPath },
            Array.Empty<string>(),
            Array.Empty<string>());
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
                ? Math.Max(
                    0,
                    requiredAttempts.Value - requirement.RequiredAmount)
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
