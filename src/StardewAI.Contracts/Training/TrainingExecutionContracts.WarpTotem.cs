using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("warp_totem_projection_fingerprint")]
    public string WarpTotemProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_base_destination_location_id")]
    public string WarpTotemBaseDestinationLocationId { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_requested_destination_tile_x")]
    public int? WarpTotemRequestedDestinationTileX { get; set; }

    [JsonPropertyName("warp_totem_requested_destination_tile_y")]
    public int? WarpTotemRequestedDestinationTileY { get; set; }

    [JsonPropertyName("warp_totem_effective_destination_location_id")]
    public string WarpTotemEffectiveDestinationLocationId { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_effective_destination_tile_x")]
    public int? WarpTotemEffectiveDestinationTileX { get; set; }

    [JsonPropertyName("warp_totem_effective_destination_tile_y")]
    public int? WarpTotemEffectiveDestinationTileY { get; set; }

    [JsonPropertyName("warp_totem_destination_route_mode")]
    public string WarpTotemDestinationRouteMode { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_farm_destination_source")]
    public string WarpTotemFarmDestinationSource { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_passive_festival_route_json")]
    public string WarpTotemPassiveFestivalRouteJson { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_active_festival_id")]
    public string WarpTotemActiveFestivalId { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_active_festival_start_time")]
    public int? WarpTotemActiveFestivalStartTime { get; set; }

    [JsonPropertyName("warp_totem_active_festival_end_time")]
    public int? WarpTotemActiveFestivalEndTime { get; set; }

    [JsonPropertyName("warp_totem_active_festival_entry_tile_x")]
    public int? WarpTotemActiveFestivalEntryTileX { get; set; }

    [JsonPropertyName("warp_totem_active_festival_entry_tile_y")]
    public int? WarpTotemActiveFestivalEntryTileY { get; set; }

    [JsonPropertyName("warp_totem_active_festival_entry_facing")]
    public int? WarpTotemActiveFestivalEntryFacing { get; set; }

    [JsonPropertyName("warp_totem_festival_prestart_warp_cancelled")]
    public bool? WarpTotemFestivalPrestartWarpCancelled { get; set; }

    [JsonPropertyName("warp_totem_festival_ready_check_required")]
    public bool? WarpTotemFestivalReadyCheckRequired { get; set; }

    [JsonPropertyName("warp_totem_facing_direction")]
    public int? WarpTotemFacingDirection { get; set; }

    [JsonPropertyName("warp_totem_animation_duration_ms")]
    public int? WarpTotemAnimationDurationMs { get; set; }

    [JsonPropertyName("warp_totem_totem_callback_delay_ms")]
    public int? WarpTotemCallbackDelayMs { get; set; }

    [JsonPropertyName("warp_totem_initial_item_sprite_count")]
    public int? WarpTotemInitialItemSpriteCount { get; set; }

    [JsonPropertyName("warp_totem_sprinkle_sprite_count")]
    public int? WarpTotemSprinkleSpriteCount { get; set; }

    [JsonPropertyName("warp_totem_poof_sprite_count")]
    public int? WarpTotemPoofSpriteCount { get; set; }

    [JsonPropertyName("warp_totem_trail_sprite_count")]
    public int? WarpTotemTrailSpriteCount { get; set; }

    [JsonPropertyName("warp_totem_initial_sound")]
    public string WarpTotemInitialSound { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_warp_sound")]
    public string WarpTotemWarpSound { get; set; } = string.Empty;

    [JsonPropertyName("warp_totem_glow_color_rgba")]
    public string WarpTotemGlowColorRgba { get; set; } = string.Empty;
}
