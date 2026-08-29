using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupRainTotemFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var started = DateTimeOffset.UtcNow.ToString("O");
        var locationName = string.IsNullOrWhiteSpace(request.LocationId) ? "Farm" : request.LocationId;
        var location = Game1.getLocationFromName(locationName);
        if (location is null)
            return BlockedWithPrimitive(request, "debug_setup_rain_totem", locationName,
                "location=unavailable", "rain_totem_fixture_location_unavailable");
        var entryWarp = location.warps.FirstOrDefault();
        var entryX = entryWarp?.X ?? Math.Max(1, Game1.player.TilePoint.X);
        var entryY = entryWarp?.Y ?? Math.Max(1, Game1.player.TilePoint.Y);
        Game1.warpFarmer(locationName, entryX, entryY, false);
        location = Game1.currentLocation;
        Game1.exitActiveMenu();
        Game1.eventUp = false;
        Game1.fadeToBlack = false;
        Game1.player.swimming.Value = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.UsingTool = false;
        var fixtureDay = request.RainTotemTomorrowIsDefaultFestival == true ? 12 : 2;
        Game1.Date.Season = Season.Spring;
        Game1.Date.DayOfMonth = fixtureDay;
        Game1.season = Season.Spring;
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = fixtureDay;
        Game1.player.forceCanMove();
        var contextData = location.GetLocationContext();
        var sourceContext = location.GetLocationContextId();
        var affectedContext = contextData.RainTotemAffectsContext ?? sourceContext;
        if (string.Equals(affectedContext, "Default", StringComparison.Ordinal))
        {
            Game1.weatherForTomorrow = "Sun";
            Game1.netWorldState.Value.WeatherForTomorrow = "Sun";
        }
        else
        {
            location.GetWeather().WeatherForTomorrow = "Sun";
        }
        var slot = EnsureInventoryItem("(O)681", 2);
        var totem = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as StardewValley.Object : null;
        if (totem is not null)
            totem.Stack = 2;
        var tomorrowFestival = string.Equals(affectedContext, "Default", StringComparison.Ordinal) &&
            Utility.isFestivalDay(Game1.dayOfMonth + 1, Game1.season);
        var festivalKey = Utility.getSeasonKey(Game1.season) + (Game1.dayOfMonth + 1);
        var festivalKeyExists = DataLoader.Festivals_FestivalDates(Game1.temporaryContent).ContainsKey(festivalKey);
        var verified = totem?.GetType() == typeof(StardewValley.Object) && totem.QualifiedItemId == "(O)681" &&
            totem.Stack == 2 && contextData.AllowRainTotem &&
            tomorrowFestival == (request.RainTotemTomorrowIsDefaultFestival == true) && Game1.player.canMove;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_rain_totem",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_rain_totem_ready", "location_context_route_bound", "tomorrow_weather_reset_to_sun" }
                : new[]
                {
                    "location=" + locationName, "source_context=" + sourceContext,
                    "affected_context=" + affectedContext, "tomorrow_festival=" + tomorrowFestival,
                    "request_tomorrow_festival=" + request.RainTotemTomorrowIsDefaultFestival,
                    "festival_key=" + festivalKey, "festival_key_exists=" + festivalKeyExists,
                    "runtime_day=" + Game1.dayOfMonth, "runtime_season=" + Game1.season,
                    "can_move=" + Game1.player.canMove
                },
            RequestedEffect = "player.rain_totem.ready=true",
            ObservedEffect = "location=" + locationName + ";source_context=" + sourceContext +
                ";affected_context=" + affectedContext + ";slot=" + slot + ";stack=" + (totem?.Stack ?? 0),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "rain_totem_fixture_post_state_mismatch" }
        };
    }
}
