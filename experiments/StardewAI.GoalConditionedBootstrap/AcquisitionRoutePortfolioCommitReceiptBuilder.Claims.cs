using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioCommitReceiptBuilder
{
    private static ExpectedPortfolioClaims ExpectedClaims(
        AcquisitionRouteTargetDateOpportunityCostReport opportunity,
        string[] selectedRouteIds,
        ICollection<string> reasons)
    {
        var routes = opportunity.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        var material = new List<MaterialReservationUpsertRequest>();
        var currency = new List<CurrencyReservationUpsertRequest>();
        var decisions = new List<ExpectedRouteClaims>();
        foreach (var routeId in selectedRouteIds)
        {
            if (!routes.TryGetValue(routeId, out var route))
            {
                reasons.Add("receipt_selected_route_not_found:" + routeId);
                continue;
            }
            var reservation = ReservationRoute(route);
            var decisionId = RouteDecisionPrefix + routeId;
            if (reservation.ClaimDisposition == "not_required")
            {
                if (reservation.ClaimSet is not null)
                {
                    reasons.Add("receipt_not_required_route_has_claim_set:" +
                        routeId);
                }
                decisions.Add(new ExpectedRouteClaims(
                    decisionId,
                    Array.Empty<string>()));
                continue;
            }
            if (reservation.ClaimDisposition is not (
                    "claim_proposed" or
                    "claim_already_committed" or
                    "claim_replacement_required") ||
                reservation.ClaimSet is null ||
                !reservation.ClaimSet.AtomicCommitRequired)
            {
                reasons.Add("receipt_route_claim_set_invalid:" + routeId);
                continue;
            }
            material.AddRange(reservation.ClaimSet.MaterialClaims);
            currency.AddRange(reservation.ClaimSet.CurrencyClaims);
            decisions.Add(new ExpectedRouteClaims(
                decisionId,
                reservation.ClaimSet.MaterialClaims
                    .Select(claim => claim.ReservationId)
                    .Concat(reservation.ClaimSet.CurrencyClaims.Select(claim =>
                        claim.ReservationId))
                    .Order(StringComparer.Ordinal)
                    .ToArray()));
        }
        var allIds = material.Select(claim => claim.ReservationId)
            .Concat(currency.Select(claim => claim.ReservationId))
            .ToArray();
        if (allIds.Distinct(StringComparer.Ordinal).Count() != allIds.Length)
            reasons.Add("receipt_expected_claim_ids_not_globally_unique");
        return new ExpectedPortfolioClaims(
            material.ToArray(),
            currency.ToArray(),
            decisions.ToArray());
    }

    private static AcquisitionRouteTargetDateReservation ReservationRoute(
        AcquisitionRouteTargetDateOpportunityCost route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute;

    private sealed record ExpectedPortfolioClaims(
        MaterialReservationUpsertRequest[] MaterialClaims,
        CurrencyReservationUpsertRequest[] CurrencyClaims,
        ExpectedRouteClaims[] Routes);

    private sealed record ExpectedRouteClaims(
        string SourceDecisionId,
        string[] ReservationIds);
}
