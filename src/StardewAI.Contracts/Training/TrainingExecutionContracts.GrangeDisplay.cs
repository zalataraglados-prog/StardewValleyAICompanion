using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("grange_projection_fingerprint")]
    public string GrangeProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("grange_interaction_tile_x")]
    public int? GrangeInteractionTileX { get; set; }

    [JsonPropertyName("grange_interaction_tile_y")]
    public int? GrangeInteractionTileY { get; set; }

    [JsonPropertyName("grange_stand_tile_x")]
    public int? GrangeStandTileX { get; set; }

    [JsonPropertyName("grange_stand_tile_y")]
    public int? GrangeStandTileY { get; set; }

    [JsonPropertyName("grange_judged")]
    public bool? GrangeJudged { get; set; }

    [JsonPropertyName("grange_objective")]
    public string GrangeObjective { get; set; } = string.Empty;

    [JsonPropertyName("grange_operation")]
    public string GrangeOperation { get; set; } = string.Empty;

    [JsonPropertyName("grange_display_slot_index")]
    public int? GrangeDisplaySlotIndex { get; set; }

    [JsonPropertyName("grange_inventory_slot_index")]
    public int? GrangeInventorySlotIndex { get; set; }

    [JsonPropertyName("grange_inventory_stack_before")]
    public int? GrangeInventoryStackBefore { get; set; }

    [JsonPropertyName("grange_inventory_stack_after")]
    public int? GrangeInventoryStackAfter { get; set; }

    [JsonPropertyName("grange_sink_inventory_slot_index")]
    public int? GrangeSinkInventorySlotIndex { get; set; }

    [JsonPropertyName("grange_item_runtime_type")]
    public string GrangeItemRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("grange_item_quality")]
    public int? GrangeItemQuality { get; set; }

    [JsonPropertyName("grange_actual_sell_price")]
    public int? GrangeActualSellPrice { get; set; }

    [JsonPropertyName("grange_item_points")]
    public int? GrangeItemPoints { get; set; }

    [JsonPropertyName("grange_scoring_group")]
    public string GrangeScoringGroup { get; set; } = string.Empty;

    [JsonPropertyName("grange_score_before")]
    public int? GrangeScoreBefore { get; set; }

    [JsonPropertyName("grange_score_after")]
    public int? GrangeScoreAfter { get; set; }

    [JsonPropertyName("grange_occupied_slots_before")]
    public int? GrangeOccupiedSlotsBefore { get; set; }

    [JsonPropertyName("grange_occupied_slots_after")]
    public int? GrangeOccupiedSlotsAfter { get; set; }

    [JsonPropertyName("grange_best_available_score")]
    public int? GrangeBestAvailableScore { get; set; }

    [JsonPropertyName("grange_first_place_score")]
    public int? GrangeFirstPlaceScore { get; set; }
}
