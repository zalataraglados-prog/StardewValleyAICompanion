namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveRadioactiveOreNodeWindow(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        int deadlineTotalDayExclusive)
    {
        if (qualifiedItemId != "(O)909" ||
            route.SourceId != "GameLocation.breakStone" ||
            route.SourceAsset != "decompiled native method" ||
            route.SourcePath != "stone 95 => (O)909")
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_radioactive_ore_node_source_invalid",
                "radioactive_ore_node_authoritative_source_identity_invalid");
        }

        Require(deadlineTotalDayExclusive > 0,
            "Radioactive-ore-node calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "native_radioactive_ore_node_calendar_invariant",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = "radioactive_ore_node",
                    SourceKey = route.SourceId,
                    RuleId = "stone:95",
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
