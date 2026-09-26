namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static AcquisitionRouteTargetDateFacility
        EvaluateExistingMachine(
            AcquisitionRouteTargetDateLocation route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionLocationRouteSnapshotState state)
    {
        var source = staticRoute.MachineSource;
        if (source is null ||
            string.IsNullOrWhiteSpace(source.MachineQualifiedItemId))
        {
            return Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                ExistingMachine,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                new[] { "authoritative_machine_source_binding_missing" });
        }
        if (!state.MachineFleet.EvidenceAvailable)
        {
            return Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                ExistingMachine,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                state.MachineFleet.BlockingReasons);
        }

        var targets = route.TargetEvaluations.Where(value =>
                value.Status == "resolved_location_route_match" &&
                value.BindingKind == "runtime_machine_source_location")
            .ToArray();
        Require(targets.Length > 0,
            "A matched machine route lacks its exact placed-machine target.");
        var evaluations = targets.Select(target =>
            EvaluateMachineTarget(target, source, state.MachineFleet)).ToArray();
        var blocking = evaluations
            .SelectMany(value => value.BlockingReasons)
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
                ExistingMachine,
                evaluations,
                Array.Empty<string>(),
                blocking);
        }
        if (evaluations.Any(value => value.MachineSourceMatches == true))
        {
            return Result(
                route,
                "resolved_existing_machine_capacity_match",
                true,
                true,
                ExistingMachine,
                evaluations,
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        return Result(
            route,
            "resolved_existing_machine_capacity_miss",
            true,
            false,
            ExistingMachine,
            evaluations,
            new[] { "matching_machine_source_capacity_not_present" },
            Array.Empty<string>());
    }

    internal static AcquisitionFacilityTargetEvaluation EvaluateMachineTarget(
        AcquisitionLocationRouteTargetEvaluation target,
        AcquisitionMachineSourceEvidence source,
        AcquisitionMachineFleetSnapshotState machineFleet)
    {
        if (!target.TargetTileX.HasValue || !target.TargetTileY.HasValue)
        {
            return MachineEvaluation(
                target,
                source,
                "blocked_existing_machine_capacity_evidence",
                null,
                string.Empty,
                new[] { "machine_location_target_tile_missing" });
        }
        if (!machineFleet.TryGet(
                target.TargetLocationId,
                target.TargetTileX.Value,
                target.TargetTileY.Value,
                out var machine))
        {
            return MachineEvaluation(
                target,
                source,
                "blocked_existing_machine_capacity_evidence",
                null,
                string.Empty,
                new[] { "machine_location_target_drifted_from_snapshot" });
        }
        if (!string.Equals(
                machine.QualifiedItemId,
                source.MachineQualifiedItemId,
                StringComparison.Ordinal))
        {
            return MachineEvaluation(
                target,
                source,
                "blocked_existing_machine_capacity_evidence",
                null,
                machine.CapacityState,
                new[] { "machine_source_identity_drifted_from_snapshot" });
        }
        return MachineEvaluation(
            target,
            source,
            machine.MachineHasOutput
                ? "resolved_existing_machine_capacity_match"
                : "resolved_existing_machine_capacity_miss",
            machine.MachineHasOutput,
            machine.CapacityState,
            Array.Empty<string>());
    }

    private static AcquisitionFacilityTargetEvaluation MachineEvaluation(
        AcquisitionLocationRouteTargetEvaluation target,
        AcquisitionMachineSourceEvidence source,
        string status,
        bool? matches,
        string capacityState,
        string[] blockingReasons) => new(
            target.TargetLocationId,
            null,
            status,
            null,
            null,
            null,
            null,
            new[]
            {
                "upstream_route.target_evaluations[].target_tile_x",
                "upstream_route.target_evaluations[].target_tile_y",
                "state.farm.machines.value[]"
            },
            blockingReasons,
            source.MachineQualifiedItemId,
            target.TargetTileX,
            target.TargetTileY,
            matches,
            capacityState);
}
