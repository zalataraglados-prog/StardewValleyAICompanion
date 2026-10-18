using System;

namespace StardewAI.Core.Execution;

public static class CropMaintenanceBudgetPolicy
{
    public const int MovementTicksPerTile = 30;
    public const int PlantTicksPerTile = 60;
    public const int WaterToolTicksPerTile = 5 + 60 + 20;
    public const double MinimumEnergyReserve = 1d;

    public static int PlantingTicks(int tileCount) =>
        CheckedProduct(tileCount, PlantTicksPerTile, nameof(tileCount));

    public static int WateringTicks(int tileCount) =>
        CheckedProduct(tileCount, WaterToolTicksPerTile, nameof(tileCount));

    public static int MovementTicks(int tileCount) =>
        CheckedProduct(tileCount, MovementTicksPerTile, nameof(tileCount));

    private static int CheckedProduct(
        int count,
        int ticksPerAction,
        string parameterName)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(parameterName);
        return checked(count * ticksPerAction);
    }
}
