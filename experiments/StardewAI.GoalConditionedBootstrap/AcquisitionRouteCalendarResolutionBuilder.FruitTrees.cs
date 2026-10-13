namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveFruitTreeWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex candidates,
        int deadlineTotalDayExclusive)
    {
        if (!FruitTreeSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_fruit_tree_source_invalid",
                "fruit_tree_authoritative_source_identity_invalid");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_fruit_tree_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "foraging.harvest_fruit_tree" &&
                !string.IsNullOrWhiteSpace(candidate.LocationId) &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles.HasValue &&
                candidate.RouteDistanceTiles.Value >= 0 &&
                candidate.EstimatedTicks > 0 &&
                candidate.EnergyCost == 0)
            .ToArray();
        if (valid.Length == 0)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_fruit_tree_candidate_match",
                "matching_current_fruit_tree_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(out var totalDay, out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_fruit_tree_calendar_evidence",
                "current_fruit_tree_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current fruit-tree date must precede the deadline.");
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
            "current_native_fruit_tree_candidate_calendar",
            windows,
            Array.Empty<string>());
    }

    internal static CalendarSourceResolution ResolveFruitTreeAbsentSourceWindow(
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        if (!FruitTreeSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_fruit_tree_source_invalid",
                "fruit_tree_authoritative_source_identity_invalid");
        }
        Require(deadlineTotalDayExclusive > 0,
            "Fruit-tree calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_fruit_tree_calendar_current_candidate_required",
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
                    WeatherModes =
                        NativeCalendarConstraintNormalizer.AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = false
                }
            },
            Array.Empty<string>());
    }

    private static bool FruitTreeSourceIdentityMatches(
        AcquisitionRequirementRouteLowering route)
    {
        if (route.RouteKind != "native_fruit_tree_produce" ||
            route.SourceAsset != "Data/FruitTrees")
        {
            return false;
        }
        var parts = route.SourceId.Split(':');
        if (parts.Length == 3 &&
            parts[0] == "fruit_tree" &&
            !string.IsNullOrWhiteSpace(parts[1]) &&
            int.TryParse(parts[2], out var rowIndex) &&
            rowIndex >= 0)
        {
            return route.SourcePath ==
                $"payload.{parts[1]}.Fruit[{rowIndex}].ItemId";
        }
        if (parts.Length == 5 &&
            parts[0] == "fruit_tree" &&
            !string.IsNullOrWhiteSpace(parts[1]) &&
            int.TryParse(parts[2], out rowIndex) &&
            rowIndex >= 0 &&
            parts[3] == "random" &&
            int.TryParse(parts[4], out var randomIndex) &&
            randomIndex >= 0)
        {
            return route.SourcePath ==
                $"payload.{parts[1]}.Fruit[{rowIndex}].RandomItemId[{randomIndex}]";
        }
        return false;
    }
}
