using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("process_input_qualified_item_id")]
    public string ProcessInputQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("process_input_quantity")]
    public int? ProcessInputQuantity { get; set; }

    [JsonPropertyName("process_additional_items_json")]
    public string ProcessAdditionalItemsJson { get; set; } = string.Empty;

    [JsonPropertyName("relocation_intent_id")]
    public string RelocationIntentId { get; set; } = string.Empty;

    [JsonPropertyName("machine_removal_projection_fingerprint")]
    public string MachineRemovalProjectionFingerprint { get; set; } =
        string.Empty;

    [JsonPropertyName("tool_qualified_item_id")]
    public string ToolQualifiedItemId { get; set; } = string.Empty;
}
