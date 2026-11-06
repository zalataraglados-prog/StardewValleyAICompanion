using StardewAI.Contracts.State;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static readonly NativeWeddingCalendarProjectionCache WeddingCalendarCache = new();
    private static readonly Season[] WeddingCalendarSeasons = { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    private static NativeWeddingCalendarProjection ReadNativeWeddingCalendar(string[] festivalDateKeys) =>
        WeddingCalendarCache.Read(Game1.uniqueIDForThisGame, Game1.year, festivalDateKeys,
            static (season, day) => Game1.canHaveWeddingOnDay(day, WeddingCalendarSeasons[season]));
}
