namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static AcquisitionRouteTargetDateFacility
        EvaluateExistingCurrentMachine(
            AcquisitionRouteTargetDateLocation route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionMachineFleetSnapshotState machineFleet)
    {
        if (!machineFleet.EvidenceAvailable)
        {
            return Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                ExistingCurrentMachine,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                machineFleet.BlockingReasons);
        }

        var targets = route.TargetEvaluations.Where(value =>
                value.Status == "resolved_location_route_match" &&
                value.BindingKind ==
                    "runtime_exact_authoritative_route_candidate")
            .ToArray();
        Require(targets.Length > 0,
            "A matched current-machine route lacks its exact runtime target.");
        var evaluations = targets.Select(target =>
                EvaluateExistingCurrentMachineTarget(
                    target,
                    staticRoute,
                    machineFleet))
            .ToArray();
        var blocking = evaluations.SelectMany(value => value.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blocking.Length > 0)
        {
            return Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                ExistingCurrentMachine,
                evaluations,
                Array.Empty<string>(),
                blocking);
        }

        Require(evaluations.All(value =>
                value.MachineSourceMatches == true &&
                value.MachineActiveOutputRouteMatches == true),
            "A current-machine facility evaluation lost its route identity.");
        return Result(
            route,
            "resolved_existing_current_machine_capacity_match",
            true,
            true,
            ExistingCurrentMachine,
            evaluations,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AcquisitionFacilityTargetEvaluation
        EvaluateExistingCurrentMachineTarget(
            AcquisitionLocationRouteTargetEvaluation target,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionMachineFleetSnapshotState machineFleet)
    {
        if (!target.TargetTileX.HasValue || !target.TargetTileY.HasValue)
        {
            return CurrentMachineEvaluation(
                target,
                null,
                null,
                new[] { "current_machine_location_target_tile_missing" });
        }
        if (!machineFleet.TryGet(
                target.TargetLocationId,
                target.TargetTileX.Value,
                target.TargetTileY.Value,
                out var machine))
        {
            return CurrentMachineEvaluation(
                target,
                null,
                null,
                new[] { "current_machine_target_drifted_from_snapshot" });
        }

        var routeMatches = MachineActiveOutputRouteMatches(
            machine,
            staticRoute);
        var blocking = new List<string>();
        if (!machine.MachineHasOutput || !machine.ReadyForHarvest)
            blocking.Add("current_machine_output_not_ready");
        if (!machine.ActiveOutputEvidenceAvailable ||
            machine.ActiveOutput is null)
        {
            blocking.Add("current_machine_active_output_evidence_missing");
        }
        if (routeMatches != true)
            blocking.Add("current_machine_active_output_route_mismatch");
        return CurrentMachineEvaluation(
            target,
            machine,
            routeMatches,
            blocking.ToArray());
    }

    private static AcquisitionFacilityTargetEvaluation CurrentMachineEvaluation(
        AcquisitionLocationRouteTargetEvaluation target,
        AcquisitionMachineRouteState? machine,
        bool? routeMatches,
        string[] blockingReasons) => new(
            target.TargetLocationId,
            null,
            blockingReasons.Length == 0
                ? "resolved_existing_current_machine_capacity_match"
                : "blocked_existing_current_machine_capacity_evidence",
            null,
            null,
            null,
            null,
            new[]
            {
                "upstream_route.target_evaluations[].target_tile_x",
                "upstream_route.target_evaluations[].target_tile_y",
                "state.farm.machines.value[]",
                "state.farm.machines.value[].active_output_authoritative_route_sources"
            },
            blockingReasons,
            machine?.QualifiedItemId,
            target.TargetTileX,
            target.TargetTileY,
            routeMatches,
            machine?.CapacityState,
            machine?.ActiveOutputEvidenceAvailable,
            machine?.ActiveOutput?.QualifiedItemId,
            machine?.ActiveOutput?.Stack,
            machine?.ActiveOutput?.Quality,
            routeMatches);
}
