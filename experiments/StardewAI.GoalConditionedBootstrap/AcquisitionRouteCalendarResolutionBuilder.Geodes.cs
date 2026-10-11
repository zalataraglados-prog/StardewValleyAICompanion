namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static CalendarSourceResolution ResolveGeodeDropWindow(
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        const string prefix = "geode:";
        if (route.SourceAsset != "Data/Objects" ||
            !route.SourceId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_geode_source_invalid",
                "geode_authoritative_source_identity_invalid");
        }

        var parts = route.SourceId[prefix.Length..].Split(':');
        var direct = parts.Length == 2 &&
            TryParseGeodeItemId(parts[0]) &&
            TryParseNonNegativeIndex(parts[1]);
        var random = parts.Length == 4 &&
            parts[2] == "random" &&
            TryParseGeodeItemId(parts[0]) &&
            TryParseNonNegativeIndex(parts[1]) &&
            TryParseNonNegativeIndex(parts[3]);
        var expectedPath = direct
            ? "payload." + parts[0] + ".GeodeDrops[" + parts[1] +
                "].ItemId"
            : random
                ? "payload." + parts[0] + ".GeodeDrops[" + parts[1] +
                    "].RandomItemId[" + parts[3] + "]"
                : string.Empty;
        if (string.IsNullOrEmpty(expectedPath) ||
            route.SourcePath != expectedPath)
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_geode_source_invalid",
                "geode_authoritative_source_path_mismatch");
        }

        Require(deadlineTotalDayExclusive > 0,
            "Geode calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "authoritative_geode_drop_row_calendar_invariant",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = "geode_drop_row",
                    SourceKey = route.SourceId,
                    RuleId = string.Join(':', parts.Skip(1)),
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

    private static bool TryParseGeodeItemId(string value) =>
        uint.TryParse(
            value,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out _);

    private static bool TryParseNonNegativeIndex(string value) =>
        int.TryParse(
            value,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) &&
        parsed >= 0;
}
