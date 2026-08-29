using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_fruit_tree_profile")]
    public string FixtureFruitTreeProfile { get; set; } = string.Empty;

    [JsonPropertyName("fruit_tree_id")]
    public string FruitTreeId { get; set; } = string.Empty;

    [JsonPropertyName("expected_fruit_count_before")]
    public int? ExpectedFruitCountBefore { get; set; }

    [JsonPropertyName("expected_fruit_count_after")]
    public int? ExpectedFruitCountAfter { get; set; }

    [JsonPropertyName("fruit_tree_projection_status")]
    public string FruitTreeProjectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("fruit_tree_native_contract")]
    public string FruitTreeNativeContract { get; set; } = string.Empty;
}
