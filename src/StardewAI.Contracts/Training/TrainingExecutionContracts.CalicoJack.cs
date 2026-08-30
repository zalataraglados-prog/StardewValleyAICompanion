using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("calico_projection_fingerprint")]
    public string CalicoProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("calico_action_raw")]
    public string CalicoActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("calico_action_token")]
    public string CalicoActionToken { get; set; } = string.Empty;
    [JsonPropertyName("calico_table_kind")]
    public string CalicoTableKind { get; set; } = string.Empty;
    [JsonPropertyName("calico_bet")]
    public int? CalicoBet { get; set; }
    [JsonPropertyName("calico_dialogue_key")]
    public string CalicoDialogueKey { get; set; } = string.Empty;
    [JsonPropertyName("calico_play_response_key")]
    public string CalicoPlayResponseKey { get; set; } = string.Empty;
    [JsonPropertyName("calico_club_coins_before")]
    public int? CalicoClubCoinsBefore { get; set; }
    [JsonPropertyName("calico_target_club_coins")]
    public int? CalicoTargetClubCoins { get; set; }
    [JsonPropertyName("calico_remaining_club_coin_demand")]
    public int? CalicoRemainingClubCoinDemand { get; set; }
    [JsonPropertyName("calico_target_item_id")]
    public string CalicoTargetItemId { get; set; } = string.Empty;
    [JsonPropertyName("calico_times_played_seed")]
    public int? CalicoTimesPlayedSeed { get; set; }
    [JsonPropertyName("calico_days_played_seed")]
    public int? CalicoDaysPlayedSeed { get; set; }
    [JsonPropertyName("calico_unique_game_id_seed")]
    public string CalicoUniqueGameIdSeed { get; set; } = string.Empty;
    [JsonPropertyName("calico_daily_luck")]
    public double? CalicoDailyLuck { get; set; }
    [JsonPropertyName("calico_luck_level")]
    public int? CalicoLuckLevel { get; set; }
    [JsonPropertyName("calico_player_cards_json")]
    public string CalicoPlayerCardsJson { get; set; } = string.Empty;
    [JsonPropertyName("calico_dealer_cards_json")]
    public string CalicoDealerCardsJson { get; set; } = string.Empty;
    [JsonPropertyName("calico_recommended_first_action")]
    public string CalicoRecommendedFirstAction { get; set; } = string.Empty;
    [JsonPropertyName("calico_projected_next_hit_card")]
    public int? CalicoProjectedNextHitCard { get; set; }
    [JsonPropertyName("calico_coin_delta_per_low_bet")]
    public int? CalicoCoinDeltaPerLowBet { get; set; }
    [JsonPropertyName("calico_expected_coin_delta")]
    public int? CalicoExpectedCoinDelta { get; set; }
    [JsonPropertyName("calico_projected_outcome")]
    public string CalicoProjectedOutcome { get; set; } = string.Empty;
    [JsonPropertyName("calico_decision_policy")]
    public string CalicoDecisionPolicy { get; set; } = string.Empty;
    [JsonPropertyName("calico_exit_policy")]
    public string CalicoExitPolicy { get; set; } = string.Empty;
    [JsonPropertyName("calico_fixture_case")]
    public string CalicoFixtureCase { get; set; } = string.Empty;
}
