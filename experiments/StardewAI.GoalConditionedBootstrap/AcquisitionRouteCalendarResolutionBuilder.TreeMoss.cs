namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveTreeMossWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex candidates,
        int deadlineTotalDayExclusive)
    {
        if (qualifiedItemId != "(O)Moss" ||
            route.RouteKind != "native_tree_moss_harvest" ||
            route.SourceId != "Tree.CreateMossItem" ||
            route.SourceAsset != "decompiled native method" ||
            route.SourcePath != "(O)Moss")
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_tree_moss_source_invalid",
                "tree_moss_authoritative_source_identity_invalid");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_tree_moss_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "foraging.harvest_tree_moss" &&
                !string.IsNullOrWhiteSpace(candidate.LocationId) &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles is >= 0 &&
                candidate.MaxMovementTiles is >= 0 &&
                candidate.MaxToolSwings == 1 &&
                candidate.ExpectedOutputStack is > 0 &&
                candidate.ExpectedOutputQuality == 0 &&
                candidate.EstimatedTicks > 0 &&
                candidate.EnergyCost == 0)
            .ToArray();
        if (valid.Length == 0)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_tree_moss_candidate_match",
                "matching_current_tree_moss_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(out var totalDay, out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_tree_moss_calendar_evidence",
                "current_tree_moss_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current tree-moss date must precede the deadline.");
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
            "current_native_tree_moss_candidate_calendar",
            windows,
            Array.Empty<string>());
    }
}
