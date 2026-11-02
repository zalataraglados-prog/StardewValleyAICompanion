using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("debug_end_of_night_ordinary_level_count")]
    public int? DebugEndOfNightOrdinaryLevelCount { get; set; }

    [JsonPropertyName("debug_end_of_night_farm_event")]
    public string? DebugEndOfNightFarmEvent { get; set; }

    [JsonPropertyName("debug_end_of_night_negative_profile")]
    public string? DebugEndOfNightNegativeProfile { get; set; }
}
