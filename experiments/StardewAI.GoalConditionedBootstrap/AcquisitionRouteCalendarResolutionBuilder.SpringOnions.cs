namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveSpringOnionWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex? candidates,
        int deadlineTotalDayExclusive)
    {
        if (qualifiedItemId != "(O)399" ||
            route.RouteKind != "native_spring_onion_harvest" ||
            route.SourceId != "Crop.harvest" ||
            route.SourceAsset != "decompiled native method" ||
            route.SourcePath != "whichForageCrop 1 => (O)399")
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_spring_onion_source_invalid",
                "spring_onion_authoritative_source_identity_invalid");
        }
        if (candidates is null)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_spring_onion_candidate_required",
                "current_spring_onion_candidate_evidence_required");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_spring_onion_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "foraging.harvest_spring_onions" &&
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
                "blocked_current_spring_onion_candidate_match",
                "matching_current_spring_onion_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(
                out var totalDay,
                out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_spring_onion_calendar_evidence",
                "current_spring_onion_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current spring-onion calendar date must precede the deadline.");
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
                WeatherModes =
                    NativeCalendarConstraintNormalizer.AllWeatherModes,
                RequiresLocationAccessEvidence = true,
                RequiresExistingLiveCandidateMatch = true,
                StochasticOutcome = false
            })
            .ToArray();
        return new CalendarSourceResolution(
            ResolvedStatus,
            "current_native_spring_onion_candidate_calendar",
            windows,
            Array.Empty<string>());
    }
}
