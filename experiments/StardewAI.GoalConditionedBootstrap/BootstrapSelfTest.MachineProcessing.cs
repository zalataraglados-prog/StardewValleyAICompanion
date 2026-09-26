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
                        { CompletionOffsetMinutes: 60 }),
            "Parallel same-day machine processing schedule drifted.");

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
                      CompletionOffsetMinutes: 30 },
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
