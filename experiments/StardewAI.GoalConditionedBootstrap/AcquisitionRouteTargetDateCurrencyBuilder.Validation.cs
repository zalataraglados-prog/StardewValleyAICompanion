using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateCurrencyBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateResourceReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_resource_inputs.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date resource-input metadata is incomplete.");
        Require(source.ResourceInputAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.ResourceInputAxisResolved) &&
                source.ResourceInputMatchCount == source.Routes.Count(route =>
                    route.ResourceInputsMatchTargetDate == true) &&
                source.ResourceInputMissCount == source.Routes.Count(route =>
                    route.ResourceInputsMatchTargetDate == false) &&
                source.ResourceInputNotRequiredCount ==
                    source.Routes.Count(route =>
                        route.ResourceInputAxisStatus ==
                            "resolved_resource_inputs_not_required") &&
                source.NotApplicableUpstreamCount == source.Routes.Count(route =>
                    route.ResourceInputAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.ResourceInputAxisStatus ==
                        "blocked_upstream_facility_capacity_axis") &&
                source.BlockedResourceEvidenceCount == source.Routes.Count(route =>
                    route.ResourceInputAxisStatus ==
                        "blocked_resource_input_evidence"),
            "Target-date resource-input counts drifted.");
        Require(source.Routes.All(route =>
                CurrencyClassByRouteKind.ContainsKey(RouteKind(route))),
            "A route kind lacks an explicit currency classification.");
    }

    private static IReadOnlyDictionary<string,
        AcquisitionRouteCalendarResolution> ValidateStaticSource(
            AcquisitionRouteCalendarResolutionReport source,
            AcquisitionRouteTargetDateResourceReport resources)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.RouteOccurrenceCount == resources.RouteOccurrenceCount &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Static calendar source metadata is incomplete.");
        var result = source.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        foreach (var route in resources.Routes)
        {
            Require(result.TryGetValue(route.RouteOccurrenceId, out var row) &&
                    row.RouteKind == RouteKind(route) &&
                    row.QualifiedItemId == QualifiedItemId(route) &&
                    row.SourceId == SourceId(route),
                "Static calendar source route identity drifted.");
        }
        return result;
    }

    private static string RouteKind(
        AcquisitionRouteTargetDateResource route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind;

    private static string QualifiedItemId(
        AcquisitionRouteTargetDateResource route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .QualifiedItemId;

    private static string SourceId(
        AcquisitionRouteTargetDateResource route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute.SourceId;

    private static JsonElement RequiredObject(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var result) &&
                result.ValueKind == JsonValueKind.Object,
            "Snapshot " + name + " is missing.");
        return result;
    }

    private static bool EqualJson<T>(T left, T right)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Serialize(left, options) ==
            JsonSerializer.Serialize(right, options);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
