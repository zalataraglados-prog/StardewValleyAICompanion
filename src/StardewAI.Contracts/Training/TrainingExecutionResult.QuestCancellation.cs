using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("quest_cancellation_fingerprint")]
    public string QuestCancellationFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("quest_cancel_reason")]
    public string QuestCancelReason { get; set; } = string.Empty;

    [JsonPropertyName("quest_accepted_before")]
    public bool? QuestAcceptedBefore { get; set; }

    [JsonPropertyName("quest_accepted_after")]
    public bool? QuestAcceptedAfter { get; set; }

    [JsonPropertyName("quest_accepted_daily_before")]
    public bool? QuestAcceptedDailyBefore { get; set; }

    [JsonPropertyName("quest_accepted_daily_after")]
    public bool? QuestAcceptedDailyAfter { get; set; }

    [JsonPropertyName("quest_log_count_before")]
    public int? QuestLogCountBefore { get; set; }

    [JsonPropertyName("quest_log_count_after")]
    public int? QuestLogCountAfter { get; set; }
}
