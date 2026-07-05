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

        return Section("time", new Dictionary<string, object>
        {
            ["year"] = Field(Context.IsWorldReady ? (int?)Game1.year : null, "Game1.year", tick),
            ["season"] = Field(Context.IsWorldReady ? Game1.currentSeason : null, "Game1.currentSeason", tick),
            ["day"] = Field(Context.IsWorldReady ? (int?)Game1.dayOfMonth : null, "Game1.dayOfMonth", tick),
            ["time"] = Field(Context.IsWorldReady ? (int?)Game1.timeOfDay : null, "Game1.timeOfDay", tick),
            ["weather"] = Field(Context.IsWorldReady ? CurrentWeather() : null, "Game1.isRaining/isSnowing/isLightning/isDebrisWeather", tick)
        });
    }

    private static string CurrentWeather()
    {
        if (Game1.isLightning)
        {
            return "lightning";
        }

        if (Game1.isRaining)
        {
            return "rain";
        }

        if (Game1.isSnowing)
        {
            return "snow";
        }

        if (Game1.isDebrisWeather)
        {
            return "debris";
        }

        return "sun";
    }
}
