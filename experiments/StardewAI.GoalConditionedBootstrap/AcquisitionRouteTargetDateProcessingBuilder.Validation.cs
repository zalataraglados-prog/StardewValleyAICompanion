using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateReservationReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_inventory_reservation.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date inventory-reservation metadata is incomplete.");
        Require(source.ReservationAxisResolvedCount == source.Routes.Count(
                    route => route.ReservationAxisResolved) &&
                source.ReservationMatchCount == source.Routes.Count(route =>
                    route.InventoryReservationMatchesTargetDate == true) &&
                source.ReservationConflictCount == source.Routes.Count(route =>
                    route.InventoryReservationMatchesTargetDate == false) &&
                source.NotApplicableUpstreamCount == source.Routes.Count(route =>
                    route.ReservationAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.ReservationAxisStatus ==
                        "blocked_upstream_currency_budget_axis") &&
                source.BlockedReservationEvidenceCount == source.Routes.Count(
                    route => route.ReservationAxisStatus ==
                        "blocked_inventory_reservation_evidence"),
            "Target-date inventory-reservation counts drifted.");
        Require(source.Routes.All(route =>
                ProcessingClassByRouteKind.ContainsKey(RouteKind(route))),
            "A reservation route kind lacks processing-lead-time classification.");
        Require(ProcessingClassByRouteKind.Count == 33,
            "Processing-lead-time route-kind inventory drifted.");
    }

    private static void ValidateStaticSource(
        AcquisitionRouteCalendarResolutionReport staticSource,
        AcquisitionRouteTargetDateReservationReport reservationSource)
    {
        Require(staticSource.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                staticSource.RouteOccurrenceInventoryComplete &&
                !staticSource.TrainingLabelEligible &&
                staticSource.GoalId == reservationSource.GoalId &&
                staticSource.GameVersion == reservationSource.GameVersion &&
                staticSource.RouteOccurrenceCount == staticSource.Routes.Length &&
                staticSource.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    staticSource.Routes.Length,
            "Static calendar source metadata is incomplete.");
        var staticIds = staticSource.Routes.Select(route =>
                route.RouteOccurrenceId)
            .ToHashSet(StringComparer.Ordinal);
        Require(staticIds.SetEquals(reservationSource.Routes.Select(route =>
                    route.RouteOccurrenceId)),
            "Static calendar and reservation route inventories disagree.");
        foreach (var route in reservationSource.Routes)
        {
            var staticRoute = staticSource.Routes.Single(value =>
                value.RouteOccurrenceId == route.RouteOccurrenceId);
            var requirement = RequirementRoute(route);
            Require(staticRoute.RouteKind == RouteKind(route) &&
                    staticRoute.QualifiedItemId == QualifiedItemId(route) &&
                    staticRoute.SourceId == requirement.SourceId &&
                    staticRoute.MatchKind == requirement.MatchKind &&
                    staticRoute.RequiredAmount == requirement.RequiredAmount &&
                    staticRoute.MinimumQuality == requirement.MinimumQuality &&
                    staticRoute.UncertaintyMode == requirement.UncertaintyMode,
                "Static and reservation route requirement disagrees: " +
                route.RouteOccurrenceId);
        }
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
