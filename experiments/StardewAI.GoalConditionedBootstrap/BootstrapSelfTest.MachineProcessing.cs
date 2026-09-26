namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineProcessingResolution()
    {
        var baseRoute = MachineFacilityStaticRoute();
        var manualRoute = baseRoute with
        {
            RequiredAmount = 2,
            MachineSource = baseRoute.MachineSource! with
            {
                MinutesUntilReady = 60,
                DaysUntilReady = -1,
                MinimumStack = 1,
                MaximumStack = 1
            }
        };
        var manualMachines = new[]
        {
            MachineProcessingRow(manualRoute, 12, 34, 0),
            MachineProcessingRow(manualRoute, 13, 34, 0)
        };
        var parallel = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                MachineProcessingReservation(manualRoute, manualMachines),
                manualRoute,
                MachineProcessingState(2500, manualMachines),
                0);
        Require(parallel.ProcessingLeadTimeMatchesTargetDate == true &&
                parallel.Evaluations.Sum(value =>
                    value.MachineScheduleBinding?.ScheduledAttemptCount ?? 0) ==
                    2 &&
                parallel.Evaluations.All(value =>
                    value.MachineScheduleBinding is
                        { CompletionOffsetMinutes: 60 }) &&
                parallel.Evaluations.All(value =>
                    !value.OutputMaterializedAtSnapshot) &&
                !AcquisitionRouteTargetDateStochasticRetryBuilder
                    .CurrentOutputAlreadyMaterialized(parallel, manualRoute),
            "Parallel same-day machine processing schedule drifted.");

        Require(AcquisitionRouteTargetDateStochasticRetryBuilder
                    .TryMachineSingleAttemptProbability(
                        manualRoute,
                        out var deterministicProbability,
                        out _) &&
                deterministicProbability == 1d,
            "Deterministic machine output probability drifted.");
        var deterministicRetry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                parallel,
                manualRoute);
        Require(deterministicRetry.StochasticRetryAxisResolved &&
                deterministicRetry.StochasticRetryBudgetMatchesTargetDate ==
                    true &&
                deterministicRetry.RequiredAttemptCount == 2 &&
                deterministicRetry.BaselineAttemptCount == 2 &&
                deterministicRetry.AdditionalRetryCount == 0,
            "Guaranteed machine output gained a spurious retry budget.");

        var firstValidFallback = manualRoute with
        {
            MachineSource = manualRoute.MachineSource! with
            {
                OutputIndex = 1,
                OutputSelectionCount = 2,
                OutputSelectionRows = new[]
                {
                    new AcquisitionMachineOutputSelectionRowEvidence(
                        0,
                        "RANDOM 0.3",
                        "(O)348",
                        string.Empty,
                        1,
                        1,
                        string.Empty,
                        string.Empty),
                    new AcquisitionMachineOutputSelectionRowEvidence(
                        1,
                        string.Empty,
                        "(O)346",
                        string.Empty,
                        1,
                        1,
                        string.Empty,
                        string.Empty)
                }
            }
        };
        Require(AcquisitionRouteTargetDateStochasticRetryBuilder
                    .TryMachineSingleAttemptProbability(
                        firstValidFallback,
                        out var fallbackProbability,
                        out _) &&
                Math.Abs(fallbackProbability - 0.7d) < 0.0000001d,
            "First-valid machine fallback probability drifted.");
        var expandedRetry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                parallel,
                firstValidFallback);
        Require(!expandedRetry.StochasticRetryAxisResolved &&
                expandedRetry.StochasticRetryAxisStatus ==
                    "blocked_retry_expanded_reservation_revalidation" &&
                expandedRetry.RequiredAttemptCount >
                    expandedRetry.BaselineAttemptCount &&
                expandedRetry.AdditionalRetryCount ==
                    expandedRetry.RequiredAttemptCount -
                    expandedRetry.BaselineAttemptCount &&
                expandedRetry.RetryExpandsReservedConsumables &&
                !expandedRetry.RetryExpandedReservationRevalidated,
            "Expanded machine retry demand bypassed reservation closure.");

        var randomValid = firstValidFallback with
        {
            MachineSource = firstValidFallback.MachineSource! with
            {
                UseFirstValidOutput = false,
                OutputIndex = 1,
                OutputSelectionCount = 3,
                OutputSelectionRows = new[]
                {
                    new AcquisitionMachineOutputSelectionRowEvidence(
                        0, string.Empty, "(O)348", string.Empty, 1, 1,
                        string.Empty, string.Empty),
                    new AcquisitionMachineOutputSelectionRowEvidence(
                        1, string.Empty, "(O)346", string.Empty, 1, 1,
                        string.Empty, string.Empty),
                    new AcquisitionMachineOutputSelectionRowEvidence(
                        2, string.Empty, "(O)390", string.Empty, 1, 1,
                        string.Empty, string.Empty)
                }
            }
        };
        Require(AcquisitionRouteTargetDateStochasticRetryBuilder
                    .TryMachineSingleAttemptProbability(
                        randomValid,
                        out var randomValidProbability,
                        out _) &&
                Math.Abs(randomValidProbability - 1d / 3d) < 0.0000001d,
            "Uniform random-valid machine output probability drifted.");

        var singleMachine = new[]
        {
            MachineProcessingRow(manualRoute, 12, 34, 0)
        };
        var capacityMiss = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                MachineProcessingReservation(manualRoute, singleMachine),
                manualRoute,
                MachineProcessingState(2500, singleMachine),
                0);
        Require(capacityMiss.ProcessingLeadTimeMatchesTargetDate == false &&
                capacityMiss.NonMatchingReasons.Contains(
                    "machine_processing_capacity_before_day_end_shortfall:1:2",
                    StringComparer.Ordinal),
            "Machine same-day capacity shortfall was not preserved.");

        var overnightRoute = manualRoute with
        {
            RequiredAmount = 1,
            MachineSource = manualRoute.MachineSource! with
            {
                MinutesUntilReady = -1,
                DaysUntilReady = 1
            }
        };
        var overnightMachine = new[]
        {
            MachineProcessingRow(overnightRoute, 12, 34, 0)
        };
        var overnight = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                MachineProcessingReservation(
                    overnightRoute,
                    overnightMachine),
                overnightRoute,
                MachineProcessingState(900, overnightMachine),
                0);
        Require(overnight.ProcessingLeadTimeMatchesTargetDate == false &&
                overnight.Evaluations.Single().MachineScheduleBinding is
                    { AuthoritativeDaysPerAttempt: 1,
                      ScheduledAttemptCount: 0 },
            "DaysUntilReady machine output incorrectly matched the current day.");

        var automaticRoute = baseRoute with
        {
            MachineSource = baseRoute.MachineSource! with
            {
                MinutesUntilReady = 30,
                DaysUntilReady = -1,
                Triggers = new[]
                {
                    new AcquisitionMachineTriggerEvidence(
                        "day_update",
                        8,
                        string.Empty,
                        Array.Empty<string>(),
                        1,
                        string.Empty)
                }
            }
        };
        var activeMachine = new[]
        {
            MachineProcessingRow(
                automaticRoute,
                12,
                34,
                30,
                automaticRoute.QualifiedItemId,
                includeActiveSource: true)
        };
        var automatic = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                MachineProcessingReservation(automaticRoute, activeMachine),
                automaticRoute,
                MachineProcessingState(900, activeMachine),
                0);
        Require(automatic.ProcessingLeadTimeMatchesTargetDate == true &&
                automatic.Evaluations.Single().MachineScheduleBinding is
                    { ActiveOutputRouteMatches: true,
                      CompletionOffsetMinutes: 30 } &&
                !automatic.Evaluations.Single().OutputMaterializedAtSnapshot &&
                !AcquisitionRouteTargetDateStochasticRetryBuilder
                    .CurrentOutputAlreadyMaterialized(
                        automatic,
                        automaticRoute),
            "Exact automatic machine in-flight output was not admitted.");

        var unresolvedMachine = new[]
        {
            MachineProcessingRow(
                automaticRoute,
                12,
                34,
                30,
                automaticRoute.QualifiedItemId,
                includeActiveSource: false)
        };
        var unresolved = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                MachineProcessingReservation(
                    automaticRoute,
                    unresolvedMachine),
                automaticRoute,
                MachineProcessingState(900, unresolvedMachine),
                0);
        Require(!unresolved.ProcessingLeadTimeAxisResolved &&
                unresolved.BlockingReasons.Any(reason =>
                    reason.StartsWith(
                        "machine_active_output_source_unresolved:",
                        StringComparison.Ordinal)),
            "Unattributed automatic machine output did not fail closed.");
    }

}
