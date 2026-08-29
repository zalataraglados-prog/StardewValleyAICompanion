using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("expected_firework_type")]
    public int? ExpectedFireworkType { get; set; }

    [JsonPropertyName("expected_firework_source_rect_x")]
    public int? ExpectedFireworkSourceRectX { get; set; }

    [JsonPropertyName("expected_firework_source_rect_y")]
    public int? ExpectedFireworkSourceRectY { get; set; }

    [JsonPropertyName("expected_firework_fuse_duration_ms")]
    public int? ExpectedFireworkFuseDurationMs { get; set; }

    [JsonPropertyName("expected_firework_rocket_delay_ms")]
    public int? ExpectedFireworkRocketDelayMs { get; set; }

    [JsonPropertyName("expected_firework_rocket_id_min")]
    public int? ExpectedFireworkRocketIdMin { get; set; }

    [JsonPropertyName("expected_firework_rocket_id_max")]
    public int? ExpectedFireworkRocketIdMax { get; set; }

    [JsonPropertyName("firework_acceleration_y_min")]
    public string FireworkAccelerationYMin { get; set; } = string.Empty;

    [JsonPropertyName("firework_acceleration_y_max")]
    public string FireworkAccelerationYMax { get; set; } = string.Empty;

    [JsonPropertyName("firework_acceleration_y_step")]
    public string FireworkAccelerationYStep { get; set; } = string.Empty;

    [JsonPropertyName("firework_random_contract")]
    public string FireworkRandomContract { get; set; } = string.Empty;
}
