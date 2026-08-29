using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("rain_totem_projection_fingerprint")]
    public string RainTotemProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_source_location_context_id")]
    public string RainTotemSourceLocationContextId { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_configured_affected_context_id")]
    public string RainTotemConfiguredAffectedContextId { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_affected_location_context_id")]
    public string RainTotemAffectedLocationContextId { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_weather_state_owner_context_id")]
    public string RainTotemWeatherStateOwnerContextId { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_allow_rain_totem")]
    public bool? RainTotemAllowRainTotem { get; set; }

    [JsonPropertyName("rain_totem_tomorrow_is_default_festival")]
    public bool? RainTotemTomorrowIsDefaultFestival { get; set; }

    [JsonPropertyName("rain_totem_affected_weather_before")]
    public string RainTotemAffectedWeatherBefore { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_affected_weather_after")]
    public string RainTotemAffectedWeatherAfter { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_tomorrow_total_days")]
    public int? RainTotemTomorrowTotalDays { get; set; }

    [JsonPropertyName("rain_totem_effective_tomorrow_weather")]
    public string RainTotemEffectiveTomorrowWeather { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_rain_will_take_effect_tomorrow")]
    public bool? RainTotemRainWillTakeEffectTomorrow { get; set; }

    [JsonPropertyName("rain_totem_facing_direction")]
    public int? RainTotemFacingDirection { get; set; }

    [JsonPropertyName("rain_totem_animation_duration_ms")]
    public int? RainTotemAnimationDurationMs { get; set; }

    [JsonPropertyName("rain_totem_cloud_sprite_count")]
    public int? RainTotemCloudSpriteCount { get; set; }

    [JsonPropertyName("rain_totem_item_sprite_count")]
    public int? RainTotemItemSpriteCount { get; set; }

    [JsonPropertyName("rain_totem_cloud_batch_count")]
    public int? RainTotemCloudBatchCount { get; set; }

    [JsonPropertyName("rain_totem_cloud_delay_step_ms")]
    public int? RainTotemCloudDelayStepMs { get; set; }

    [JsonPropertyName("rain_totem_initial_sound")]
    public string RainTotemInitialSound { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_delayed_sound")]
    public string RainTotemDelayedSound { get; set; } = string.Empty;

    [JsonPropertyName("rain_totem_delayed_sound_ms")]
    public int? RainTotemDelayedSoundMs { get; set; }
}
