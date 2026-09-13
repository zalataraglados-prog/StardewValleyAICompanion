using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateLocationBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateFestivalReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_festival_state.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date festival-state metadata is incomplete.");
        Require(source.CalendarConditionAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.CalendarConditionAxisResolved) &&
                source.CalendarConditionMatchCount == source.Routes.Count(route =>
                    route.CalendarConditionsMatchTargetDate == true) &&
                source.CalendarConditionMissCount == source.Routes.Count(route =>
                    route.CalendarConditionsMatchTargetDate == false) &&
                source.NotApplicableStaticWindowCount == source.Routes.Count(route =>
                    route.CalendarConditionAxisStatus ==
                        "not_applicable_static_window_miss") &&
                source.NotApplicableUnlockStateCount == source.Routes.Count(route =>
                    route.CalendarConditionAxisStatus ==
                        "not_applicable_unlock_state_miss") &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.CalendarConditionAxisStatus ==
                        "blocked_upstream_unlock_axis") &&
                source.BlockedCalendarEvidenceCount == source.Routes.Count(route =>
                    route.CalendarConditionAxisStatus ==
                        "blocked_calendar_evidence"),
            "Target-date festival-state counts drifted.");
    }

    private static IReadOnlyDictionary<string,
        AcquisitionRouteCalendarResolution> ValidateStaticSource(
            AcquisitionRouteTargetDateFestivalReport source,
            AcquisitionRouteCalendarResolutionReport staticReport)
    {
        Require(staticReport.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                staticReport.RouteOccurrenceInventoryComplete &&
                !staticReport.TrainingLabelEligible &&
                staticReport.RouteOccurrenceCount == staticReport.Routes.Length &&
                staticReport.GameVersion == source.GameVersion &&
                staticReport.GoalId == source.GoalId,
            "Static calendar resolution metadata is incomplete.");
        var result = staticReport.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        Require(result.Count == source.Routes.Length,
            "Static and target-date route occurrence counts disagree.");
        foreach (var route in source.Routes)
        {
            Require(result.TryGetValue(route.RouteOccurrenceId, out var value) &&
                    value.QualifiedItemId == route.UpstreamRoute.QualifiedItemId &&
                    value.RouteKind == route.UpstreamRoute.RouteKind &&
                    value.SourceId == route.UpstreamRoute.SourceId,
                "Static source identity disagrees with a target-date route occurrence.");
        }
        return result;
    }

    private static bool EqualJson<T>(T left, T right)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return string.Equals(
            JsonSerializer.Serialize(left, options),
            JsonSerializer.Serialize(right, options),
            StringComparison.Ordinal);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
