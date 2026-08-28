using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("singing_stone_safe_slot_kind")]
    public string SingingStoneSafeSlotKind { get; set; } = string.Empty;

    [JsonPropertyName("singing_stone_sound_name")]
    public string SingingStoneSoundName { get; set; } = string.Empty;

    [JsonPropertyName("singing_stone_pitch_rng_source")]
    public string SingingStonePitchRngSource { get; set; } = string.Empty;

    [JsonPropertyName("singing_stone_exact_next_pitch_status")]
    public string SingingStoneExactNextPitchStatus { get; set; } = string.Empty;

    [JsonPropertyName("singing_stone_pitch_min")]
    public int? SingingStonePitchMin { get; set; }

    [JsonPropertyName("singing_stone_pitch_max")]
    public int? SingingStonePitchMax { get; set; }

    [JsonPropertyName("singing_stone_pitch_step")]
    public int? SingingStonePitchStep { get; set; }

    [JsonPropertyName("singing_stone_pitch_outcome_count")]
    public int? SingingStonePitchOutcomeCount { get; set; }

    [JsonPropertyName("singing_stone_expected_shake_timer")]
    public int? SingingStoneExpectedShakeTimer { get; set; }

    [JsonPropertyName("singing_stone_expected_location_action_return")]
    public bool? SingingStoneExpectedLocationActionReturn { get; set; }
}
