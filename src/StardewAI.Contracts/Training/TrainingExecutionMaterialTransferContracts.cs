using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_material_transfer_profile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FixtureMaterialTransferProfile { get; set; }

    [JsonPropertyName("material_transfer_intent")]
    public MaterialTransferIntent? MaterialTransferIntent { get; set; }

    [JsonPropertyName("material_transfer_projection")]
    public MaterialTransferProjection? MaterialTransferProjection { get; set; }
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("material_transfer_intent")]
    public MaterialTransferIntent? MaterialTransferIntent { get; set; }

    [JsonPropertyName("material_transfer_projection")]
    public MaterialTransferProjection? MaterialTransferProjection { get; set; }

    [JsonPropertyName("material_transfer_click_count")]
    public int? MaterialTransferClickCount { get; set; }

    [JsonPropertyName("material_transfer_source_stack_before")]
    public int? MaterialTransferSourceStackBefore { get; set; }

    [JsonPropertyName("material_transfer_source_stack_after")]
    public int? MaterialTransferSourceStackAfter { get; set; }

    [JsonPropertyName("material_transfer_source_slot_after")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaterialTransferSourceSlotAfter { get; set; }

    [JsonPropertyName("material_transfer_destination_quantity_before")]
    public int? MaterialTransferDestinationQuantityBefore { get; set; }

    [JsonPropertyName("material_transfer_destination_quantity_after")]
    public int? MaterialTransferDestinationQuantityAfter { get; set; }

    [JsonPropertyName("material_transfer_native_menu_opened")]
    public bool? MaterialTransferNativeMenuOpened { get; set; }

    [JsonPropertyName("material_transfer_native_lock_released")]
    public bool? MaterialTransferNativeLockReleased { get; set; }
}
