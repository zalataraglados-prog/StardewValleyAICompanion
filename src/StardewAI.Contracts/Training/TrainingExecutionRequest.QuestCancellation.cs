using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("quest_cancellation_fingerprint")]
    public string QuestCancellationFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("quest_cancel_reason")]
    public string QuestCancelReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_quest_cancel")]
    public bool? ConfirmQuestCancel { get; set; }

    [JsonPropertyName("quest_expected_accepted_before")]
    public bool? QuestExpectedAcceptedBefore { get; set; }

    [JsonPropertyName("quest_expected_completed_before")]
    public bool? QuestExpectedCompletedBefore { get; set; }

    [JsonPropertyName("quest_expected_daily_quest")]
    public bool? QuestExpectedDailyQuest { get; set; }

    [JsonPropertyName("quest_expected_day_accepted")]
    public int? QuestExpectedDayAccepted { get; set; }

    [JsonPropertyName("quest_expected_days_left")]
    public int? QuestExpectedDaysLeft { get; set; }

    [JsonPropertyName("quest_log_count_before")]
    public int? QuestLogCountBefore { get; set; }

    [JsonPropertyName("quest_log_count_after")]
    public int? QuestLogCountAfter { get; set; }

    [JsonPropertyName("quest_accepted_daily_before")]
    public bool? QuestAcceptedDailyBefore { get; set; }

    [JsonPropertyName("quest_accepted_daily_after")]
    public bool? QuestAcceptedDailyAfter { get; set; }

    [JsonPropertyName("quest_resets_accepted_daily_quest")]
    public bool? QuestResetsAcceptedDailyQuest { get; set; }

    [JsonPropertyName("quest_cancellation_fixture_case")]
    public string QuestCancellationFixtureCase { get; set; } = string.Empty;
}
