namespace StardewAI.GoalConditionedBootstrap;

using StardewAI.Contracts.Strategy;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineActiveOutputCredit(
        AcquisitionRouteCalendarResolution manualRoute,
        AcquisitionRouteCalendarResolution stochasticRoute)
    {
        var creditedRoute = manualRoute with { RequiredAmount = 2 };
        var creditedMachines = new[]
        {
            MachineProcessingRow(
                creditedRoute,
                2,
                6,
                30,
                creditedRoute.QualifiedItemId,
                includeActiveSource: true),
            MachineProcessingRow(creditedRoute, 3, 6, 0)
        };
        var creditedReservation = MachineProcessingReservation(
            creditedRoute,
            creditedMachines);
        var creditedProcessing =
            AcquisitionRouteTargetDateProcessingBuilder.EvaluateMachine(
                creditedReservation,
                creditedRoute,
                MachineProcessingState(900, creditedMachines),
                0);
        Require(creditedReservation.UpstreamRoute.UpstreamRoute
                    .InputEvaluations.All(value => value.MachineBinding is
                    {
                        RequiredAttemptCount: 1,
                        CreditedExistingOutputQuantity: 1
                    }) &&
                creditedProcessing.ProcessingLeadTimeMatchesTargetDate == true &&
                creditedProcessing.Evaluations.Sum(value =>
                    value.MachineScheduleBinding?.ScheduledAttemptCount ?? 0) ==
                    1 &&
                creditedProcessing.Evaluations.Sum(value =>
                    value.MachineScheduleBinding?
                        .CreditedExistingOutputQuantity ?? 0) == 1 &&
                AcquisitionOutputProof.ReadyQuantity(
                    creditedProcessing.Evaluations,
                    creditedRoute.MinimumQuality) == 2,
            "Partial existing machine output credit did not reach processing.");

        var fullyCreditedRoute = manualRoute with { RequiredAmount = 1 };
        var fullyCreditedMachines = new[]
        {
            MachineProcessingRow(
                fullyCreditedRoute,
                2,
                6,
                30,
                fullyCreditedRoute.QualifiedItemId,
                includeActiveSource: true)
        };
        var fullyCreditedReservation = MachineProcessingReservation(
            fullyCreditedRoute,
            fullyCreditedMachines);
        var fullyCreditedProcessing =
            AcquisitionRouteTargetDateProcessingBuilder.EvaluateMachine(
                fullyCreditedReservation,
                fullyCreditedRoute,
                MachineProcessingState(900, fullyCreditedMachines),
                0);
        Require(fullyCreditedReservation.UpstreamRoute.UpstreamRoute
                    .InputEvaluations.Single().MachineBinding is
                    {
                        RequiredAttemptCount: 0,
                        CreditedExistingOutputQuantity: 1
                    } &&
                fullyCreditedProcessing.ProcessingLeadTimeMatchesTargetDate ==
                    true &&
                fullyCreditedProcessing.Evaluations.Single()
                    .MachineScheduleBinding is
                    {
                        ScheduledAttemptCount: 0,
                        CreditedExistingOutputQuantity: 1,
                        CompletionOffsetMinutes: 30
                    },
            "Fully credited machine output still scheduled a new attempt.");
        var fullyCreditedRetry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                fullyCreditedProcessing,
                fullyCreditedRoute);
        Require(fullyCreditedRetry.StochasticRetryAxisResolved &&
                fullyCreditedRetry.StochasticRetryBudgetMatchesTargetDate ==
                    true &&
                fullyCreditedRetry.RequiredAttemptCount == 0 &&
                fullyCreditedRetry.BaselineAttemptCount == 0 &&
                fullyCreditedRetry.AdditionalRetryCount == 0,
            "Fully credited machine output gained a retry attempt.");

        VerifyMachineActiveOutputCreditDaily(
            creditedRoute,
            fullyCreditedRoute);
        VerifyMachineActiveOutputCreditRetry(stochasticRoute);
    }

    private static void VerifyMachineActiveOutputCreditDaily(
        AcquisitionRouteCalendarResolution creditedRoute,
        AcquisitionRouteCalendarResolution fullyCreditedRoute)
    {
        var creditedDailyRoute = creditedRoute with
        {
            CalendarWindows = new[] { MachineDailyWindow() }
        };
        var creditedDailyMachines = new[]
        {
            MachineProcessingRow(
                creditedDailyRoute,
                2,
                6,
                30,
                creditedDailyRoute.QualifiedItemId,
                includeActiveSource: true),
            MachineProcessingRow(creditedDailyRoute, 3, 6, 0)
        };
        var creditedDailyProcessing =
            AcquisitionRouteTargetDateProcessingBuilder.EvaluateMachine(
                MachineProcessingReservation(
                    creditedDailyRoute,
                    creditedDailyMachines),
                creditedDailyRoute,
                MachineProcessingState(900, creditedDailyMachines),
                0);
        var creditedDailyRetry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                creditedDailyProcessing,
                creditedDailyRoute);
        var creditedDailyBudget =
            AcquisitionRouteTargetDateDailyTimeEnergyBuilder.EvaluateMachine(
                creditedDailyRetry,
                creditedDailyRoute,
                MachineDailyTimeEnergyState(
                    creditedDailyMachines,
                    emptyInventorySlots: 1));
        var creditedDailySteps = creditedDailyBudget.Evaluation?
            .TerminalRouteSteps ??
            Array.Empty<AcquisitionDailyTerminalRouteStep>();
        Require(creditedDailyBudget.DailyTimeEnergyMatchesTargetDate == true &&
                creditedDailyBudget.Evaluation?.RequiredAttemptCount == 1 &&
                creditedDailySteps.Count(value => value.ActionKind ==
                    "load_machine_input") == 1 &&
                creditedDailySteps.Count(value => value.ActionKind ==
                    "collect_machine_output") == 2,
            "Partial existing output credit did not reduce the daily load queue.");

        var fullyCreditedDailyRoute = fullyCreditedRoute with
        {
            CalendarWindows = new[] { MachineDailyWindow() }
        };
        var fullyCreditedDailyMachines = new[]
        {
            MachineProcessingRow(
                fullyCreditedDailyRoute,
                2,
                6,
                30,
                fullyCreditedDailyRoute.QualifiedItemId,
                includeActiveSource: true)
        };
        var fullyCreditedDailyProcessing =
            AcquisitionRouteTargetDateProcessingBuilder.EvaluateMachine(
                MachineProcessingReservation(
                    fullyCreditedDailyRoute,
                    fullyCreditedDailyMachines),
                fullyCreditedDailyRoute,
                MachineProcessingState(900, fullyCreditedDailyMachines),
                0);
        var fullyCreditedDailyRetry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                fullyCreditedDailyProcessing,
                fullyCreditedDailyRoute);
        var fullyCreditedDailyBudget =
            AcquisitionRouteTargetDateDailyTimeEnergyBuilder.EvaluateMachine(
                fullyCreditedDailyRetry,
                fullyCreditedDailyRoute,
                MachineDailyTimeEnergyState(
                    fullyCreditedDailyMachines,
                    emptyInventorySlots: 1));
        Require(fullyCreditedDailyBudget.DailyTimeEnergyMatchesTargetDate ==
                    true &&
                fullyCreditedDailyBudget.Evaluation is
                {
                    RequiredAttemptCount: 0,
                    TerminalActionGameMinutes: 1
                } &&
                fullyCreditedDailyBudget.Evaluation.TerminalRouteSteps
                    .Select(value => value.ActionKind)
                    .SequenceEqual(new[] { "collect_machine_output" }),
            "Fully credited machine output did not compile to collect-only.");

        var productionUnsupportedRoute = fullyCreditedDailyRoute with
        {
            MachineSource = fullyCreditedDailyRoute.MachineSource! with
            {
                DaysUntilReady = 1,
                MinutesUntilReady = -1,
                OnlyCompleteOvernight = true
            }
        };
        var productionUnsupportedMachines = new[]
        {
            MachineProcessingRow(
                productionUnsupportedRoute,
                2,
                6,
                30,
                productionUnsupportedRoute.QualifiedItemId,
                includeActiveSource: true)
        };
        var collectOnlyProcessing =
            AcquisitionRouteTargetDateProcessingBuilder.EvaluateMachine(
                MachineProcessingReservation(
                    productionUnsupportedRoute,
                    productionUnsupportedMachines),
                productionUnsupportedRoute,
                MachineProcessingState(900, productionUnsupportedMachines),
                0);
        var collectOnlyRetry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                collectOnlyProcessing,
                productionUnsupportedRoute);
        var collectOnlyBudget =
            AcquisitionRouteTargetDateDailyTimeEnergyBuilder.EvaluateMachine(
                collectOnlyRetry,
                productionUnsupportedRoute,
                MachineDailyTimeEnergyState(
                    productionUnsupportedMachines,
                    emptyInventorySlots: 1));
        Require(collectOnlyProcessing.ProcessingLeadTimeMatchesTargetDate ==
                    true &&
                collectOnlyBudget.DailyTimeEnergyMatchesTargetDate == true &&
                collectOnlyBudget.Evaluation is
                {
                    RequiredAttemptCount: 0,
                    TerminalRouteSteps:
                    [{ ActionKind: "collect_machine_output" }]
                },
            "Collect-only credit was rejected by unused production semantics.");
    }

    private static void VerifyMachineActiveOutputCreditRetry(
        AcquisitionRouteCalendarResolution stochasticRoute)
    {
        var creditedRoute = stochasticRoute with { RequiredAmount = 2 };
        var machines = new[]
        {
            MachineProcessingRow(
                creditedRoute,
                2,
                6,
                30,
                creditedRoute.QualifiedItemId,
                includeActiveSource: true),
            MachineProcessingRow(creditedRoute, 3, 6, 0),
            MachineProcessingRow(creditedRoute, 4, 6, 0),
            MachineProcessingRow(creditedRoute, 5, 6, 0)
        };
        var processing =
            AcquisitionRouteTargetDateProcessingBuilder.EvaluateMachine(
                MachineProcessingReservation(creditedRoute, machines),
                creditedRoute,
                MachineProcessingState(900, machines),
                0);
        var retry =
            AcquisitionRouteTargetDateStochasticRetryBuilder.EvaluateMachine(
                processing,
                creditedRoute,
                MachineRetryExpansionContextFixture(machines));
        Require(retry.StochasticRetryAxisResolved &&
                retry.StochasticRetryBudgetMatchesTargetDate == true &&
                retry.BaselineAttemptCount == 1 &&
                retry.RequiredAttemptCount > 1 &&
                retry.UpstreamRoute.Evaluations.Sum(value =>
                    value.MachineScheduleBinding?
                        .CreditedExistingOutputQuantity ?? 0) == 1 &&
                retry.UpstreamRoute.Evaluations.Sum(value =>
                    value.MachineScheduleBinding?.ScheduledAttemptCount ?? 0) ==
                    retry.RequiredAttemptCount,
            "Machine retries were not limited to the uncredited output remainder.");
    }
}
