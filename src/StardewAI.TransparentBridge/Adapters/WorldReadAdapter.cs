using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class WorldReadAdapter : ReadAdapterBase
{
    public override string Domain => "world";
    public override int Priority => 10;

    public override StateAdapterResult Collect(long tick)
    {
        var location = Context.IsWorldReady ? Game1.currentLocation : null;

        return Section("game", new Dictionary<string, object>
        {
            ["world_ready"] = Field(Context.IsWorldReady, "Context.IsWorldReady", tick),
            ["date"] = Field(Context.IsWorldReady ? $"{Game1.year}-{Game1.currentSeason}-{Game1.dayOfMonth}" : null, "Game1.year/currentSeason/dayOfMonth", tick),
            ["year"] = Field(Context.IsWorldReady ? (int?)Game1.year : null, "Game1.year", tick),
            ["season"] = Field(Context.IsWorldReady ? Game1.currentSeason : null, "Game1.currentSeason", tick),
            ["day_of_month"] = Field(Context.IsWorldReady ? (int?)Game1.dayOfMonth : null, "Game1.dayOfMonth", tick),
            ["time_of_day"] = Field(Context.IsWorldReady ? (int?)Game1.timeOfDay : null, "Game1.timeOfDay", tick),
            ["weather_tomorrow"] = Field(Context.IsWorldReady ? Game1.weatherForTomorrow : null, "Game1.weatherForTomorrow", tick),
            ["current_map"] = Field(location?.NameOrUniqueName, "Game1.currentLocation.NameOrUniqueName", tick),
            ["is_outdoors"] = Field(location?.IsOutdoors, "Game1.currentLocation.IsOutdoors", tick),
            ["is_farm"] = Field(location?.IsFarm, "Game1.currentLocation.IsFarm", tick)
        });
    }
}
