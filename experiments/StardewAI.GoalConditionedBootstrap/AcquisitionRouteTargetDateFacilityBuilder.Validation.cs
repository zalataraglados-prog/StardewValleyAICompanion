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
