namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerOpportunityCatalogBuilder
{
    private static MasterAnglerCalendarConstraint NormalizeCalendar(
        MasterAnglerFishDataConstraint fishConstraint,
        string spawnSeason,
        string condition,
        bool ignoreFishDataRequirements) =>
        NativeCalendarConstraintNormalizer.Normalize(
            ignoreFishDataRequirements || fishConstraint.Seasons.Length == 0
                ? NativeCalendarConstraintNormalizer.AllSeasons
                : fishConstraint.Seasons,
            ignoreFishDataRequirements || fishConstraint.TimeWindows.Length == 0
                ? NativeCalendarConstraintNormalizer.AllDay
                : fishConstraint.TimeWindows,
            ignoreFishDataRequirements
                ? NativeCalendarConstraintNormalizer.AllWeatherModes
                : NativeCalendarConstraintNormalizer.FishWeatherModes(
                    fishConstraint.Weather),
            spawnSeason,
            condition);
}
