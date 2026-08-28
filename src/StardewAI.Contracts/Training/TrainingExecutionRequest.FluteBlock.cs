using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("flute_block_safe_slot_kind")]
    public string FluteBlockSafeSlotKind { get; set; } = string.Empty;
    [JsonPropertyName("flute_block_current_pitch_raw")]
    public string FluteBlockCurrentPitchRaw { get; set; } = string.Empty;
    [JsonPropertyName("flute_block_current_pitch")]
    public int? FluteBlockCurrentPitch { get; set; }
    [JsonPropertyName("flute_block_next_pitch")]
    public int? FluteBlockNextPitch { get; set; }
    [JsonPropertyName("flute_block_pitch_min")]
    public int? FluteBlockPitchMin { get; set; }
    [JsonPropertyName("flute_block_pitch_max")]
    public int? FluteBlockPitchMax { get; set; }
    [JsonPropertyName("flute_block_pitch_step")]
    public int? FluteBlockPitchStep { get; set; }
    [JsonPropertyName("flute_block_pitch_state_count")]
    public int? FluteBlockPitchStateCount { get; set; }
    [JsonPropertyName("flute_block_sound_cue")]
    public string FluteBlockSoundCue { get; set; } = string.Empty;
    [JsonPropertyName("flute_block_expected_shake_timer")]
    public int? FluteBlockExpectedShakeTimer { get; set; }
    [JsonPropertyName("flute_block_expected_scale_y")]
    public float? FluteBlockExpectedScaleY { get; set; }
    [JsonPropertyName("flute_block_expected_location_action_return")]
    public bool? FluteBlockExpectedLocationActionReturn { get; set; }
}
