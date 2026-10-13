namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static CalendarSourceResolution ResolveFishPondOutputWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        AcquisitionCurrentRouteCandidateIndex candidates,
        int deadlineTotalDayExclusive)
    {
        if (!FishPondSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_fish_pond_source_invalid",
                "fish_pond_authoritative_source_identity_invalid");
        }
        if (!candidates.TryFindCanonical(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_fish_pond_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        var valid = matches.Where(candidate =>
                candidate.OptionId == "fishing.service_fish_ponds" &&
                !string.IsNullOrWhiteSpace(candidate.LocationId) &&
                candidate.TargetTileX.HasValue &&
                candidate.TargetTileY.HasValue &&
                candidate.StandTileX.HasValue &&
                candidate.StandTileY.HasValue &&
                candidate.RouteDistanceTiles is >= 0 &&
                candidate.EstimatedTicks >
                    candidate.RouteDistanceTiles.Value * 60 &&
                candidate.EnergyCost == 0 &&
                candidate.ExpectedOutputStack is > 0 &&
                candidate.ExpectedOutputQuality is >= 0)
            .ToArray();
        if (valid.Length == 0)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_fish_pond_candidate_match",
                "matching_current_fish_pond_candidate_not_present");
        }
        if (!candidates.TryGetCurrentCalendar(out var totalDay, out var season))
        {
            return BlockLiveSourceCalendar(
                "blocked_current_fish_pond_calendar_evidence",
                "current_fish_pond_calendar_evidence_incomplete");
        }

        Require(deadlineTotalDayExclusive > 0 &&
                totalDay >= 0 &&
                totalDay < deadlineTotalDayExclusive,
            "Current fish-pond date must precede the deadline.");
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
            "current_native_fish_pond_output_candidate_calendar",
            windows,
            Array.Empty<string>());
    }

    private static CalendarSourceResolution
        ResolveFishPondOutputAbsentSourceWindow(
            AcquisitionRequirementRouteLowering route,
            int deadlineTotalDayExclusive)
    {
        if (!FishPondSourceIdentityMatches(route))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_fish_pond_source_invalid",
                "fish_pond_authoritative_source_identity_invalid");
        }
        Require(deadlineTotalDayExclusive > 0,
            "Fish-pond calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_fish_pond_output_row_calendar_invariant",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = "fish_pond_output_row",
                    SourceKey = route.SourceId,
                    RuleId = route.SourcePath,
                    FirstTotalDay = 0,
                    LastTotalDay = deadlineTotalDayExclusive - 1,
                    TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
                    WeatherModes =
                        NativeCalendarConstraintNormalizer.AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = true
                }
            },
            Array.Empty<string>());
    }

    private static bool FishPondSourceIdentityMatches(
        AcquisitionRequirementRouteLowering route)
    {
        const string sourcePrefix = "fish_pond:";
        if (route.SourceAsset != "Data/FishPondData" ||
            !route.SourceId.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceIdentity = route.SourceId[sourcePrefix.Length..];
        var randomMarker = sourceIdentity.IndexOf(
            ":random:",
            StringComparison.Ordinal);
        var baseIdentity = randomMarker >= 0
            ? sourceIdentity[..randomMarker]
            : sourceIdentity;
        var outputSeparator = baseIdentity.LastIndexOf(':');
        var pondId = outputSeparator > 0
            ? baseIdentity[..outputSeparator]
            : string.Empty;
        var outputIndexText = outputSeparator > 0
            ? baseIdentity[(outputSeparator + 1)..]
            : string.Empty;
        var randomIndexText = randomMarker >= 0
            ? sourceIdentity[(randomMarker + ":random:".Length)..]
            : string.Empty;
        var pathSuffix = randomMarker >= 0
            ? "].RandomItemId[" + randomIndexText + "]"
            : "].ItemId";
        const string pathPrefix = "payload[";
        const string producedItemsMarker = "].ProducedItems[";
        var pondIndexEnd = route.SourcePath.IndexOf(
            producedItemsMarker,
            StringComparison.Ordinal);
        var pondIndexText = pondIndexEnd > pathPrefix.Length
            ? route.SourcePath[pathPrefix.Length..pondIndexEnd]
            : string.Empty;
        var expectedPath = pathPrefix + pondIndexText +
            producedItemsMarker + outputIndexText + pathSuffix;
        if (string.IsNullOrWhiteSpace(pondId) ||
            !int.TryParse(outputIndexText, out var outputIndex) ||
            outputIndex < 0 ||
            randomMarker >= 0 &&
            (!int.TryParse(randomIndexText, out var randomIndex) ||
             randomIndex < 0) ||
            !int.TryParse(pondIndexText, out var pondIndex) ||
            pondIndex < 0 ||
            route.SourcePath != expectedPath)
        {
            return false;
        }
        return true;
    }
}
