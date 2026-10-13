namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveWildTreeProductWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex candidates,
        int deadlineTotalDayExclusive)
    {
        if (!WildTreeProductSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_wild_tree_product_source_invalid",
                "wild_tree_product_authoritative_source_identity_invalid");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_wild_tree_product_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "foraging.harvest_tree_product" &&
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
                "blocked_current_wild_tree_product_candidate_match",
                "matching_current_wild_tree_product_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(out var totalDay, out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_wild_tree_product_calendar_evidence",
                "current_wild_tree_product_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current wild-tree product date must precede the deadline.");
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
            "current_native_wild_tree_product_candidate_calendar",
            windows,
            Array.Empty<string>());
    }

    internal static CalendarSourceResolution
        ResolveWildTreeProductAbsentSourceWindow(
            AcquisitionRequirementRouteLowering route,
            int deadlineTotalDayExclusive)
    {
        if (!WildTreeProductSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_wild_tree_product_source_invalid",
                "wild_tree_product_authoritative_source_identity_invalid");
        }
        Require(deadlineTotalDayExclusive > 0,
            "Wild-tree product calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_wild_tree_product_calendar_current_candidate_required",
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
                    WeatherModes = NativeCalendarConstraintNormalizer
                        .AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = false
                }
            },
            Array.Empty<string>());
    }

    private static bool WildTreeProductSourceIdentityMatches(
        AcquisitionRequirementRouteLowering route)
    {
        if (route.SourceAsset != "Data/WildTrees")
            return false;
        var parts = route.SourceId.Split(':');
        if (route.RouteKind == "native_wild_tree_seed" &&
            parts.Length == 2 &&
            parts[0] == "wild_tree" &&
            !string.IsNullOrWhiteSpace(parts[1]))
        {
            return route.SourcePath ==
                $"payload.{parts[1]}.SeedItemId";
        }
        if (route.RouteKind == "native_wild_tree_seed_drop" &&
            parts.Length == 3 &&
            parts[0] == "wild_tree" &&
            !string.IsNullOrWhiteSpace(parts[1]) &&
            int.TryParse(parts[2], out var rowIndex) &&
            rowIndex >= 0)
        {
            return route.SourcePath ==
                $"payload.{parts[1]}.SeedDropItems[{rowIndex}].ItemId";
        }
        return false;
    }
}
