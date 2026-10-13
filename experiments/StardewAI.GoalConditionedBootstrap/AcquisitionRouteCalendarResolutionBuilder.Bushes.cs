namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveBushWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex? candidates,
        int deadlineTotalDayExclusive)
    {
        var expectedPath = route.RouteKind switch
        {
            "native_bush_shake" when qualifiedItemId == "(O)296" =>
                "Season.Spring => (O)296",
            "native_tea_bush_harvest" when qualifiedItemId == "(O)815" =>
                "size 3 => (O)815",
            _ => string.Empty
        };
        if (route.SourceId != "Bush.GetShakeOffItem" ||
            route.SourceAsset != "decompiled native method" ||
            string.IsNullOrWhiteSpace(expectedPath) ||
            route.SourcePath != expectedPath)
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_bush_source_invalid",
                "bush_authoritative_source_identity_invalid");
        }
        if (candidates is null)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_bush_candidate_required",
                "current_bush_candidate_evidence_required");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_bush_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "foraging.harvest_bushes" &&
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
                "blocked_current_bush_candidate_match",
                "matching_current_bush_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(
                out var totalDay,
                out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_bush_calendar_evidence",
                "current_bush_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current bush calendar date must precede the deadline.");
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
            "current_native_bush_candidate_calendar",
            windows,
            Array.Empty<string>());
    }
}
