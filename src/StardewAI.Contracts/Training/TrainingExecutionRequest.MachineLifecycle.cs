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
}
