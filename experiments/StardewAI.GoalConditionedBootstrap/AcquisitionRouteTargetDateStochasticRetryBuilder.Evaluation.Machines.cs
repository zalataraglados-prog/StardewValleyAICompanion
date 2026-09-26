namespace StardewAI.GoalConditionedBootstrap;

using StardewAI.Core.Infrastructure;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    private static readonly HashSet<string> MachineRouteKinds = new(
        new[]
        {
            "machine_output",
            "native_machine_flavored_output",
            "native_machine_item_query_output"
        },
        StringComparer.Ordinal);

    internal static AcquisitionRouteTargetDateStochasticRetry EvaluateMachine(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute,
        MachineRetryExpansionContext? expansionContext = null)
    {
        var creditedExistingOutput =
            MachineCreditedExistingOutputQuantity(route);
        var baselineAttempts = MachineBaselineAttemptCount(route);
        if (creditedExistingOutput < 0 ||
            creditedExistingOutput > staticRoute.RequiredAmount)
        {
            return MachineProbabilityBlocked(
                route,
                staticRoute,
                baselineAttempts,
                "machine_existing_output_credit_out_of_range:" +
                creditedExistingOutput + ":" + staticRoute.RequiredAmount);
        }
        if (!baselineAttempts.HasValue || baselineAttempts < 0)
        {
            return MachineProbabilityBlocked(
                route,
                staticRoute,
                baselineAttempts,
                "machine_processing_baseline_attempt_binding_missing");
        }
        if (MachineOutputSelectionAlreadyResolved(route, staticRoute))
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "resolved_current_machine_output_selection",
                true,
                true,
                "current_machine_output_selection_already_resolved",
                1d,
                baselineAttempts,
                false,
                true,
                MachineEvidencePaths(route),
                Array.Empty<string>(),
                Array.Empty<string>(),
                baselineAttempts);
        }

        if (baselineAttempts <= 0)
        {
            return MachineProbabilityBlocked(
                route,
                staticRoute,
                null,
                "machine_processing_baseline_attempt_binding_missing");
        }
        if (!TryMachineSingleAttemptProbability(
                staticRoute,
                out var probability,
                out var probabilityBlockingReason))
        {
            return MachineProbabilityBlocked(
                route,
                staticRoute,
                baselineAttempts,
                probabilityBlockingReason);
        }

        var source = staticRoute.MachineSource!;
        var minimumOutputPerSuccess = Math.Max(1, source.MinimumStack);
        var remainingRequiredQuantity = Math.Max(
            0,
            staticRoute.RequiredAmount - creditedExistingOutput);
        var requiredSuccesses = AcquisitionQuantityMath.DivideRoundUp(
            remainingRequiredQuantity,
            minimumOutputPerSuccess);
        if (requiredSuccesses != baselineAttempts.Value)
        {
            return MachineProbabilityBlocked(
                route,
                staticRoute,
                baselineAttempts,
                "machine_processing_baseline_attempt_count_drifted:" +
                baselineAttempts.Value + ":" + requiredSuccesses);
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
                MachineEvidencePaths(route),
                new[] { "machine_single_attempt_probability_zero" },
                Array.Empty<string>(),
                baselineAttempts);
        }

        var requiredAttempts =
            StochasticRetryPolicy.RequiredIndependentAttemptCount(
                requiredSuccesses,
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
                MachineEvidencePaths(route),
                Array.Empty<string>(),
                new[] { "independent_retry_attempt_limit_exceeded" },
                baselineAttempts);
        }

        var expandsReservation = requiredAttempts.Value >
            baselineAttempts.Value;
        if (expandsReservation)
        {
            var expansionReasons = new[]
            {
                "machine_retry_expansion_context_missing"
            };
            if (expansionContext is not null)
            {
                if (TryExpandMachineRetryRoute(
                        route,
                        staticRoute,
                        requiredAttempts.Value,
                        creditedExistingOutput,
                        expansionContext,
                        out var expandedRoute,
                        out expansionReasons))
                {
                    return Result(
                        expandedRoute,
                        staticRoute.UncertaintyMode,
                        "resolved_independent_stochastic_retry_budget",
                        true,
                        true,
                        "independent_binomial_retry_budget",
                        probability,
                        requiredAttempts,
                        true,
                        true,
                        MachineEvidencePaths(expandedRoute),
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        baselineAttempts);
                }
            }
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_retry_expanded_reservation_revalidation",
                false,
                null,
                "independent_binomial_retry_budget",
                probability,
                requiredAttempts,
                true,
                false,
                MachineEvidencePaths(route),
                Array.Empty<string>(),
                expansionReasons,
                baselineAttempts);
        }

        return Result(
            route,
            staticRoute.UncertaintyMode,
            "resolved_independent_stochastic_retry_budget",
            true,
            true,
            "independent_binomial_retry_budget",
            probability,
            requiredAttempts,
            false,
            true,
            MachineEvidencePaths(route),
            Array.Empty<string>(),
            Array.Empty<string>(),
            baselineAttempts);
    }

    private static AcquisitionRouteTargetDateStochasticRetry
        MachineProbabilityBlocked(
            AcquisitionRouteTargetDateProcessing route,
            AcquisitionRouteCalendarResolution staticRoute,
            int? baselineAttempts,
            string reason) => Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_stochastic_probability_evidence",
                false,
                null,
                "native_machine_retry_budget_requires_exact_probability",
                null,
                null,
                false,
                false,
                MachineEvidencePaths(route),
                Array.Empty<string>(),
                new[] { reason },
                baselineAttempts);

    private static bool MachineOutputSelectionAlreadyResolved(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute)
    {
        if (MachineCreditedExistingOutputQuantity(route) >=
            staticRoute.RequiredAmount)
        {
            return true;
        }
        var selected = route.Evaluations.Where(value =>
                value.OutputReadyOnTargetDate == true &&
                value.MachineScheduleBinding is
                    { ScheduleKind: "automatic_trigger_active_output",
                      ActiveOutputRouteMatches: true })
            .ToArray();
        return selected.Length > 0 &&
            AcquisitionOutputProof.ReadyQuantity(
                selected,
                staticRoute.MinimumQuality) >= staticRoute.RequiredAmount;
    }

    private static int MachineCreditedExistingOutputQuantity(
        AcquisitionRouteTargetDateProcessing route) => route.Evaluations.Sum(
            value => value.MachineScheduleBinding?
                .CreditedExistingOutputQuantity ?? 0);

    private static int? MachineBaselineAttemptCount(
        AcquisitionRouteTargetDateProcessing route)
    {
        var counts = route.Evaluations
            .Select(value => value.MachineScheduleBinding?.RequiredAttemptCount)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();
        return counts.Length == 1 ? counts[0] : null;
    }

    private static string[] MachineEvidencePaths(
        AcquisitionRouteTargetDateProcessing route) => route.Evaluations
            .SelectMany(value => value.EvidencePaths)
            .Append("static_calendar_resolution.routes[].machine_source.output_selection_rows[]")
            .Append("locked decompile MachineDataUtility.GetOutputData")
            .Append("locked decompile GameStateQuery.RandomImpl")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool IsMachineRoute(string routeKind) =>
        MachineRouteKinds.Contains(routeKind);
}
