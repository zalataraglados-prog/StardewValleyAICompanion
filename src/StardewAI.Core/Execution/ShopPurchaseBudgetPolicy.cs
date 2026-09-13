using System;

namespace StardewAI.Core.Execution;

public static class ShopPurchaseBudgetPolicy
{
    public const int MaximumMenuSafetyWaitTicks = 600;
    public const int InteractTicks = 30;
    public const int DialogueResponseTicks = 30;
    public const int PurchaseTicks = 20;
    public const int CloseMenuTicks = 10;

    public static int ConservativeGameMinutesForPurchases(int purchaseCount)
    {
        if (purchaseCount < 0)
            throw new ArgumentOutOfRangeException(nameof(purchaseCount));
        if (purchaseCount == 0)
            return 0;

        var ticksPerRollingPurchase = checked(
            MaximumMenuSafetyWaitTicks +
            InteractTicks +
            DialogueResponseTicks +
            PurchaseTicks +
            CloseMenuTicks);
        return (int)Math.Ceiling(
            purchaseCount * (double)ticksPerRollingPurchase * 1000d /
            (GameClockBudgetPolicy.RuntimeUpdatesPerSecond *
             GameClockBudgetPolicy.RealMillisecondsPerGameMinute));
    }
}
