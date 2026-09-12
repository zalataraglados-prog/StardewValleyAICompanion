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

        return new
        {
            current_total_day = Game1.Date.TotalDays,
            time_of_day = Game1.timeOfDay,
            days_played = Game1.stats.DaysPlayed,
            festival_date_keys = DataLoader
                .Festivals_FestivalDates(Game1.temporaryContent)
                .Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray(),
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
