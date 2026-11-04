using System;

namespace StardewAI.Contracts.Execution;

public static class MaterialTransferBudgetPolicy
{
    public const int MenuWaitTicks = 180;
    public const int UnlockWaitTicks = 120;
    public const int MovementTicksPerTile = 16;

    public static int InteractionTicks(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        // Each wait may succeed at limit + 1; Move-at-stand, Open, click completion and Close each take one update.
        return (int)Math.Min(int.MaxValue, (long)MenuWaitTicks + 1 + UnlockWaitTicks + 1 + 4 + quantity);
    }

    public static int EstimatedTicks(int distance, int quantity) =>
        (int)Math.Min(int.MaxValue, (long)Math.Max(0, distance) * MovementTicksPerTile + InteractionTicks(quantity));
}
