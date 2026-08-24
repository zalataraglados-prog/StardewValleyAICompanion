using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("floor_data_key")]
    public string FloorDataKey { get; set; } = string.Empty;

    [JsonPropertyName("flooring_connect_type")]
    public string FlooringConnectType { get; set; } = string.Empty;

    [JsonPropertyName("expected_flooring_neighbor_mask")]
    public int? ExpectedFlooringNeighborMask { get; set; }

    [JsonPropertyName("expected_flooring_view_min")]
    public int? ExpectedFlooringViewMin { get; set; }

    [JsonPropertyName("expected_flooring_view_max")]
    public int? ExpectedFlooringViewMax { get; set; }
}
