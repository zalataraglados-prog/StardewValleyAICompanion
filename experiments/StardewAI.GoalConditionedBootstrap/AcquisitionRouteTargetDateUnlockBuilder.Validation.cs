namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateUnlockBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateCalendarReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_calendar.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date calendar metadata is incomplete.");
        Require(source.CalendarAxisResolvedCount == source.Routes.Count(route =>
                    route.CalendarAxisResolved) &&
                source.StaticWindowMatchCount == source.Routes.Count(route =>
                    route.StaticWindowMatchesTargetDate) &&
                source.StaticWindowMissCount == source.Routes.Count(route =>
                    route.CalendarAxisResolved &&
                    !route.StaticWindowMatchesTargetDate) &&
                source.BlockedStaticSourceCount == source.Routes.Count(route =>
                    !route.CalendarAxisResolved),
            "Target-date calendar counts drifted.");
        Require(source.Routes.All(route =>
                !string.IsNullOrWhiteSpace(route.MatchKind) &&
                route.RequiredAmount > 0 &&
                route.MinimumQuality >= 0),
            "A target-date route lost its requirement amount or quality contract.");
    }

}
