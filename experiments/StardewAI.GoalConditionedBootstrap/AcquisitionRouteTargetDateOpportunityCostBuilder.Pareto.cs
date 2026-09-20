namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateOpportunityCostBuilder
{
    private static AcquisitionRouteTargetDateOpportunityCost[]
        ApplyParetoDominance(
            AcquisitionRouteTargetDateOpportunityCost[] preliminary)
    {
        var matched = preliminary.Where(route =>
                route.OpportunityCostAxisStatus ==
                    "resolved_opportunity_cost_vector_pending_pareto" &&
                route.CostVector is not null)
            .GroupBy(route => route.ComparisonGroupKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(route => route.RouteOccurrenceId,
                    StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return preliminary.Select(route =>
        {
            if (route.OpportunityCostAxisStatus !=
                    "resolved_opportunity_cost_vector_pending_pareto" ||
                route.CostVector is null)
            {
                return route;
            }
            var dominators = matched[route.ComparisonGroupKey]
                .Where(candidate => candidate.RouteOccurrenceId !=
                        route.RouteOccurrenceId &&
                    OpportunityCostDominates(
                        candidate.CostVector!,
                        route.CostVector))
                .Select(candidate => candidate.RouteOccurrenceId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return dominators.Length == 0
                ? route with
                {
                    OpportunityCostAxisStatus =
                        "resolved_opportunity_cost_pareto_frontier",
                    DominatedByRouteOccurrenceIds = Array.Empty<string>()
                }
                : route with
                {
                    OpportunityCostAxisStatus =
                        "resolved_opportunity_cost_pareto_dominated",
                    OpportunityCostMatchesTargetDate = false,
                    DominatedByRouteOccurrenceIds = dominators,
                    NonMatchingReasons = new[]
                    {
                        "strictly_pareto_dominated_within_requirement_alternative"
                    }
                };
        }).ToArray();
    }

    internal static bool OpportunityCostDominates(
        AcquisitionOpportunityCostVector candidate,
        AcquisitionOpportunityCostVector target)
    {
        var allNoWorse = candidate.GuaranteedElapsedGameMinutes <=
                target.GuaranteedElapsedGameMinutes &&
            candidate.RequiredEnergy <= target.RequiredEnergy;
        var anyBetter = candidate.GuaranteedElapsedGameMinutes <
                target.GuaranteedElapsedGameMinutes ||
            candidate.RequiredEnergy < target.RequiredEnergy;

        var candidateMaterials = MaterialDimensions(candidate);
        var targetMaterials = MaterialDimensions(target);
        foreach (var key in candidateMaterials.Keys.Concat(targetMaterials.Keys)
                     .Distinct())
        {
            var left = candidateMaterials.GetValueOrDefault(key);
            var right = targetMaterials.GetValueOrDefault(key);
            allNoWorse &= left <= right;
            anyBetter |= left < right;
        }
        var candidateCurrencies = CurrencyDimensions(candidate);
        var targetCurrencies = CurrencyDimensions(target);
        foreach (var key in candidateCurrencies.Keys.Concat(targetCurrencies.Keys)
                     .Distinct())
        {
            var left = candidateCurrencies.GetValueOrDefault(key);
            var right = targetCurrencies.GetValueOrDefault(key);
            allNoWorse &= left <= right;
            anyBetter |= left < right;
        }
        return allNoWorse && anyBetter;
    }

    private static IReadOnlyDictionary<MaterialDimension, int>
        MaterialDimensions(AcquisitionOpportunityCostVector vector) =>
        vector.MaterialCosts.ToDictionary(
            row => new MaterialDimension(
                row.QualifiedItemId,
                row.Quality,
                row.UnitSalePrice),
            row => row.Quantity);

    private static IReadOnlyDictionary<int, int> CurrencyDimensions(
        AcquisitionOpportunityCostVector vector) =>
        vector.CurrencyCosts.ToDictionary(
            row => row.CurrencyId,
            row => row.Amount);

    private sealed record MaterialDimension(
        string QualifiedItemId,
        int Quality,
        int UnitSalePrice);
}
