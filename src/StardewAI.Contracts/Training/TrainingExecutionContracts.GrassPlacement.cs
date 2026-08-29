using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("expected_grass_type")]
    public int? ExpectedGrassType { get; set; }

    [JsonPropertyName("expected_initial_number_of_weeds")]
    public int? ExpectedInitialNumberOfWeeds { get; set; }

    [JsonPropertyName("grass_placement_sound")]
    public string GrassPlacementSound { get; set; } = string.Empty;
}
