using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateFacilityReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_facility_capacity.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date facility capacity metadata is incomplete.");
        Require(source.FacilityCapacityAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.FacilityCapacityAxisResolved) &&
                source.FacilityCapacityMatchCount == source.Routes.Count(route =>
                    route.FacilityCapacityMatchesTargetDate == true) &&
                source.FacilityCapacityMissCount == source.Routes.Count(route =>
                    route.FacilityCapacityMatchesTargetDate == false) &&
                source.FacilityCapacityNotRequiredCount ==
                    source.Routes.Count(route =>
                        route.FacilityCapacityAxisStatus ==
                            "resolved_facility_capacity_not_required") &&
                source.NotApplicableUpstreamCount == source.Routes.Count(route =>
                    route.FacilityCapacityAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.FacilityCapacityAxisStatus ==
                        "blocked_upstream_location_route_axis") &&
                source.BlockedFacilityEvidenceCount == source.Routes.Count(route =>
                    route.FacilityCapacityAxisStatus ==
                        "blocked_facility_capacity_evidence"),
            "Target-date facility capacity counts drifted.");
        Require(source.Routes.All(route =>
                ResourceClassByRouteKind.ContainsKey(RouteKind(route))),
            "A route kind lacks an explicit resource-input classification.");
    }

    private static IReadOnlyDictionary<string,
        AcquisitionRouteCalendarResolution> ValidateStaticSource(
            AcquisitionRouteCalendarResolutionReport source,
            AcquisitionRouteTargetDateFacilityReport facility)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.RouteOccurrenceCount == facility.RouteOccurrenceCount,
            "Static calendar source metadata is incomplete.");
        Require(source.Routes.Select(route => route.RouteOccurrenceId)
                .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Static calendar source contains duplicate route occurrences.");
        var result = source.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        foreach (var route in facility.Routes)
        {
            Require(result.TryGetValue(route.RouteOccurrenceId, out var row) &&
                    row.RouteKind == RouteKind(route) &&
                    row.QualifiedItemId == QualifiedItemId(route) &&
                    row.SourceId == SourceId(route) &&
                    row.MatchKind == MatchKind(route) &&
                    row.RequiredAmount == RequiredAmount(route) &&
                    row.MinimumQuality == MinimumQuality(route),
                "Static calendar source route requirement drifted.");
        }
        return result;
    }

    private static string RouteKind(
        AcquisitionRouteTargetDateFacility route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind;

    private static string QualifiedItemId(
        AcquisitionRouteTargetDateFacility route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.QualifiedItemId;

    private static string SourceId(
        AcquisitionRouteTargetDateFacility route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.SourceId;

    private static string MatchKind(
        AcquisitionRouteTargetDateFacility route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.MatchKind;

    private static int RequiredAmount(
        AcquisitionRouteTargetDateFacility route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RequiredAmount;

    private static int MinimumQuality(
        AcquisitionRouteTargetDateFacility route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.MinimumQuality;

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
