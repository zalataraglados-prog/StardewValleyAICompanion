namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static AcquisitionRouteTargetDateFacility
        EvaluateExistingGeodeService(
            AcquisitionRouteTargetDateLocation route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionCurrentRouteCandidateIndex candidates)
    {
        if (!candidates.TryFind(
                staticRoute.RouteKind,
                staticRoute.SourceId,
                staticRoute.QualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                ExistingGeodeService,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                blockingReasons);
        }

        var resolvedTargets = route.TargetEvaluations
            .Where(value => value.Status == "resolved_location_route_match")
            .Select(value => (
                value.TargetLocationId,
                value.TargetTileX,
                value.TargetTileY))
            .ToHashSet();
        var candidate = matches
            .Where(value => value.OptionId == "processing.crack_geode" &&
                resolvedTargets.Contains((
                    value.LocationId,
                    value.TargetTileX,
                    value.TargetTileY)))
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is null)
        {
            return Result(
                route,
                "resolved_facility_capacity_miss",
                true,
                false,
                ExistingGeodeService,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                new[] { "matching_current_geode_service_candidate_not_present" },
                Array.Empty<string>());
        }

        return Result(
            route,
            "resolved_existing_geode_service_match",
            true,
            true,
            ExistingGeodeService,
            new[]
            {
                new AcquisitionFacilityTargetEvaluation(
                    candidate.LocationId,
                    null,
                    "resolved_existing_geode_service_match",
                    null,
                    null,
                    null,
                    null,
                    new[]
                    {
                        "candidate:processing.crack_geode",
                        "state.player.geode_processing.value.base_service_status",
                        "state.player.geode_processing.value.clint",
                        "state.player.geode_processing.value.counter_action_tiles"
                    },
                    Array.Empty<string>(),
                    TargetTileX: candidate.TargetTileX,
                    TargetTileY: candidate.TargetTileY)
            },
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
