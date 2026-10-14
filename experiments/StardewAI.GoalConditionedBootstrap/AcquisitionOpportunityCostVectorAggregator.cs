namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionOpportunityCostVectorAggregator
{
    internal static AcquisitionOpportunityCostVector Aggregate(
        IEnumerable<AcquisitionOpportunityCostVector> values,
        IEnumerable<string> evidencePaths)
    {
        var vectors = values?.ToArray() ??
            throw new InvalidDataException(
                "Opportunity-cost vectors are required.");
        var evidence = evidencePaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        if (vectors.Length == 0 || evidence.Length == 0)
        {
            throw new InvalidDataException(
                "Opportunity-cost aggregation has no vectors or evidence.");
        }

        var materials = vectors
            .SelectMany(vector => vector.MaterialCosts ??
                Array.Empty<AcquisitionOpportunityMaterialCost>())
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
        var currencies = vectors
            .SelectMany(vector => vector.CurrencyCosts ??
                Array.Empty<AcquisitionOpportunityCurrencyCost>())
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
            evidence);
    }
}
