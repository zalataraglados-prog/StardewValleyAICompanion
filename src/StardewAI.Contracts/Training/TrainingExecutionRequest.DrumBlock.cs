using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("drum_block_safe_slot_kind")]
    public string DrumBlockSafeSlotKind { get; set; } = string.Empty;
    [JsonPropertyName("drum_block_current_tone_raw")]
    public string DrumBlockCurrentToneRaw { get; set; } = string.Empty;
    [JsonPropertyName("drum_block_current_tone")]
    public int? DrumBlockCurrentTone { get; set; }
    [JsonPropertyName("drum_block_next_tone")]
    public int? DrumBlockNextTone { get; set; }
    [JsonPropertyName("drum_block_tone_min")]
    public int? DrumBlockToneMin { get; set; }
    [JsonPropertyName("drum_block_tone_max")]
    public int? DrumBlockToneMax { get; set; }
    [JsonPropertyName("drum_block_tone_step")]
    public int? DrumBlockToneStep { get; set; }
    [JsonPropertyName("drum_block_tone_state_count")]
    public int? DrumBlockToneStateCount { get; set; }
    [JsonPropertyName("drum_block_sound_cue")]
    public string DrumBlockSoundCue { get; set; } = string.Empty;
    [JsonPropertyName("drum_block_expected_shake_timer")]
    public int? DrumBlockExpectedShakeTimer { get; set; }
    [JsonPropertyName("drum_block_expected_scale_y")]
    public float? DrumBlockExpectedScaleY { get; set; }
    [JsonPropertyName("drum_block_expected_location_action_return")]
    public bool? DrumBlockExpectedLocationActionReturn { get; set; }
}
