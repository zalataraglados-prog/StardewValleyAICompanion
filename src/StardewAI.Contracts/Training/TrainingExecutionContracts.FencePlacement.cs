using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fence_data_key")]
    public string FenceDataKey { get; set; } = string.Empty;

    [JsonPropertyName("expected_fence_is_gate")]
    public bool? ExpectedFenceIsGate { get; set; }

    [JsonPropertyName("expected_fence_draw_sum")]
    public int? ExpectedFenceDrawSum { get; set; }

    [JsonPropertyName("expected_fence_gate_functional")]
    public bool? ExpectedFenceGateFunctional { get; set; }

    [JsonPropertyName("expected_fence_health_min")]
    public double? ExpectedFenceHealthMin { get; set; }

    [JsonPropertyName("expected_fence_health_max")]
    public double? ExpectedFenceHealthMax { get; set; }

    [JsonPropertyName("expected_fence_max_health_min")]
    public double? ExpectedFenceMaxHealthMin { get; set; }

    [JsonPropertyName("expected_fence_max_health_max")]
    public double? ExpectedFenceMaxHealthMax { get; set; }
}
