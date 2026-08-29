using System.Text.Json;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string RainTotemNativeContract =
        "Object.performUseAction((O)681)->Object.rainTotem->AllowRainTotem->RainTotemAffectsContext_or_location_context->Default_festival_guard_or_context_WeatherForTomorrow=Rain->Default_Game1.getWeatherModificationsForDate";

    private static object ReadRainTotemContext(Farmer? player)
    {
        if (player?.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        var contextId = location.GetLocationContextId();
        var context = location.GetLocationContext();
        var configuredAffectedContext = context.RainTotemAffectsContext ?? string.Empty;
        var affectedContext = string.IsNullOrEmpty(configuredAffectedContext)
            ? contextId
            : configuredAffectedContext;
        var weatherStateOwnerContext = string.Equals(affectedContext, "Default", StringComparison.Ordinal)
            ? "Default"
            : contextId;
        var weatherBefore = string.Equals(affectedContext, "Default", StringComparison.Ordinal)
            ? Game1.weatherForTomorrow
            : location.GetWeather().WeatherForTomorrow;
        var tomorrowIsDefaultFestival = string.Equals(affectedContext, "Default", StringComparison.Ordinal) &&
            Utility.isFestivalDay(Game1.dayOfMonth + 1, Game1.season);
        var tomorrowDate = new WorldDate(Game1.Date);
        tomorrowDate.TotalDays++;
        var effectiveTomorrowWeather = string.Equals(affectedContext, "Default", StringComparison.Ordinal)
            ? Game1.getWeatherModificationsForDate(tomorrowDate, "Rain")
            : "Rain";
        var rainWillTakeEffectTomorrow = string.Equals(effectiveTomorrowWeather, "Rain", StringComparison.Ordinal);
        var rows = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                string.Equals(entry.item.QualifiedItemId, "(O)681", StringComparison.Ordinal))
            .Select(entry => new
            {
                inventory_slot_index = entry.slot,
                item_id = entry.item!.ItemId,
                qualified_item_id = entry.item.QualifiedItemId,
                display_name = entry.item.DisplayName,
                inventory_runtime_type = entry.item.GetType().FullName,
                stack_before = entry.item.Stack,
                stack_after = Math.Max(0, entry.item.Stack - 1),
                temporarily_invisible = ((StardewValley.Object)entry.item).isTemporarilyInvisible
            })
            .ToArray();
        var visibleItem = rows.Any(row => !row.temporarily_invisible && row.stack_before > 0);
        var nativeBaseGate = player.canMove && visibleItem && !Game1.eventUp && !Game1.isFestival() &&
            !Game1.fadeToBlack && !player.swimming.Value && !player.bathingClothes.Value &&
            !player.onBridge.Value && Game1.activeClickableMenu is null;
        var gateStatus = rows.Length == 0 ? "blocked_no_inventory_rain_totem" :
            !nativeBaseGate ? "blocked_base_object_use_gate" :
            !context.AllowRainTotem ? "blocked_location_context_disallows_rain_totem" :
            tomorrowIsDefaultFestival ? "blocked_default_festival_tomorrow" :
            string.Equals(weatherBefore, "Rain", StringComparison.Ordinal) ? "blocked_weather_already_rain" :
            !rainWillTakeEffectTomorrow ? "blocked_tomorrow_weather_override" :
            "ready";
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "rain_totem.v1",
            location = location.NameOrUniqueName,
            contextId,
            configuredAffectedContext,
            affectedContext,
            weatherStateOwnerContext,
            context.AllowRainTotem,
            tomorrowIsDefaultFestival,
            tomorrowDate.TotalDays,
            effectiveTomorrowWeather,
            rainWillTakeEffectTomorrow,
            weatherBefore,
            nativeBaseGate,
            rows
        }));

        return new
        {
            schema_version = "rain_totem.v1",
            projection_status = "complete_current_native_rain_totem_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            native_use_gate_status = gateStatus,
            native_base_use_gate = new
            {
                can_move = player.canMove,
                visible_rain_totem_available = visibleItem,
                event_up = Game1.eventUp,
                festival = Game1.isFestival(),
                fade_to_black = Game1.fadeToBlack,
                swimming = player.swimming.Value,
                bathing_clothes = player.bathingClothes.Value,
                on_bridge = player.onBridge.Value,
                active_menu_clear = Game1.activeClickableMenu is null,
                passed = nativeBaseGate
            },
            context_routing = new
            {
                source_location_context_id = contextId,
                configured_affected_context_id = configuredAffectedContext,
                affected_location_context_id = affectedContext,
                weather_state_owner_context_id = weatherStateOwnerContext,
                allow_rain_totem = context.AllowRainTotem
            },
            weather_transition = new
            {
                tomorrow_is_default_festival = tomorrowIsDefaultFestival,
                affected_weather_before = weatherBefore,
                affected_weather_after = "Rain",
                tomorrow_total_days = tomorrowDate.TotalDays,
                effective_tomorrow_weather = effectiveTomorrowWeather,
                rain_will_take_effect_tomorrow = rainWillTakeEffectTomorrow
            },
            animation_contract = new
            {
                facing_direction = 2,
                animation_duration_ms = 2000,
                cloud_sprite_count = 18,
                item_sprite_count = 1,
                cloud_batch_count = 6,
                cloud_delay_step_ms = 200,
                initial_sound = "thunder",
                delayed_sound = "rainsound",
                delayed_sound_ms = 2000
            },
            native_contract = RainTotemNativeContract,
            rows
        };
    }
}
