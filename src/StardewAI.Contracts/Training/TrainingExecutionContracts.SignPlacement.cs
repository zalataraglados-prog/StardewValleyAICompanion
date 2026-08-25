using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("sign_placement_kind")]
    public string SignPlacementKind { get; set; } = string.Empty;

    [JsonPropertyName("sign_expected_passable")]
    public bool? SignExpectedPassable { get; set; }

    [JsonPropertyName("sign_expected_display_item_empty")]
    public bool? SignExpectedDisplayItemEmpty { get; set; }

    [JsonPropertyName("sign_expected_display_type")]
    public int? SignExpectedDisplayType { get; set; }

    [JsonPropertyName("sign_expected_text")]
    public string SignExpectedText { get; set; } = string.Empty;

    [JsonPropertyName("sign_expected_show_next_index")]
    public bool? SignExpectedShowNextIndex { get; set; }
}
