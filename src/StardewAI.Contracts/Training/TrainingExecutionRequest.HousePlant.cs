using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("house_plant_current_sprite_index")]
    public int? HousePlantCurrentSpriteIndex { get; set; }

    [JsonPropertyName("house_plant_expected_sprite_index")]
    public int? HousePlantExpectedSpriteIndex { get; set; }

    [JsonPropertyName("house_plant_expected_object_action_calls")]
    public int? HousePlantExpectedObjectActionCalls { get; set; }

    [JsonPropertyName("house_plant_expected_location_action_return")]
    public bool? HousePlantExpectedLocationActionReturn { get; set; }
}
