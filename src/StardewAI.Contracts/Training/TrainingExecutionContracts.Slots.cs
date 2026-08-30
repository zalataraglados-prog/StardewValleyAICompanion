using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("slots_projection_fingerprint")]
    public string SlotsProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("slots_action_raw")]
    public string SlotsActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("slots_action_token")]
    public string SlotsActionToken { get; set; } = string.Empty;
    [JsonPropertyName("slots_bet")]
    public int? SlotsBet { get; set; }
    [JsonPropertyName("slots_club_coins_before")]
    public int? SlotsClubCoinsBefore { get; set; }
    [JsonPropertyName("slots_target_club_coins")]
    public int? SlotsTargetClubCoins { get; set; }
    [JsonPropertyName("slots_remaining_club_coin_demand")]
    public int? SlotsRemainingClubCoinDemand { get; set; }
    [JsonPropertyName("slots_target_item_id")]
    public string SlotsTargetItemId { get; set; } = string.Empty;
    [JsonPropertyName("slots_times_played_before")]
    public int? SlotsTimesPlayedBefore { get; set; }
    [JsonPropertyName("slots_daily_luck")]
    public double? SlotsDailyLuck { get; set; }
    [JsonPropertyName("slots_luck_level")]
    public int? SlotsLuckLevel { get; set; }
    [JsonPropertyName("slots_luck_multiplier")]
    public double? SlotsLuckMultiplier { get; set; }
    [JsonPropertyName("slots_expected_payout_multiplier")]
    public double? SlotsExpectedPayoutMultiplier { get; set; }
    [JsonPropertyName("slots_expected_net_coin_delta")]
    public double? SlotsExpectedNetCoinDelta { get; set; }
    [JsonPropertyName("slots_payout_rows_json")]
    public string SlotsPayoutRowsJson { get; set; } = string.Empty;
    [JsonPropertyName("slots_rng_contract")]
    public string SlotsRngContract { get; set; } = string.Empty;
    [JsonPropertyName("slots_exit_policy")]
    public string SlotsExitPolicy { get; set; } = string.Empty;
    [JsonPropertyName("slots_fixture_case")]
    public string SlotsFixtureCase { get; set; } = string.Empty;
}
