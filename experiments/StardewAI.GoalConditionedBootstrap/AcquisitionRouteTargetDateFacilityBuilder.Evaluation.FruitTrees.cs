namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static AcquisitionRouteTargetDateFacility
        EvaluateExistingCurrentFruitTree(
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
                ExistingCurrentFruitTree,
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
            .Where(value =>
                value.OptionId == "foraging.harvest_fruit_tree" &&
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
                ExistingCurrentFruitTree,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                new[] { "matching_current_fruit_tree_candidate_not_present" },
                Array.Empty<string>());
        }

        return Result(
            route,
            "resolved_existing_current_fruit_tree_capacity_match",
            true,
            true,
            ExistingCurrentFruitTree,
            new[]
            {
                new AcquisitionFacilityTargetEvaluation(
                    candidate.LocationId,
                    1,
                    "resolved_existing_current_fruit_tree_capacity_match",
                    1,
                    0,
                    1,
                    0,
                    new[]
                    {
                        "candidate:foraging.harvest_fruit_tree",
                        "candidate.parameters[authoritative_route_sources_json]",
                        "state.current_location.terrain_features.value[].fruit_tree_expected_outputs"
                    },
                    Array.Empty<string>(),
                    TargetTileX: candidate.TargetTileX,
                    TargetTileY: candidate.TargetTileY)
            },
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
