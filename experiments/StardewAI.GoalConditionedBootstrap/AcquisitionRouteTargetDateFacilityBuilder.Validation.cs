using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateLocationReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_location_route.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date location-route metadata is incomplete.");
        Require(source.LocationRouteAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.LocationRouteAxisResolved) &&
                source.LocationRouteMatchCount == source.Routes.Count(route =>
                    route.LocationRouteMatchesTargetDate == true) &&
                source.LocationRouteMissCount == source.Routes.Count(route =>
                    route.LocationRouteMatchesTargetDate == false) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.LocationRouteAxisStatus ==
                        "blocked_upstream_calendar_condition_axis") &&
                source.BlockedLocationEvidenceCount == source.Routes.Count(route =>
                    route.LocationRouteAxisStatus ==
                        "blocked_location_route_evidence"),
            "Target-date location-route counts drifted.");
        Require(source.Routes.All(route =>
                FacilityClassByRouteKind.ContainsKey(
                    route.UpstreamRoute.UpstreamRoute.RouteKind)),
            "A route kind lacks an explicit facility-capacity classification.");
    }

    private static IReadOnlyDictionary<string,
        AcquisitionRouteCalendarResolution> ValidateStaticSource(
            AcquisitionRouteCalendarResolutionReport source,
            AcquisitionRouteTargetDateLocationReport locations)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.RouteOccurrenceCount == locations.RouteOccurrenceCount,
            "Static calendar source metadata is incomplete.");
        var result = source.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        Require(result.Count == source.Routes.Length,
            "Static calendar source contains duplicate route occurrences.");
        foreach (var route in locations.Routes)
        {
            var target = route.UpstreamRoute.UpstreamRoute;
            Require(result.TryGetValue(route.RouteOccurrenceId, out var row) &&
                    row.RouteKind == target.RouteKind &&
                    row.QualifiedItemId == target.QualifiedItemId &&
                    row.SourceId == target.SourceId &&
                    row.MatchKind == target.MatchKind &&
                    row.RequiredAmount == target.RequiredAmount &&
                    row.MinimumQuality == target.MinimumQuality,
                "Static calendar source route requirement drifted.");
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
