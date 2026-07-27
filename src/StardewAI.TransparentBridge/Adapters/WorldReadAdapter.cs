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
            ["total_days"] = Field(Context.IsWorldReady ? (int?)Game1.Date.TotalDays : null, "Game1.Date.TotalDays", tick),
            ["time"] = Field(Context.IsWorldReady ? (int?)Game1.timeOfDay : null, "Game1.timeOfDay", tick),
            ["is_green_rain"] = Field(Context.IsWorldReady ? (bool?)Game1.isGreenRain : null, "Game1.isGreenRain", tick),
            ["weather"] = Field(Context.IsWorldReady ? CurrentWeather() : null, "Game1.isRaining/isSnowing/isLightning/isDebrisWeather", tick),
            ["weather_for_tomorrow"] = Field(
                Context.IsWorldReady ? Game1.weatherForTomorrow : null,
                "Game1.weatherForTomorrow",
                tick),
            ["location_context_weather"] = Field(
                Context.IsWorldReady
                    ? ReadExistingLocationContextWeather()
                    : null,
                "Game1.netWorldState.Value.LocationWeather",
                tick)
        });
    }

    private static object[] ReadExistingLocationContextWeather()
    {
        var weatherByContext =
            Game1.netWorldState.Value.LocationWeather;
        return weatherByContext.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key =>
            {
                var weather = weatherByContext[key];
                return new
                {
                    location_context_id = key,
                    weather = weather.Weather,
                    weather_for_tomorrow =
                        weather.WeatherForTomorrow,
                    is_raining = weather.IsRaining,
                    is_snowing = weather.IsSnowing,
                    is_lightning = weather.IsLightning,
                    is_debris_weather =
                        weather.IsDebrisWeather,
                    is_green_rain = weather.IsGreenRain
                };
            })
            .ToArray<object>();
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
