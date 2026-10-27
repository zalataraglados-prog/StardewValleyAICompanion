using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("expected_animal_door_open_before")]
    public bool? ExpectedAnimalDoorOpenBefore { get; set; }

    [JsonPropertyName("expected_animal_door_open_after")]
    public bool? ExpectedAnimalDoorOpenAfter { get; set; }
}
