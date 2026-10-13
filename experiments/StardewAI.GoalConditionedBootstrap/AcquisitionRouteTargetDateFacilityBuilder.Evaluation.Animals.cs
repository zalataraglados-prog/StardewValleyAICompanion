namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static AcquisitionRouteTargetDateFacility
        EvaluateExistingCurrentFarmAnimal(
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
                ExistingCurrentFarmAnimal,
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
                value.OptionId == "farm.collect_animal_products" &&
                !string.IsNullOrWhiteSpace(value.RequiredToolKind) &&
                value.ToolSlotIndex is >= 0 &&
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
                ExistingCurrentFarmAnimal,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                new[]
                {
                    "matching_current_farm_animal_candidate_not_present"
                },
                Array.Empty<string>());
        }

        return Result(
            route,
            "resolved_existing_current_farm_animal_capacity_match",
            true,
            true,
            ExistingCurrentFarmAnimal,
            new[]
            {
                new AcquisitionFacilityTargetEvaluation(
                    candidate.LocationId,
                    1,
                    "resolved_existing_current_farm_animal_capacity_match",
                    1,
                    0,
                    1,
                    0,
                    new[]
                    {
                        "candidate:farm.collect_animal_products",
                        "candidate.parameters[authoritative_route_sources_json]",
                        "candidate.parameters[required_tool_kind]",
                        "candidate.parameters[tool_slot_index]",
                        "state.farm.animals.value[].harvest_status"
                    },
                    Array.Empty<string>(),
                    TargetTileX: candidate.TargetTileX,
                    TargetTileY: candidate.TargetTileY)
            },
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
