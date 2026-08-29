using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("monster_musk_projection_fingerprint")]
    public string MonsterMuskProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_buff_id")]
    public string MonsterMuskBuffId { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_buff_active_before")]
    public bool? MonsterMuskBuffActiveBefore { get; set; }

    [JsonPropertyName("monster_musk_buff_remaining_before_ms")]
    public int? MonsterMuskBuffRemainingBeforeMs { get; set; }

    [JsonPropertyName("monster_musk_buff_total_before_ms")]
    public int? MonsterMuskBuffTotalBeforeMs { get; set; }

    [JsonPropertyName("monster_musk_buff_duration_ms")]
    public int? MonsterMuskBuffDurationMs { get; set; }

    [JsonPropertyName("monster_musk_buff_max_duration_ms")]
    public int? MonsterMuskBuffMaxDurationMs { get; set; }

    [JsonPropertyName("monster_musk_buff_is_debuff")]
    public bool? MonsterMuskBuffIsDebuff { get; set; }

    [JsonPropertyName("monster_musk_buff_icon_sprite_index")]
    public int? MonsterMuskBuffIconSpriteIndex { get; set; }

    [JsonPropertyName("monster_musk_buff_icon_texture")]
    public string MonsterMuskBuffIconTexture { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_buff_glow_color")]
    public string MonsterMuskBuffGlowColor { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_buff_effects_empty")]
    public bool? MonsterMuskBuffEffectsEmpty { get; set; }

    [JsonPropertyName("monster_musk_buff_actions_on_apply_count")]
    public int? MonsterMuskBuffActionsOnApplyCount { get; set; }

    [JsonPropertyName("monster_musk_buff_reapply_semantics")]
    public string MonsterMuskBuffReapplySemantics { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_ordinary_mine_spawn_multiplier")]
    public int? MonsterMuskOrdinaryMineSpawnMultiplier { get; set; }

    [JsonPropertyName("monster_musk_volcano_spawn_multiplier")]
    public int? MonsterMuskVolcanoSpawnMultiplier { get; set; }

    [JsonPropertyName("monster_musk_repellent_buff_id")]
    public string MonsterMuskRepellentBuffId { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_facing_direction")]
    public int? MonsterMuskFacingDirection { get; set; }

    [JsonPropertyName("monster_musk_freeze_pause_ms")]
    public int? MonsterMuskFreezePauseMs { get; set; }

    [JsonPropertyName("monster_musk_callback_delay_ms")]
    public int? MonsterMuskCallbackDelayMs { get; set; }

    [JsonPropertyName("monster_musk_followup_animation_ms")]
    public int? MonsterMuskFollowupAnimationMs { get; set; }

    [JsonPropertyName("monster_musk_sprite_count")]
    public int? MonsterMuskSpriteCount { get; set; }

    [JsonPropertyName("monster_musk_sprite_delays_ms")]
    public string MonsterMuskSpriteDelaysMs { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_sprite_motion_x_domain")]
    public string MonsterMuskSpriteMotionXDomain { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_initial_sound")]
    public string MonsterMuskInitialSound { get; set; } = string.Empty;

    [JsonPropertyName("monster_musk_callback_sound")]
    public string MonsterMuskCallbackSound { get; set; } = string.Empty;
}
