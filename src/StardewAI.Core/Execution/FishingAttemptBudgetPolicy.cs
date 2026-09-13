using System;

namespace StardewAI.Core.Execution;

public static class FishingAttemptBudgetPolicy
{
    public const int NativeMaximumBiteMilliseconds = 30_000;
    public const int MaximumCastChargeMilliseconds = 1_000;
    public const int CastChosenCountdownMilliseconds = 350;
    public const int HookToMinigameMilliseconds = 1_500;
    public const int NativeUpdatesPerSecond = 60;
    public const int OrdinaryPerfectLockUpdates = 351;
    public const int ChallengeBaitPerfectLockUpdates = 451;
    public const int PullAnimationUpperBoundMilliseconds = 1_200;

    public static int ConservativeAttemptMilliseconds(
        bool challengeBait)
    {
        var perfectLockUpdates = challengeBait
            ? ChallengeBaitPerfectLockUpdates
            : OrdinaryPerfectLockUpdates;
        var perfectLockMilliseconds = (int)Math.Ceiling(
            perfectLockUpdates * 1_000d / NativeUpdatesPerSecond);
        var successSettlementMilliseconds = (int)Math.Ceiling(
            1_500d + 20d * 1_000d / NativeUpdatesPerSecond);

        return MaximumCastChargeMilliseconds +
               CastChosenCountdownMilliseconds +
               CastFlightUpperBoundMilliseconds() +
               FirstCastBiteUpperBoundMilliseconds() +
               OneNativeUpdateMilliseconds() +
               HookToMinigameMilliseconds +
               perfectLockMilliseconds +
               successSettlementMilliseconds +
               PullAnimationUpperBoundMilliseconds +
               OneNativeUpdateMilliseconds();
    }

    public static int ConservativeGameMinutesForAttempts(
        int attemptCount,
        bool challengeBait)
    {
        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount));
        }
        if (attemptCount == 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(
            attemptCount * (double)ConservativeAttemptMilliseconds(
                challengeBait) /
            GameClockBudgetPolicy.RealMillisecondsPerGameMinute);
    }

    public static double EnergyPerAttempt(
        int effectiveFishingLevel,
        bool efficientEnchantment)
    {
        if (effectiveFishingLevel < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveFishingLevel));
        }

        return efficientEnchantment
            ? 0d
            : Math.Max(0d, 8d - effectiveFishingLevel * 0.1d);
    }

    public static double EnergyForAttempts(
        int attemptCount,
        int effectiveFishingLevel,
        bool efficientEnchantment)
    {
        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount));
        }

        return attemptCount * EnergyPerAttempt(
            effectiveFishingLevel,
            efficientEnchantment);
    }

    private static int FirstCastBiteUpperBoundMilliseconds() =>
        (int)Math.Ceiling(NativeMaximumBiteMilliseconds * 0.75d);

    private static int OneNativeUpdateMilliseconds() =>
        (int)Math.Ceiling(1_000d / NativeUpdatesPerSecond);

    private static int CastFlightUpperBoundMilliseconds()
    {
        const double acceleration = 0.005d;
        const double maximumAddedDistance = 4d;
        var verticalDistance = (maximumAddedDistance + 3d) * 64d;
        var signedDistance = -Math.Max(128d, verticalDistance);
        var trajectoryHeight = Math.Abs(signedDistance - 64d);
        var initialVelocity = Math.Sqrt(
            2d * acceleration * trajectoryHeight);
        var duration = Math.Sqrt(
                           2d * (trajectoryHeight - signedDistance) /
                           acceleration) +
                       initialVelocity / acceleration;
        return (int)Math.Ceiling(duration * 1.05d);
    }
}
