using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static object? ReadGameStateQueryCalendarState()
    {
        if (!Context.IsWorldReady)
        {
            return null;
        }

        var passiveFestivals = DataLoader.PassiveFestivals(Game1.content)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new
            {
                festival_id = pair.Key,
                season = pair.Value.Season.ToString().ToLowerInvariant(),
                start_day = pair.Value.StartDay,
                end_day = pair.Value.EndDay,
                start_time = pair.Value.StartTime,
                condition = pair.Value.Condition ?? string.Empty
            })
            .ToArray();

        var festivalDateKeys = DataLoader.Festivals_FestivalDates(Game1.temporaryContent).Keys
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        return new
        {
            current_total_day = Game1.Date.TotalDays,
            save_unique_id = Game1.uniqueIDForThisGame,
            time_of_day = Game1.timeOfDay,
            days_played = Game1.stats.DaysPlayed,
            festival_date_keys = festivalDateKeys,
            wedding_calendar = ReadNativeWeddingCalendar(festivalDateKeys),
            active_passive_festival_ids = Game1.netWorldState.Value
                .ActivePassiveFestivals
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            passive_festivals = passiveFestivals,
            festival_location_context_resolution_status =
                "not_projected"
        };
    }
}
