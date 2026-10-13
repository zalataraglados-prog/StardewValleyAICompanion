namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static AcquisitionRouteTargetDateFacility
        EvaluateExistingCurrentFishPond(
            AcquisitionRouteTargetDateLocation route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionCurrentRouteCandidateIndex candidates)
    {
        if (!candidates.TryFindCanonical(
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
                ExistingCurrentFishPond,
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
                value.OptionId == "fishing.service_fish_ponds" &&
                value.ExpectedOutputStack >= staticRoute.RequiredAmount &&
                value.ExpectedOutputQuality >= staticRoute.MinimumQuality &&
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
                ExistingCurrentFishPond,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                new[] { "matching_current_fish_pond_candidate_not_present" },
                Array.Empty<string>());
        }

        return Result(
            route,
            "resolved_existing_current_fish_pond_capacity_match",
            true,
            true,
            ExistingCurrentFishPond,
            new[]
            {
                new AcquisitionFacilityTargetEvaluation(
                    candidate.LocationId,
                    1,
                    "resolved_existing_current_fish_pond_capacity_match",
                    1,
                    0,
                    1,
                    0,
                    new[]
                    {
                        "candidate:fishing.service_fish_ponds",
                        "candidate.parameters[authoritative_route_sources_json]",
                        "candidate.parameters[expected_output_quality]",
                        "state.farm.buildings.value[].fish_pond.output_items_json"
                    },
                    Array.Empty<string>(),
                    TargetTileX: candidate.TargetTileX,
                    TargetTileY: candidate.TargetTileY)
            },
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
