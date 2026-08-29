using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("return_scepter_projection_fingerprint")]
    public string ReturnScepterProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("return_scepter_home_location_id")]
    public string ReturnScepterHomeLocationId { get; set; } = string.Empty;

    [JsonPropertyName("return_scepter_home_runtime_type")]
    public string ReturnScepterHomeRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("return_scepter_destination_location_id")]
    public string ReturnScepterDestinationLocationId { get; set; } = string.Empty;

    [JsonPropertyName("return_scepter_front_door_tile_x")]
    public int? ReturnScepterFrontDoorTileX { get; set; }

    [JsonPropertyName("return_scepter_front_door_tile_y")]
    public int? ReturnScepterFrontDoorTileY { get; set; }

    [JsonPropertyName("return_scepter_home_is_cabin")]
    public bool? ReturnScepterHomeIsCabin { get; set; }

    [JsonPropertyName("return_scepter_already_at_destination")]
    public bool? ReturnScepterAlreadyAtDestination { get; set; }

    [JsonPropertyName("return_scepter_instant_use")]
    public bool? ReturnScepterInstantUse { get; set; }

    [JsonPropertyName("return_scepter_facing_direction")]
    public int? ReturnScepterFacingDirection { get; set; }

    [JsonPropertyName("return_scepter_callback_delay_ms")]
    public int? ReturnScepterCallbackDelayMs { get; set; }

    [JsonPropertyName("return_scepter_freeze_pause_ms")]
    public int? ReturnScepterFreezePauseMs { get; set; }

    [JsonPropertyName("return_scepter_poof_sprite_count")]
    public int? ReturnScepterPoofSpriteCount { get; set; }

    [JsonPropertyName("return_scepter_trail_sprite_count")]
    public int? ReturnScepterTrailSpriteCount { get; set; }

    [JsonPropertyName("return_scepter_trail_delay_step_ms")]
    public int? ReturnScepterTrailDelayStepMs { get; set; }

    [JsonPropertyName("return_scepter_trail_max_delay_ms")]
    public int? ReturnScepterTrailMaxDelayMs { get; set; }

    [JsonPropertyName("return_scepter_sound")]
    public string ReturnScepterSound { get; set; } = string.Empty;
}
