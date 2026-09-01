using System;

namespace StardewAI.Core.Execution;

public static class GameClockBudgetPolicy
{
    public const int AutonomousRecoveryStartTime = 1900;
    public const int DayEndTime = 2600;
    public const int RecoverySafetyBufferMinutes = 60;

    // Stardew 1.6.15 advances one game minute per 700 real milliseconds.
    // Game1.ticks and SMAPI UpdateTicked run at the game's 60 UPS target.
    // SERVER_FPS controls headless display output and is not this update rate.
    public const int RealMillisecondsPerGameMinute = 700;
    public const int RuntimeUpdatesPerSecond = 60;
    public const int PerfectTravelTilesPerGameMinute = 3;

    public static int TicksToGameMinutes(int ticks)
    {
        var boundedTicks = Math.Max(1, ticks);
        return Math.Max(
            1,
            (int)Math.Ceiling(
                boundedTicks * 1000d /
                (RuntimeUpdatesPerSecond * RealMillisecondsPerGameMinute)));
    }

    public static int MovementTilesToGameMinutes(int tiles) =>
        Math.Max(
            1,
            (int)Math.Ceiling(
                Math.Max(1, tiles) /
                (double)PerfectTravelTilesPerGameMinute));

    public static int ClockMinutesBetween(int start, int end) =>
        ToAbsoluteMinutes(end) - ToAbsoluteMinutes(start);

    public static bool RecoveryWindowStarted(int timeOfDay) =>
        timeOfDay >= AutonomousRecoveryStartTime;

    private static int ToAbsoluteMinutes(int hhmm)
    {
        var hours = hhmm / 100;
        var minutes = hhmm % 100;
        return hours * 60 + minutes;
    }
}
