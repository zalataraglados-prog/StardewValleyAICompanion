using System;

namespace StardewAI.Core.Execution;

public static class MachineInteractionBudgetPolicy
{
    public const int CollectOutputTicks = 30;
    public const int LoadInputTicks = 30;

    public static int ConservativeGameMinutesForCollects(int count) =>
        ConservativeGameMinutes(count, CollectOutputTicks);

    public static int ConservativeGameMinutesForLoads(int count) =>
        ConservativeGameMinutes(count, LoadInputTicks);

    private static int ConservativeGameMinutes(int count, int ticksPerAction)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        return count == 0
            ? 0
            : checked(count * GameClockBudgetPolicy.TicksToGameMinutes(
                ticksPerAction));
    }
}
