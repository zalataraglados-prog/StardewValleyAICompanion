using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioBuilder
{
    private static ReservationPortfolioCommitRequest? BuildCommitRequest(
        AcquisitionRouteTargetDateOpportunityCostReport opportunity,
        AcquisitionRoutePortfolioProposal proposal,
        AcquisitionRouteTargetDateOpportunityCost[] selected,
        StrategyCommitmentLedger ledger,
        ICollection<string> reasons)
    {
        var materialClaims = new List<MaterialReservationUpsertRequest>();
        var currencyClaims = new List<CurrencyReservationUpsertRequest>();
        var releaseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in selected)
        {
            var reservation = ReservationRoute(route);
            switch (reservation.ClaimDisposition)
            {
                case "not_required":
                case "claim_already_committed":
                    break;
                case "claim_proposed":
                case "claim_replacement_required":
                    if (reservation.ClaimSet is null)
                    {
                        reasons.Add("selected_route_claim_set_missing:" +
                            route.RouteOccurrenceId);
                        continue;
                    }
                    materialClaims.AddRange(reservation.ClaimSet.MaterialClaims);
                    currencyClaims.AddRange(reservation.ClaimSet.CurrencyClaims);
                    if (reservation.ClaimDisposition ==
                        "claim_replacement_required")
                    {
                        var desired = reservation.ClaimSet.MaterialClaims
                            .Select(claim => claim.ReservationId)
                            .Concat(reservation.ClaimSet.CurrencyClaims.Select(
                                claim => claim.ReservationId))
                            .ToHashSet(StringComparer.Ordinal);
                        foreach (var id in reservation.ClaimSet
                                     .ExistingActiveReservationIds
                                     .Where(id => !desired.Contains(id)))
                        {
                            releaseIds.Add(id);
                        }
                    }
                    break;
                default:
                    reasons.Add("selected_route_claim_disposition_invalid:" +
                        route.RouteOccurrenceId);
                    break;
            }
        }

        var routeIds = opportunity.Routes.Select(route =>
                route.RouteOccurrenceId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var routeId in proposal.ReplacedRouteOccurrenceIds)
        {
            if (!routeIds.Contains(routeId))
            {
                reasons.Add("replaced_route_occurrence_not_found:" + routeId);
                continue;
            }
            var decisionId = RouteDecisionPrefix + routeId;
            var activeIds = ledger.MaterialReservations
                .Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceDecisionId == decisionId)
                .Select(row => row.ReservationId)
                .Concat(ledger.CurrencyReservations
                    .Where(row =>
                        row.Status == StrategyCommitmentStatuses.Active &&
                        row.SourceDecisionId == decisionId)
                    .Select(row => row.ReservationId))
                .ToArray();
            if (activeIds.Length == 0)
            {
                reasons.Add("replaced_route_has_no_active_reservations:" +
                    routeId);
            }
            foreach (var id in activeIds)
                releaseIds.Add(id);
        }
        if (reasons.Count > 0)
            return null;

        var desiredIds = materialClaims.Select(claim => claim.ReservationId)
            .Concat(currencyClaims.Select(claim => claim.ReservationId))
            .ToHashSet(StringComparer.Ordinal);
        if (releaseIds.Overlaps(desiredIds))
        {
            reasons.Add("portfolio_claim_release_overlap");
            return null;
        }
        if (materialClaims.Count == 0 &&
            currencyClaims.Count == 0 &&
            releaseIds.Count == 0)
        {
            return null;
        }
        return new ReservationPortfolioCommitRequest
        {
            StateHash = proposal.SnapshotStateHash,
            ExpectedLedgerRevision = proposal.ExpectedLedgerRevision,
            PortfolioId = "target-date-acquisition-portfolio:" +
                proposal.ProposalId,
            GoalId = proposal.GoalId,
            SourceDecisionId = "target-date-acquisition-portfolio:" +
                proposal.ProposalId,
            ReleaseReservationIds = releaseIds
                .Order(StringComparer.Ordinal)
                .ToArray(),
            MaterialClaims = materialClaims
                .OrderBy(claim => claim.ReservationId, StringComparer.Ordinal)
                .ToArray(),
            CurrencyClaims = currencyClaims
                .OrderBy(claim => claim.ReservationId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static AcquisitionOpportunityCostVector? AggregateCosts(
        AcquisitionRouteTargetDateOpportunityCost[] selected,
        ICollection<string> reasons)
    {
        try
        {
            var vectors = selected.Select(route => route.CostVector!).ToArray();
            var materials = vectors.SelectMany(vector => vector.MaterialCosts)
                .GroupBy(row => new
                {
                    row.QualifiedItemId,
                    row.Quality,
                    row.UnitSalePrice
                })
                .Select(group =>
                {
                    var quantity = checked(group.Sum(row => row.Quantity));
                    return new AcquisitionOpportunityMaterialCost(
                        group.Key.QualifiedItemId,
                        group.Key.Quality,
                        group.Key.UnitSalePrice,
                        quantity,
                        checked(quantity * group.Key.UnitSalePrice));
                })
                .OrderBy(row => row.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(row => row.Quality)
                .ThenBy(row => row.UnitSalePrice)
                .ToArray();
            var currencies = vectors.SelectMany(vector => vector.CurrencyCosts)
                .GroupBy(row => new { row.CurrencyId, row.CurrencyKey })
                .Select(group => new AcquisitionOpportunityCurrencyCost(
                    group.Key.CurrencyId,
                    group.Key.CurrencyKey,
                    checked(group.Sum(row => row.Amount))))
                .OrderBy(row => row.CurrencyId)
                .ToArray();
            return new AcquisitionOpportunityCostVector(
                checked(vectors.Sum(vector =>
                    vector.GuaranteedElapsedGameMinutes)),
                vectors.Sum(vector => vector.RequiredEnergy),
                checked(materials.Sum(row => row.TotalSaleValue)),
                materials,
                currencies,
                new[]
                {
                    "selected_routes[].cost_vector",
                    "authoritative_requirement_inventory.requirement_sets[].groups[]"
                });
        }
        catch (OverflowException)
        {
            reasons.Add("route_portfolio_aggregate_cost_overflow");
            return null;
        }
    }
}
