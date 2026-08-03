using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_spawned_object_profile")]
    public string FixtureSpawnedObjectProfile { get; set; } = string.Empty;

    [JsonPropertyName("expected_farming_experience_delta")]
    public int? ExpectedFarmingExperienceDelta { get; set; }
}
