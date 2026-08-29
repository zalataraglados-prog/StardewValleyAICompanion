using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("treasure_totem_projection_fingerprint")]
    public string TreasureTotemProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("treasure_totem_center_tile_x")]
    public int? TreasureTotemCenterTileX { get; set; }

    [JsonPropertyName("treasure_totem_center_tile_y")]
    public int? TreasureTotemCenterTileY { get; set; }

    [JsonPropertyName("treasure_totem_ring_candidate_count")]
    public int? TreasureTotemRingCandidateCount { get; set; }

    [JsonPropertyName("treasure_totem_expected_spawn_count")]
    public int? TreasureTotemExpectedSpawnCount { get; set; }

    [JsonPropertyName("treasure_totem_expected_spawn_tiles_json")]
    public string TreasureTotemExpectedSpawnTilesJson { get; set; } = string.Empty;

    [JsonPropertyName("treasure_totem_existing_artifact_spot_count_before")]
    public int? TreasureTotemExistingArtifactSpotCountBefore { get; set; }

    [JsonPropertyName("treasure_totem_existing_artifact_spot_count_after")]
    public int? TreasureTotemExistingArtifactSpotCountAfter { get; set; }

    [JsonPropertyName("treasure_totems_used_before")]
    public int? TreasureTotemsUsedBefore { get; set; }

    [JsonPropertyName("treasure_totems_used_after")]
    public int? TreasureTotemsUsedAfter { get; set; }

    [JsonPropertyName("treasure_totem_ring_scan_radius")]
    public int? TreasureTotemRingScanRadius { get; set; }

    [JsonPropertyName("treasure_totem_rounded_radius")]
    public int? TreasureTotemRoundedRadius { get; set; }

    [JsonPropertyName("treasure_totem_artifact_spot_qualified_item_id")]
    public string TreasureTotemArtifactSpotQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("treasure_totem_initial_sound")]
    public string TreasureTotemInitialSound { get; set; } = string.Empty;
}
