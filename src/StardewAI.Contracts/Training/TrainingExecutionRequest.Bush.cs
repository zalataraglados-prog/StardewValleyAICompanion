using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_bush_profile")]
    public string FixtureBushProfile { get; set; } = string.Empty;
}
