namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static AcquisitionRouteTargetDateProcessing
        EvaluateCurrentMachineOutput(
            AcquisitionRouteTargetDateReservation route,
            AcquisitionRouteCalendarResolution staticRoute,
            int targetTotalDay)
    {
        var facility = route.UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var targets = AcquisitionCurrentMachineOutputTargetSelector
            .SelectMatching(
                facility.TargetEvaluations,
                staticRoute.QualifiedItemId);
        if (targets.Length == 0)
        {
            return Blocked(
                route,
                CurrentMachineOutput,
                "current_machine_materialized_output_evidence_missing");
        }

        var dispatchableTargets = AcquisitionCurrentMachineOutputTargetSelector
            .SelectDispatchable(
                targets,
                staticRoute.QualifiedItemId,
                staticRoute.RequiredAmount,
                staticRoute.MinimumQuality);
        var evaluatedTargets = dispatchableTargets.Length > 0
            ? dispatchableTargets
            : targets;
        var evaluations = evaluatedTargets.Select(target =>
                new AcquisitionProcessingLeadTimeEvaluation(
                    target.TargetLocationId,
                    "current_machine_ready_output",
                    "resolved_current_machine_output_materialized",
                    "zero_wait_materialized_output",
                    null,
                    0,
                    targetTotalDay,
                    target.MachineActiveOutputStack,
                    target.MachineActiveOutputQuality,
                    true,
                    new[]
                    {
                        "target_date_facility_capacity.routes[].target_evaluations[]",
                        "state.farm.machines.value[].held_item",
                        "state.farm.machines.value[].active_output_authoritative_route_sources"
                    },
                    Array.Empty<string>(),
                    OutputMaterializedAtSnapshot: true))
            .ToArray();
        var largestProvenQuantity = targets
            .Where(target =>
                target.MachineActiveOutputQuality >=
                    staticRoute.MinimumQuality)
            .Select(target => target.MachineActiveOutputStack ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return dispatchableTargets.Length > 0
            ? ResolvedMatch(route, CurrentMachineOutput, evaluations)
            : ResolvedMiss(
                route,
                CurrentMachineOutput,
                evaluations,
                "current_machine_single_output_shortfall:" +
                largestProvenQuantity + ":" + staticRoute.RequiredAmount);
    }
}
