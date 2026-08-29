using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyRainTotemRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_rain_totem", StringComparison.Ordinal))
            return;
        request.RainTotemProjectionFingerprint = ReadQueueParameterString(item, "rain_totem_projection_fingerprint");
        request.RainTotemSourceLocationContextId = ReadQueueParameterString(item, "source_location_context_id");
        request.RainTotemConfiguredAffectedContextId = ReadQueueParameterString(item, "configured_affected_context_id");
        request.RainTotemAffectedLocationContextId = ReadQueueParameterString(item, "affected_location_context_id");
        request.RainTotemWeatherStateOwnerContextId = ReadQueueParameterString(item, "weather_state_owner_context_id");
        request.RainTotemAllowRainTotem = ReadQueueParameterBool(item, "allow_rain_totem");
        request.RainTotemTomorrowIsDefaultFestival = ReadQueueParameterBool(item, "tomorrow_is_default_festival");
        request.RainTotemAffectedWeatherBefore = ReadQueueParameterString(item, "affected_weather_before");
        request.RainTotemAffectedWeatherAfter = ReadQueueParameterString(item, "affected_weather_after");
        request.RainTotemTomorrowTotalDays = ReadQueueParameterInt(item, "tomorrow_total_days");
        request.RainTotemEffectiveTomorrowWeather = ReadQueueParameterString(item, "effective_tomorrow_weather");
        request.RainTotemRainWillTakeEffectTomorrow = ReadQueueParameterBool(item, "rain_will_take_effect_tomorrow");
        request.RainTotemFacingDirection = ReadQueueParameterInt(item, "native_facing_direction");
        request.RainTotemAnimationDurationMs = ReadQueueParameterInt(item, "native_animation_duration_ms");
        request.RainTotemCloudSpriteCount = ReadQueueParameterInt(item, "native_cloud_sprite_count");
        request.RainTotemItemSpriteCount = ReadQueueParameterInt(item, "native_item_sprite_count");
        request.RainTotemCloudBatchCount = ReadQueueParameterInt(item, "native_cloud_batch_count");
        request.RainTotemCloudDelayStepMs = ReadQueueParameterInt(item, "native_cloud_delay_step_ms");
        request.RainTotemInitialSound = ReadQueueParameterString(item, "native_initial_sound");
        request.RainTotemDelayedSound = ReadQueueParameterString(item, "native_delayed_sound");
        request.RainTotemDelayedSoundMs = ReadQueueParameterInt(item, "native_delayed_sound_ms");
    }
}
