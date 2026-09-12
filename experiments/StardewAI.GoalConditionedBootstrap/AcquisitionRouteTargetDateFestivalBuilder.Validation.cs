namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFestivalBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateUnlockReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_unlock_state.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date unlock-state metadata is incomplete.");
        Require(source.UnlockAxisResolvedCount == source.Routes.Count(route =>
                    route.UnlockAxisResolved) &&
                source.UnlockStateMatchCount == source.Routes.Count(route =>
                    route.UnlockStateMatchesTargetDate == true) &&
                source.UnlockStateMissCount == source.Routes.Count(route =>
                    route.UnlockStateMatchesTargetDate == false) &&
                source.StaticWindowMissCount == source.Routes.Count(route =>
                    route.UnlockAxisStatus ==
                        "not_applicable_static_window_miss") &&
                source.BlockedUpstreamCalendarCount == source.Routes.Count(route =>
                    route.UnlockAxisStatus == "blocked_upstream_calendar") &&
                source.BlockedUnlockEvidenceCount == source.Routes.Count(route =>
                    route.StaticWindowMatchesTargetDate &&
                    !route.UnlockAxisResolved),
            "Target-date unlock-state counts drifted.");
    }
}
