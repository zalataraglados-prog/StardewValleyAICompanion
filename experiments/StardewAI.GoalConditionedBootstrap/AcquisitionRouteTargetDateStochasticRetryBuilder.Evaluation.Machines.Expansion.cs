namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    private static bool TryExpandMachineRetryRoute(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteCalendarResolution staticRoute,
        int requiredAttempts,
        int creditedExistingOutput,
        MachineRetryExpansionContext context,
        out AcquisitionRouteTargetDateProcessing expandedRoute,
        out string[] blockingReasons)
    {
        expandedRoute = route;
        var reasons = new List<string>();
        var source = staticRoute.MachineSource!;
        int expandedOutputRequirement;
        try
        {
            expandedOutputRequirement = checked(
                creditedExistingOutput +
                requiredAttempts * Math.Max(1, source.MinimumStack));
        }
        catch (OverflowException)
        {
            blockingReasons = new[]
            {
                "machine_retry_expanded_output_requirement_overflow"
            };
            return false;
        }
        var expandedStaticRoute = staticRoute with
        {
            RequiredAmount = expandedOutputRequirement
        };
        var facility = route.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;
        var resource = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            facility,
            expandedStaticRoute,
            context.Resources);
        if (resource.ResourceInputsMatchTargetDate != true)
        {
            AddAxisReasons(
                reasons,
                "machine_retry_resource",
                resource.NonMatchingReasons,
                resource.BlockingReasons);
        }

        var currency = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            resource,
            expandedStaticRoute,
            context.Currencies);
        if (currency.CurrencyBudgetMatchesTargetDate != true)
        {
            AddAxisReasons(
                reasons,
                "machine_retry_currency",
                currency.NonMatchingReasons,
                currency.BlockingReasons);
        }

        var reservation = AcquisitionRouteTargetDateReservationBuilder.Evaluate(
            currency,
            context.GoalId,
            context.StateHash,
            context.Ledger,
            context.Resources,
            context.Currencies);
        if (reservation.InventoryReservationMatchesTargetDate != true ||
            reservation.ClaimSet is null ||
            !reservation.ClaimSet.AtomicCommitRequired)
        {
            AddAxisReasons(
                reasons,
                "machine_retry_reservation",
                reservation.NonMatchingReasons,
                reservation.BlockingReasons);
            if (reservation.ClaimSet is null)
                reasons.Add("machine_retry_expanded_claim_set_missing");
            else if (!reservation.ClaimSet.AtomicCommitRequired)
                reasons.Add("machine_retry_expanded_claim_set_not_atomic");
        }

        var processing = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                reservation,
                expandedStaticRoute,
                context.Processing,
                context.TargetTotalDay);
        var scheduledAttempts = processing.Evaluations.Sum(value =>
            value.MachineScheduleBinding?.ScheduledAttemptCount ?? 0);
        var creditedQuantity = processing.Evaluations.Sum(value =>
            value.MachineScheduleBinding?.CreditedExistingOutputQuantity ?? 0);
        if (processing.ProcessingLeadTimeMatchesTargetDate != true ||
            scheduledAttempts != requiredAttempts ||
            creditedQuantity != creditedExistingOutput ||
            processing.Evaluations.Any(value =>
                value.MachineScheduleBinding is not null &&
                value.MachineScheduleBinding.RequiredAttemptCount !=
                    requiredAttempts))
        {
            AddAxisReasons(
                reasons,
                "machine_retry_processing",
                processing.NonMatchingReasons,
                processing.BlockingReasons);
            if (scheduledAttempts != requiredAttempts)
            {
                reasons.Add(
                    "machine_retry_expanded_scheduled_attempt_count_mismatch:" +
                    scheduledAttempts + ":" + requiredAttempts);
            }
            if (creditedQuantity != creditedExistingOutput)
            {
                reasons.Add(
                    "machine_retry_expanded_existing_output_credit_mismatch:" +
                    creditedQuantity + ":" + creditedExistingOutput);
            }
        }

        blockingReasons = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blockingReasons.Length > 0)
            return false;
        expandedRoute = processing;
        return true;
    }

    private static void AddAxisReasons(
        ICollection<string> destination,
        string prefix,
        IEnumerable<string> nonMatchingReasons,
        IEnumerable<string> blockingReasons)
    {
        var reasons = nonMatchingReasons.Concat(blockingReasons).ToArray();
        if (reasons.Length == 0)
        {
            destination.Add(prefix + "_axis_did_not_match");
            return;
        }
        foreach (var reason in reasons)
            destination.Add(prefix + ":" + reason);
    }
}

internal sealed record MachineRetryExpansionContext(
    string GoalId,
    string StateHash,
    int TargetTotalDay,
    AcquisitionStrategyLedgerState Ledger,
    AcquisitionResourceInputSnapshotState Resources,
    AcquisitionShopQuoteSnapshotState Currencies,
    AcquisitionProcessingLeadTimeSnapshotState Processing);
