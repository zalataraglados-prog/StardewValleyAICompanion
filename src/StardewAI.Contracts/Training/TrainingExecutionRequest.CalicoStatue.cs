using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("calico_statue_projection_fingerprint")]
    public string CalicoStatueProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_accepted_effect_id")]
    public int? CalicoStatueAcceptedEffectId { get; set; }

    [JsonPropertyName("calico_statue_effect_key")]
    public string CalicoStatueEffectKey { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_strategy_polarity")]
    public string CalicoStatueStrategyPolarity { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_exact_effect")]
    public string CalicoStatueExactEffect { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_calico_egg_reward")]
    public int? CalicoStatueCalicoEggReward { get; set; }

    [JsonPropertyName("calico_statue_current_effects_csv")]
    public string CalicoStatueCurrentEffectsCsv { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_expected_effects_after_csv")]
    public string CalicoStatueExpectedEffectsAfterCsv { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_total_activated_before")]
    public int? CalicoStatueTotalActivatedBefore { get; set; }

    [JsonPropertyName("calico_statue_next_activation_number")]
    public int? CalicoStatueNextActivationNumber { get; set; }

    [JsonPropertyName("calico_statue_rating_before")]
    public int? CalicoStatueRatingBefore { get; set; }

    [JsonPropertyName("calico_statue_expected_rating_after")]
    public int? CalicoStatueExpectedRatingAfter { get; set; }

    [JsonPropertyName("calico_statue_average_daily_luck")]
    public double? CalicoStatueAverageDailyLuck { get; set; }

    [JsonPropertyName("calico_statue_days_played")]
    public int? CalicoStatueDaysPlayed { get; set; }

    [JsonPropertyName("calico_statue_unique_game_id_half")]
    public string CalicoStatueUniqueGameIdHalf { get; set; } = string.Empty;

    [JsonPropertyName("calico_statue_use_legacy_random")]
    public bool? CalicoStatueUseLegacyRandom { get; set; }

    [JsonPropertyName("calico_statue_mine_level")]
    public int? CalicoStatueMineLevel { get; set; }

    [JsonPropertyName("calico_statue_festival_day")]
    public int? CalicoStatueFestivalDay { get; set; }

    [JsonPropertyName("calico_statue_tile_index_before")]
    public int? CalicoStatueTileIndexBefore { get; set; }

    [JsonPropertyName("calico_statue_tile_index_after")]
    public int? CalicoStatueTileIndexAfter { get; set; }

    [JsonPropertyName("calico_statue_eggs_before")]
    public int? CalicoStatueEggsBefore { get; set; }

    [JsonPropertyName("calico_statue_health_before")]
    public int? CalicoStatueHealthBefore { get; set; }

    [JsonPropertyName("calico_statue_max_health")]
    public int? CalicoStatueMaxHealth { get; set; }

    [JsonPropertyName("calico_statue_stamina_before")]
    public double? CalicoStatueStaminaBefore { get; set; }

    [JsonPropertyName("calico_statue_max_stamina")]
    public double? CalicoStatueMaxStamina { get; set; }

    [JsonPropertyName("calico_statue_fixture_effect_id")]
    public int? CalicoStatueFixtureEffectId { get; set; }
}
