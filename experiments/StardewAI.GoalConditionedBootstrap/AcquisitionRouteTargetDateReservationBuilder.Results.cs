namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private static AcquisitionRouteTargetDateReservation NotRequired(
        AcquisitionRouteTargetDateCurrency route) => Result(
        route,
        "resolved_inventory_reservation_not_required",
        true,
        true,
        "not_required",
        null,
        Array.Empty<string>(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateReservation Available(
        AcquisitionRouteTargetDateCurrency route,
        AcquisitionRouteReservationClaimSet claimSet,
        string disposition) => Result(
        route,
        disposition == "claim_already_committed"
            ? "resolved_inventory_reservation_claim_committed"
            : "resolved_inventory_reservation_claim_available",
        true,
        true,
        disposition,
        claimSet,
        Array.Empty<string>(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateReservation Conflict(
        AcquisitionRouteTargetDateCurrency route,
        params string[] reasons) => Result(
        route,
        "resolved_inventory_reservation_conflict",
        true,
        false,
        "conflict",
        null,
        reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateReservation Blocked(
        AcquisitionRouteTargetDateCurrency route,
        params string[] reasons) => Result(
        route,
        "blocked_inventory_reservation_evidence",
        false,
        null,
        "blocked",
        null,
        Array.Empty<string>(),
        reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());

    private static AcquisitionRouteTargetDateReservation Result(
        AcquisitionRouteTargetDateCurrency route,
        string status,
        bool resolved,
        bool? matches,
        string disposition,
        AcquisitionRouteReservationClaimSet? claimSet,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            status,
            resolved,
            matches,
            disposition,
            claimSet,
            nonMatchingReasons,
            blockingReasons);
}
