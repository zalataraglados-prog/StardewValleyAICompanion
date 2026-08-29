using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyMonsterMuskRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_monster_musk", StringComparison.Ordinal))
            return;
        request.MonsterMuskProjectionFingerprint = ReadQueueParameterString(item, "monster_musk_projection_fingerprint");
        request.MonsterMuskBuffId = ReadQueueParameterString(item, "buff_id");
        request.MonsterMuskBuffActiveBefore = ReadQueueParameterBool(item, "buff_active_before");
        request.MonsterMuskBuffRemainingBeforeMs = ReadQueueParameterInt(item, "buff_remaining_before_ms");
        request.MonsterMuskBuffTotalBeforeMs = ReadQueueParameterInt(item, "buff_total_before_ms");
        request.MonsterMuskBuffDurationMs = ReadQueueParameterInt(item, "buff_duration_ms");
        request.MonsterMuskBuffMaxDurationMs = ReadQueueParameterInt(item, "buff_max_duration_ms");
        request.MonsterMuskBuffIsDebuff = ReadQueueParameterBool(item, "buff_is_debuff");
        request.MonsterMuskBuffIconSpriteIndex = ReadQueueParameterInt(item, "buff_icon_sprite_index");
        request.MonsterMuskBuffIconTexture = ReadQueueParameterString(item, "buff_icon_texture");
        request.MonsterMuskBuffGlowColor = ReadQueueParameterString(item, "buff_glow_color");
        request.MonsterMuskBuffEffectsEmpty = ReadQueueParameterBool(item, "buff_effects_empty");
        request.MonsterMuskBuffActionsOnApplyCount = ReadQueueParameterInt(item, "buff_actions_on_apply_count");
        request.MonsterMuskBuffReapplySemantics = ReadQueueParameterString(item, "buff_reapply_semantics");
        request.MonsterMuskOrdinaryMineSpawnMultiplier = ReadQueueParameterInt(item, "ordinary_mine_spawn_multiplier");
        request.MonsterMuskVolcanoSpawnMultiplier = ReadQueueParameterInt(item, "volcano_spawn_multiplier");
        request.MonsterMuskRepellentBuffId = ReadQueueParameterString(item, "repellent_buff_id");
        request.MonsterMuskFacingDirection = ReadQueueParameterInt(item, "native_facing_direction");
        request.MonsterMuskFreezePauseMs = ReadQueueParameterInt(item, "native_freeze_pause_ms");
        request.MonsterMuskCallbackDelayMs = ReadQueueParameterInt(item, "native_callback_delay_ms");
        request.MonsterMuskFollowupAnimationMs = ReadQueueParameterInt(item, "native_followup_animation_ms");
        request.MonsterMuskSpriteCount = ReadQueueParameterInt(item, "native_sprite_count");
        request.MonsterMuskSpriteDelaysMs = ReadQueueParameterString(item, "native_sprite_delays_ms");
        request.MonsterMuskSpriteMotionXDomain = ReadQueueParameterString(item, "native_sprite_motion_x_domain");
        request.MonsterMuskInitialSound = ReadQueueParameterString(item, "native_initial_sound");
        request.MonsterMuskCallbackSound = ReadQueueParameterString(item, "native_callback_sound");
    }
}
