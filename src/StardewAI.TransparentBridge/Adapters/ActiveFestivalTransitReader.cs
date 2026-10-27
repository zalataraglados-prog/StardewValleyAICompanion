using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

internal static class ActiveFestivalTransitReader
{
    public static ActiveFestivalTransitProjection Read(string destinationLocation)
    {
        var nativeGateActive = Game1.weatherIcon == 1 &&
            !string.IsNullOrWhiteSpace(Game1.whereIsTodaysFest);
        if (!Utility.isFestivalDay() && !nativeGateActive)
        {
            return ActiveFestivalTransitProjection.None;
        }

        try
        {
            var festivalId = Game1.season.ToString().ToLowerInvariant() +
                Game1.dayOfMonth;
            var data = Game1.temporaryContent.Load<Dictionary<string, string>>(
                "Data\\Festivals\\" + festivalId);
            if (!data.TryGetValue("conditions", out var rawConditions))
            {
                return ActiveFestivalTransitProjection.Unavailable(
                    "festival_conditions_missing",
                    nativeGateActive,
                    destinationLocation);
            }

            var conditionParts = rawConditions.Split('/');
            if (conditionParts.Length < 2)
            {
                return ActiveFestivalTransitProjection.Unavailable(
                    "festival_conditions_invalid",
                    nativeGateActive,
                    destinationLocation);
            }

            var times = ArgUtility.SplitBySpace(conditionParts[1]);
            if (times.Length < 2 ||
                !int.TryParse(times[0], out var startTime) ||
                !int.TryParse(times[1], out var endTime))
            {
                return ActiveFestivalTransitProjection.Unavailable(
                    "festival_time_window_invalid",
                    nativeGateActive,
                    destinationLocation);
            }

            var locationId = conditionParts[0];
            var entryX = -1;
            var entryY = -1;
            var entryFacing = -1;
            if (data.TryGetValue("set-up", out var setup))
            {
                foreach (var command in setup.Split('/')
                    .Select(value => ArgUtility.SplitBySpace(value)))
                {
                    if (command.Length >= 4 &&
                        string.Equals(
                            command[0],
                            "farmer",
                            StringComparison.Ordinal) &&
                        int.TryParse(command[1], out entryX) &&
                        int.TryParse(command[2], out entryY) &&
                        int.TryParse(command[3], out entryFacing))
                    {
                        break;
                    }
                }
            }

            var festivalTargetsDestination = string.Equals(
                locationId,
                destinationLocation,
                StringComparison.Ordinal);
            if (festivalTargetsDestination &&
                (entryX < 0 || entryY < 0 || entryFacing < 0))
            {
                return ActiveFestivalTransitProjection.Unavailable(
                    "festival_farmer_entry_missing",
                    nativeGateActive,
                    destinationLocation);
            }

            var nativeGateTargetsDestination = nativeGateActive &&
                string.Equals(
                    Game1.whereIsTodaysFest,
                    destinationLocation,
                    StringComparison.Ordinal);
            var nativePrestartWarpRejected =
                nativeGateTargetsDestination &&
                Game1.timeOfDay <= endTime &&
                Game1.timeOfDay < startTime;
            var nativeFestivalEntryWindow =
                nativeGateTargetsDestination &&
                Game1.timeOfDay >= startTime &&
                Game1.timeOfDay <= endTime;
            var ordinaryTransitAllowedNow =
                !nativeGateTargetsDestination || Game1.timeOfDay > endTime;
            var nextOrdinaryOpenTime = nativeGateTargetsDestination &&
                !ordinaryTransitAllowedNow
                    ? NextTenMinuteTime(endTime)
                    : null;
            var ordinaryTransitAllowedOnCaptureDate =
                ordinaryTransitAllowedNow || nextOrdinaryOpenTime.HasValue;
            var ordinaryTransitMode = !nativeGateTargetsDestination
                ? "ordinary"
                : nativePrestartWarpRejected
                    ? "blocked_prestart_native_dialogue"
                    : nativeFestivalEntryWindow
                        ? "active_festival_entry_only"
                        : "ordinary";

            return new ActiveFestivalTransitProjection(
                true,
                "complete",
                festivalId,
                locationId,
                startTime,
                endTime,
                entryX,
                entryY,
                entryFacing,
                festivalTargetsDestination,
                nativeGateActive,
                nativeGateTargetsDestination,
                nativePrestartWarpRejected,
                nativeFestivalEntryWindow,
                ordinaryTransitAllowedNow,
                ordinaryTransitAllowedOnCaptureDate,
                nextOrdinaryOpenTime,
                ordinaryTransitMode);
        }
        catch
        {
            return ActiveFestivalTransitProjection.Unavailable(
                "festival_data_load_failed",
                nativeGateActive,
                destinationLocation);
        }
    }

    private static int? NextTenMinuteTime(int time)
    {
        var minutes = time / 100 * 60 + time % 100 + 10;
        if (minutes >= 26 * 60)
        {
            return null;
        }
        return minutes / 60 * 100 + minutes % 60;
    }
}

internal sealed record ActiveFestivalTransitProjection(
    bool ProjectionComplete,
    string Status,
    string FestivalId,
    string LocationId,
    int StartTime,
    int EndTime,
    int EntryTileX,
    int EntryTileY,
    int EntryFacing,
    bool FestivalTargetsDestination,
    bool NativeGateActive,
    bool NativeGateTargetsDestination,
    bool NativePrestartWarpRejected,
    bool NativeFestivalEntryWindow,
    bool OrdinaryTransitAllowedNow,
    bool OrdinaryTransitAllowedOnCaptureDate,
    int? NextOrdinaryOpenTime,
    string OrdinaryTransitMode)
{
    public static ActiveFestivalTransitProjection None { get; } = new(
        true,
        "not_festival_day",
        string.Empty,
        string.Empty,
        -1,
        -1,
        -1,
        -1,
        -1,
        false,
        false,
        false,
        false,
        false,
        true,
        true,
        null,
        "ordinary");

    public static ActiveFestivalTransitProjection Unavailable(
        string status,
        bool nativeGateActive,
        string destinationLocation)
    {
        var targets = nativeGateActive && string.Equals(
            Game1.whereIsTodaysFest,
            destinationLocation,
            StringComparison.Ordinal);
        return new(
            false,
            status,
            string.Empty,
            Game1.whereIsTodaysFest ?? string.Empty,
            -1,
            -1,
            -1,
            -1,
            -1,
            false,
            nativeGateActive,
            targets,
            false,
            false,
            !targets,
            !targets,
            null,
            targets ? "unresolved_native_festival_gate" : "ordinary");
    }
}
