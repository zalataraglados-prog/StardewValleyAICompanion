namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveAnimalProductWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex candidates,
        int deadlineTotalDayExclusive)
    {
        if (!AnimalProductSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_animal_product_source_invalid",
                "animal_product_authoritative_source_identity_invalid");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_animal_product_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "farm.collect_animal_products" &&
                !string.IsNullOrWhiteSpace(candidate.LocationId) &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles is >= 0 &&
                candidate.EstimatedTicks > 0 &&
                !string.IsNullOrWhiteSpace(candidate.RequiredToolKind) &&
                candidate.ToolSlotIndex is >= 0)
            .ToArray();
        if (valid.Length == 0)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_animal_product_candidate_match",
                "matching_current_animal_product_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(out var totalDay, out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_animal_product_calendar_evidence",
                "current_animal_product_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current animal-product date must precede the deadline.");
        var windows = valid
            .Select(candidate => candidate.LocationId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(locationId => new AuthoritativeCalendarSourceWindow
            {
                SourceKind = route.RouteKind,
                SourceKey = route.SourceId,
                LocationId = locationId,
                RuleId = route.SourcePath,
                Season = season,
                FirstTotalDay = totalDay,
                LastTotalDay = totalDay,
                TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
                WeatherModes = NativeCalendarConstraintNormalizer.AllWeatherModes,
                RequiresLocationAccessEvidence = true,
                RequiresExistingLiveCandidateMatch = true,
                StochasticOutcome = false
            })
            .ToArray();
        return new CalendarSourceResolution(
            ResolvedStatus,
            "current_native_animal_product_candidate_calendar",
            windows,
            Array.Empty<string>());
    }

    internal static CalendarSourceResolution ResolveAnimalProductAbsentSourceWindow(
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        if (!AnimalProductSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_animal_product_source_invalid",
                "animal_product_authoritative_source_identity_invalid");
        }
        Require(deadlineTotalDayExclusive > 0,
            "Animal-product calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_animal_product_calendar_current_candidate_required",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = route.RouteKind,
                    SourceKey = route.SourceId,
                    RuleId = route.SourcePath,
                    FirstTotalDay = 0,
                    LastTotalDay = deadlineTotalDayExclusive - 1,
                    TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
                    WeatherModes = NativeCalendarConstraintNormalizer.AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = false
                }
            },
            Array.Empty<string>());
    }

    private static bool AnimalProductSourceIdentityMatches(
        AcquisitionRequirementRouteLowering route)
    {
        var listName = route.RouteKind switch
        {
            "native_farm_animal_produce" => "ProduceItemIds",
            "native_farm_animal_deluxe_produce" => "DeluxeProduceItemIds",
            _ => string.Empty
        };
        if (listName.Length == 0 || route.SourceAsset != "Data/FarmAnimals")
            return false;
        const string prefix = "farm_animal:";
        if (!route.SourceId.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var suffix = route.SourceId[prefix.Length..];
        var separator = suffix.LastIndexOf(':');
        if (separator <= 0 || separator == suffix.Length - 1 ||
            !int.TryParse(suffix[(separator + 1)..], out var rowIndex) ||
            rowIndex < 0)
        {
            return false;
        }
        var animalType = suffix[..separator];
        return route.SourcePath ==
            $"payload.{animalType}.{listName}[{rowIndex}].ItemId";
    }
}
