using StardewValley;
using StardewValley.Network;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string SolarPanelQualifiedItemId = "(BC)231";
    private const string SolarPanelOutputQualifiedItemId = "(O)787";
    private const string SolarPanelStateModelId =
        "solar_panel_day_update_weather.v1";

    private static bool IsVettedSolarPanelOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId ==
                SolarPanelQualifiedItemId &&
            machine.GetType() ==
                typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputSolarPanel",
                StringComparison.Ordinal);
    }

    private static object? ReadSolarPanelSpecialState(
        StardewValley.Object machine,
        GameLocation location)
    {
        if (machine.QualifiedItemId !=
                SolarPanelQualifiedItemId ||
            machine.GetType() != typeof(StardewValley.Object))
        {
            return null;
        }

        var locationContextId =
            location.GetLocationContextId();
        var hasWeather = Game1.netWorldState.Value
            .LocationWeather.TryGetValue(
                locationContextId,
                out var weather);
        var heldItem = machine.heldObject.Value;
        var lifecycleState = heldItem is null
            ? "waiting_for_day_update"
            : machine.readyForHarvest.Value
                ? "ready_for_collection"
                : "charging";
        var currentRainBlocksProgress =
            hasWeather && weather!.IsRaining;
        var currentClockProgressAllowed =
            hasWeather &&
            location.IsOutdoors &&
            !currentRainBlocksProgress;

        return new
        {
            schema_version =
                "solar_panel_special_state.v1",
            status = hasWeather
                ? "available"
                : "blocked",
            reason = hasWeather
                ? string.Empty
                : "location_context_weather_not_initialized",
            special_state_model_id =
                SolarPanelStateModelId,
            source =
                "decompiled_Object.DayUpdate_OutputSolarPanel_ShouldTimePassForMachine",
            lifecycle_state = lifecycleState,
            output_qualified_item_id =
                SolarPanelOutputQualifiedItemId,
            held_output_matches_native_contract =
                heldItem is null ||
                heldItem.QualifiedItemId ==
                    SolarPanelOutputQualifiedItemId,
            minutes_until_ready =
                machine.MinutesUntilReady,
            ready_for_harvest =
                machine.readyForHarvest.Value,
            location_context_id = locationContextId,
            location_is_outdoors = location.IsOutdoors,
            current_weather =
                ReadSolarPanelWeather(weather),
            current_clock_progress_allowed =
                currentClockProgressAllowed,
            day_update_contract = new
            {
                trigger = "DayUpdate",
                creates_output_only_when_held_item_is_null =
                    true,
                initial_days_until_morning = 7,
                initial_output_qualified_item_id =
                    SolarPanelOutputQualifiedItemId,
                initial_sunny_outdoor_minute_adjustment =
                    -2400,
                time_pass_blockers = new[]
                {
                    "Inside",
                    "Rain"
                }
            },
            completion_projection_status =
                lifecycleState == "ready_for_collection"
                    ? "exact_ready_now"
                    : "weather_dependent_no_guessed_multi_day_completion"
        };
    }

    private static object? ReadSolarPanelWeather(
        LocationWeather? weather)
    {
        return weather is null
            ? null
            : new
            {
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
    }
}
