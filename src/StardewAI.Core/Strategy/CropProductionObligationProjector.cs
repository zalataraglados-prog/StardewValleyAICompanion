using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Strategy;

public sealed record CropProductionObligationProjection(
    int[] RequiredWateringTotalDays,
    int[] HarvestTotalDays,
    int MinimumUnitsPerWave,
    int MinimumHarvestWaveCount,
    int MinimumTotalUnits,
    long? MinimumGrossRevenue);

public static class CropProductionObligationProjector
{
    public static CropProductionObligationProjection Project(
        int plantingTotalDay,
        int firstHarvestTotalDay,
        int lastHarvestTotalDay,
        int? regrowDays,
        int tileCount,
        int harvestMinimumStack,
        bool needsWatering,
        int? harvestUnitSalePrice)
    {
        if (plantingTotalDay < 0 ||
            firstHarvestTotalDay <= plantingTotalDay ||
            lastHarvestTotalDay < firstHarvestTotalDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstHarvestTotalDay),
                "Crop production dates are not ordered.");
        }
        if (tileCount <= 0 || harvestMinimumStack <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tileCount),
                "Crop production quantity must be positive.");
        }
        if (harvestUnitSalePrice.HasValue && harvestUnitSalePrice.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(harvestUnitSalePrice),
                "Crop harvest sale price cannot be negative.");
        }

        var harvestDays = BuildHarvestDays(
            firstHarvestTotalDay,
            lastHarvestTotalDay,
            regrowDays);
        var wateringDays = needsWatering
            ? Enumerable.Range(
                    plantingTotalDay,
                    checked(lastHarvestTotalDay - plantingTotalDay))
                .ToArray()
            : Array.Empty<int>();
        var unitsPerWave = checked(tileCount * harvestMinimumStack);
        var totalUnits = checked(unitsPerWave * harvestDays.Length);
        return new CropProductionObligationProjection(
            wateringDays,
            harvestDays,
            unitsPerWave,
            harvestDays.Length,
            totalUnits,
            harvestUnitSalePrice.HasValue
                ? checked((long)totalUnits * harvestUnitSalePrice.Value)
                : null);
    }

    private static int[] BuildHarvestDays(
        int firstHarvestTotalDay,
        int lastHarvestTotalDay,
        int? regrowDays)
    {
        if (lastHarvestTotalDay == firstHarvestTotalDay)
        {
            return new[] { firstHarvestTotalDay };
        }
        if (!regrowDays.HasValue || regrowDays.Value <= 0 ||
            (lastHarvestTotalDay - firstHarvestTotalDay) %
                regrowDays.Value != 0)
        {
            throw new ArgumentException(
                "A repeated harvest window requires one exact regrow interval.",
                nameof(regrowDays));
        }

        var result = new List<int>();
        for (var day = firstHarvestTotalDay;
             day <= lastHarvestTotalDay;
             day = checked(day + regrowDays.Value))
        {
            result.Add(day);
        }
        return result.ToArray();
    }
}
