using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("furniture_inventory_rotation_before")]
    public int? FurnitureInventoryRotationBefore { get; set; }

    [JsonPropertyName("furniture_desired_rotation")]
    public int? FurnitureDesiredRotation { get; set; }

    [JsonPropertyName("furniture_rotation_steps")]
    public int? FurnitureRotationSteps { get; set; }

    [JsonPropertyName("furniture_type")]
    public int? FurnitureType { get; set; }

    [JsonPropertyName("furniture_can_free_place")]
    public bool? FurnitureCanFreePlace { get; set; }

    [JsonPropertyName("furniture_expected_passable")]
    public bool? FurnitureExpectedPassable { get; set; }

    [JsonPropertyName("furniture_placement_endpoint")]
    public string FurniturePlacementEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("furniture_expected_anchor_x")]
    public int? FurnitureExpectedAnchorX { get; set; }

    [JsonPropertyName("furniture_expected_anchor_y")]
    public int? FurnitureExpectedAnchorY { get; set; }

    [JsonPropertyName("furniture_footprint_width")]
    public int? FurnitureFootprintWidth { get; set; }

    [JsonPropertyName("furniture_footprint_height")]
    public int? FurnitureFootprintHeight { get; set; }

    [JsonPropertyName("furniture_table_index")]
    public int? FurnitureTableIndex { get; set; }

    [JsonPropertyName("furniture_table_tile_x")]
    public int? FurnitureTableTileX { get; set; }

    [JsonPropertyName("furniture_table_tile_y")]
    public int? FurnitureTableTileY { get; set; }
}
