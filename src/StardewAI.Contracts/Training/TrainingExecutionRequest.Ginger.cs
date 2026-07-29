using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_ginger_profile")]
    public string FixtureGingerProfile { get; set; } = string.Empty;
}
