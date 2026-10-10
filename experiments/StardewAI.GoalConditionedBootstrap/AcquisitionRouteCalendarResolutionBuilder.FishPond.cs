namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static CalendarSourceResolution ResolveFishPondOutputWindow(
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        const string sourcePrefix = "fish_pond:";
        if (route.SourceAsset != "Data/FishPondData" ||
            !route.SourceId.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_fish_pond_source_invalid",
                "fish_pond_authoritative_source_identity_invalid");
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
            return BlockLiveSourceCalendar(
                "blocked_authoritative_fish_pond_source_invalid",
                "fish_pond_authoritative_source_path_mismatch");
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
}
