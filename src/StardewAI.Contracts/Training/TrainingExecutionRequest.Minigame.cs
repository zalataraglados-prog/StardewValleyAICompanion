using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("minigame_id")]
    public string MinigameId { get; set; } = string.Empty;

    [JsonPropertyName("minigame_mode")]
    public int? MinigameMode { get; set; }

    [JsonPropertyName("minigame_target_score")]
    public int? MinigameTargetScore { get; set; }

    [JsonPropertyName("minigame_max_attempts")]
    public int? MinigameMaxAttempts { get; set; }
}
