using System;

namespace StardewAI.Core.Execution;

public static class CropHarvestBudgetPolicy
{
    public const int HarvestTicksPerCrop = 60;

    public static int ConservativeGameMinutesForHarvests(int harvestCount)
    {
        if (harvestCount < 0)
            throw new ArgumentOutOfRangeException(nameof(harvestCount));
        if (harvestCount == 0)
            return 0;

        return GameClockBudgetPolicy.TicksToGameMinutes(
            checked(harvestCount * HarvestTicksPerCrop));
    }
}
