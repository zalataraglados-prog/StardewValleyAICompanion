namespace StardewAI.GoalConditionedBootstrap;

using StardewAI.Contracts.Strategy;

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
                parallel.Evaluations
                    .SelectMany(value => value.MachineScheduleBinding!
                        .AttemptSchedule)
                    .OrderBy(value => value.AttemptOrdinal)
                    .Select(value => value.AttemptOrdinal)
                    .SequenceEqual(new[] { 1, 2 }) &&
                parallel.Evaluations.All(value =>
                    value.MachineScheduleBinding!.AttemptSchedule.Single() is
                        { MachineAttemptOrdinal: 1,
                          ProcessingStartOffsetMinutes: 0,
                          CompletionOffsetMinutes: 60 }) &&
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

        var dailyRoute = manualRoute with
        {
            CalendarWindows = new[] { MachineDailyWindow() }
        };
        var dailyMachines = new[]
        {
            MachineProcessingRow(dailyRoute, 2, 6, 0),
            MachineProcessingRow(dailyRoute, 3, 6, 0)
        };
        var dailyProcessing = AcquisitionRouteTargetDateProcessingBuilder
            .EvaluateMachine(
                MachineProcessingReservation(dailyRoute, dailyMachines),
                dailyRoute,
                MachineProcessingState(900, dailyMachines),
                0);
        var dailyRetry = AcquisitionRouteTargetDateStochasticRetryBuilder
            .EvaluateMachine(dailyProcessing, dailyRoute);
        var dailyBudget = AcquisitionRouteTargetDateDailyTimeEnergyBuilder
            .EvaluateMachine(
                dailyRetry,
                dailyRoute,
                MachineDailyTimeEnergyState(dailyMachines));
        var dailySteps = dailyBudget.Evaluation?.TerminalRouteSteps ??
            Array.Empty<AcquisitionDailyTerminalRouteStep>();
        Require(dailyBudget.DailyTimeEnergyMatchesTargetDate == true &&
                dailyBudget.Evaluation is
                {
                    RequiredAttemptCount: 2,
                    TerminalActionGameMinutes: 4,
                    RequiredEnergy: 0d,
                    TimeBudgetMatches: true,
                    EnergyBudgetMatches: true
                } &&
                dailySteps.Length == 4 &&
                dailySteps.Count(value => value.ActionKind ==
                    "load_machine_input") == 2 &&
                dailySteps.Count(value => value.ActionKind ==
                    "collect_machine_output") == 2 &&
                dailySteps[^1].GuaranteedCompletionByTime >= 1000,
            "Machine movement, interaction, wait and collection budget drifted.");

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
        var expandedRetryRevalidated =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                parallel,
                firstValidFallback,
                MachineRetryExpansionContextFixture(
                    manualMachines));
        var expandedReservation = expandedRetryRevalidated.UpstreamRoute
            .UpstreamRoute;
        Require(expandedRetryRevalidated.StochasticRetryAxisResolved &&
                expandedRetryRevalidated
                    .StochasticRetryBudgetMatchesTargetDate == true &&
                expandedRetryRevalidated.RetryExpandsReservedConsumables &&
                expandedRetryRevalidated
                    .RetryExpandedReservationRevalidated &&
                expandedReservation.ClaimSet is
                    { AtomicCommitRequired: true } &&
                expandedReservation.ClaimSet.MaterialClaims.Sum(value =>
                    value.Quantity) ==
                    expandedRetryRevalidated.RequiredAttemptCount &&
                expandedRetryRevalidated.UpstreamRoute.Evaluations.Sum(value =>
                    value.MachineScheduleBinding?.ScheduledAttemptCount ?? 0) ==
                    expandedRetryRevalidated.RequiredAttemptCount &&
                expandedRetryRevalidated.UpstreamRoute.Evaluations
                    .SelectMany(value => value.MachineScheduleBinding?
                        .AttemptSchedule ??
                        Array.Empty<
                            AcquisitionMachineProcessingAttemptBinding>())
                    .Count() ==
                    expandedRetryRevalidated.RequiredAttemptCount,
            "Expanded machine retries did not reuse reservation and processing axes.");

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
            CalendarWindows = new[] { MachineDailyWindow() },
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
                2,
                6,
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
        var automaticRetry = AcquisitionRouteTargetDateStochasticRetryBuilder
            .EvaluateMachine(automatic, automaticRoute);
        var automaticDaily =
            AcquisitionRouteTargetDateDailyTimeEnergyBuilder.EvaluateMachine(
                automaticRetry,
                automaticRoute,
                MachineDailyTimeEnergyState(activeMachine));
        Require(automaticDaily.DailyTimeEnergyMatchesTargetDate == true &&
                automaticDaily.Evaluation is
                {
                    TerminalActionGameMinutes: 1,
                    RequiredAttemptCount: 1,
                    RequiredEnergy: 0d
                } &&
                automaticDaily.Evaluation.TerminalRouteSteps.Single()
                    .ActionKind == "collect_machine_output" &&
                automaticDaily.Evaluation.TerminalRouteSteps.Single()
                    .ActionStartTime >= 930,
            "Automatic machine completion wait and collection budget drifted.");

        var unresolvedMachine = new[]
        {
            MachineProcessingRow(
                automaticRoute,
                2,
                6,
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
