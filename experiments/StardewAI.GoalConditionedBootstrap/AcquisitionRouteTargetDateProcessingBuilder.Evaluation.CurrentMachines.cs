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
        var targets = facility.TargetEvaluations.Where(value =>
                value.Status ==
                    "resolved_existing_current_machine_capacity_match" &&
                value.MachineSourceMatches == true &&
                value.MachineActiveOutputEvidenceAvailable == true &&
                value.MachineActiveOutputRouteMatches == true &&
                value.MachineActiveOutputQualifiedItemId ==
                    staticRoute.QualifiedItemId &&
                value.MachineActiveOutputStack is > 0 &&
                value.MachineActiveOutputQuality is >= 0)
            .ToArray();
        if (targets.Length == 0)
        {
            return Blocked(
                route,
                CurrentMachineOutput,
                "current_machine_materialized_output_evidence_missing");
        }

        var evaluations = targets.Select(target =>
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
        var provenQuantity = AcquisitionOutputProof.ReadyQuantity(
            evaluations,
            staticRoute.MinimumQuality);
        return provenQuantity >= staticRoute.RequiredAmount
            ? ResolvedMatch(route, CurrentMachineOutput, evaluations)
            : ResolvedMiss(
                route,
                CurrentMachineOutput,
                evaluations,
                "current_machine_materialized_output_shortfall:" +
                provenQuantity + ":" + staticRoute.RequiredAmount);
    }
}
