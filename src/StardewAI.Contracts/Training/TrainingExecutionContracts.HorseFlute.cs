using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("horse_warp_restrictions")]
    public int? HorseWarpRestrictions { get; set; }

    [JsonPropertyName("horse_warp_restriction_names")]
    public string HorseWarpRestrictionNames { get; set; } = string.Empty;

    [JsonPropertyName("owned_horse_id")]
    public string OwnedHorseId { get; set; } = string.Empty;

    [JsonPropertyName("owned_horse_location_id")]
    public string OwnedHorseLocationId { get; set; } = string.Empty;

    [JsonPropertyName("owned_horse_tile_x")]
    public int? OwnedHorseTileX { get; set; }

    [JsonPropertyName("owned_horse_tile_y")]
    public int? OwnedHorseTileY { get; set; }

    [JsonPropertyName("owned_horse_nearby")]
    public bool? OwnedHorseNearby { get; set; }

    [JsonPropertyName("team_event_stable_horse_id")]
    public string TeamEventStableHorseId { get; set; } = string.Empty;

    [JsonPropertyName("team_event_stable_location_id")]
    public string TeamEventStableLocationId { get; set; } = string.Empty;

    [JsonPropertyName("team_event_stable_tile_x")]
    public int? TeamEventStableTileX { get; set; }

    [JsonPropertyName("team_event_stable_tile_y")]
    public int? TeamEventStableTileY { get; set; }

    [JsonPropertyName("team_event_stable_matches_owned_horse")]
    public bool? TeamEventStableMatchesOwnedHorse { get; set; }

    [JsonPropertyName("horse_flute_expected_result")]
    public string HorseFluteExpectedResult { get; set; } = string.Empty;

    [JsonPropertyName("horse_flute_use_delay_ms")]
    public int? HorseFluteUseDelayMs { get; set; }

    [JsonPropertyName("horse_flute_freeze_pause_ms")]
    public int? HorseFluteFreezePauseMs { get; set; }

    [JsonPropertyName("horse_flute_music_duck_ms")]
    public int? HorseFluteMusicDuckMs { get; set; }

    [JsonPropertyName("horse_flute_expected_facing_direction")]
    public int? HorseFluteExpectedFacingDirection { get; set; }
}
