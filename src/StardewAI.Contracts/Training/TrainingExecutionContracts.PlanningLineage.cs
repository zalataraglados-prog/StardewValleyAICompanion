using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("source_planning_option_id")]
    public string SourcePlanningOptionId { get; set; } = string.Empty;
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("source_planning_option_id")]
    public string SourcePlanningOptionId { get; set; } = string.Empty;
}
