using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string MonsterMuskNativeContract =
        "Object.performUseAction((O)879)->750ms_callback_Object.MonsterMusk->Farmer.applyBuff(24)->BuffManager.Apply_remove_then_replace";

    private static CompiledActionStep[] CompileUseMonsterMuskStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(location))
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("use_monster_musk", location + ":slot" + slot.Value + ":(O)879",
                "inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                ";buff_id=24;buff_duration_ms=600000;ordinary_mine_multiplier=2;volcano_multiplier=2", 180)
        };
    }

    private static string[] ValidateUseMonsterMuskPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_monster_musk")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var before = ReadIntParameter(action, "inventory_stack_before");
        var after = ReadIntParameter(action, "inventory_stack_after");
        if (!slot.HasValue || !before.HasValue || before < 1 || !after.HasValue ||
            !string.Equals(ReadParameter(action, "item_id"), "879", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "qualified_item_id"), "(O)879", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal))
            return new[] { "use_monster_musk_typed_fields_required" };
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("use_monster_musk_menu_must_be_clear");
        if (!TargetLocationMatchesCurrent(action, snapshot))
            reasons.Add("use_monster_musk_requires_loaded_target_location");

        var context = ReadStateFieldValue(snapshot, "player", "monster_musk");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("use_monster_musk_projection_unavailable").Distinct(StringComparer.Ordinal).ToArray();
        if (!string.Equals(ReadParameter(action, "monster_musk_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("use_monster_musk_projection_fingerprint_drifted");
        if (!string.Equals(ReadString(context.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_monster_musk_native_use_gate_blocked");

        JsonElement? row = null;
        if (context.Value.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            row = rows.EnumerateArray().FirstOrDefault(value => ReadInt(value, "inventory_slot_index", -1) == slot);
        if (!row.HasValue || !string.Equals(ReadString(row.Value, "item_id"), "879", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "qualified_item_id"), "(O)879", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            ReadBool(row.Value, "temporarily_invisible") == true || after != before - 1 || ReadInt(row.Value, "stack_before", -1) != before ||
            ReadInt(row.Value, "stack_after", -1) != after)
            reasons.Add("use_monster_musk_inventory_identity_drifted");

        var buff = context.Value.GetProperty("buff_contract");
        if (!string.Equals(ReadParameter(action, "buff_id"), ReadString(buff, "id"), StringComparison.Ordinal) ||
            ReadIntParameter(action, "buff_duration_ms") != ReadInt(buff, "duration_ms") ||
            ReadIntParameter(action, "buff_max_duration_ms") != ReadInt(buff, "max_duration_ms") ||
            ReadBoolParameter(action, "buff_is_debuff") != ReadBool(buff, "is_debuff") ||
            ReadIntParameter(action, "buff_icon_sprite_index") != ReadInt(buff, "icon_sprite_index") ||
            !string.Equals(ReadParameter(action, "buff_icon_texture"), ReadString(buff, "icon_texture"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "buff_glow_color"), ReadString(buff, "glow_color"), StringComparison.OrdinalIgnoreCase) ||
            ReadBoolParameter(action, "buff_effects_empty") != ReadBool(buff, "effects_empty") ||
            ReadIntParameter(action, "buff_actions_on_apply_count") != ReadInt(buff, "actions_on_apply_count") ||
            !string.Equals(ReadParameter(action, "buff_reapply_semantics"), ReadString(buff, "reapply_semantics"), StringComparison.Ordinal))
            reasons.Add("use_monster_musk_buff_contract_drifted");

        var active = context.Value.GetProperty("active_buff");
        if (ReadBoolParameter(action, "buff_active_before") != ReadBool(active, "active") ||
            ReadIntParameter(action, "buff_remaining_before_ms") != ReadInt(active, "remaining_ms") ||
            ReadIntParameter(action, "buff_total_before_ms") != ReadInt(active, "total_ms"))
            reasons.Add("use_monster_musk_active_buff_drifted");
        var spawn = context.Value.GetProperty("spawn_semantics");
        if (ReadIntParameter(action, "ordinary_mine_spawn_multiplier") != ReadInt(spawn, "ordinary_mine_multiplier") ||
            ReadIntParameter(action, "volcano_spawn_multiplier") != ReadInt(spawn, "volcano_multiplier") ||
            !string.Equals(ReadParameter(action, "repellent_buff_id"), ReadString(spawn, "repellent_buff_id"), StringComparison.Ordinal))
            reasons.Add("use_monster_musk_spawn_semantics_drifted");
        var animation = context.Value.GetProperty("animation_contract");
        if (ReadIntParameter(action, "native_facing_direction") != ReadInt(animation, "facing_direction") ||
            ReadIntParameter(action, "native_freeze_pause_ms") != ReadInt(animation, "freeze_pause_ms") ||
            ReadIntParameter(action, "native_callback_delay_ms") != ReadInt(animation, "callback_delay_ms") ||
            ReadIntParameter(action, "native_followup_animation_ms") != ReadInt(animation, "followup_animation_ms") ||
            ReadIntParameter(action, "native_sprite_count") != ReadInt(animation, "sprite_count") ||
            !string.Equals(ReadParameter(action, "native_sprite_delays_ms"), ReadString(animation, "sprite_delays_ms"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_sprite_motion_x_domain"), ReadString(animation, "sprite_motion_x_domain"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_initial_sound"), ReadString(animation, "initial_sound"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_callback_sound"), ReadString(animation, "callback_sound"), StringComparison.Ordinal))
            reasons.Add("use_monster_musk_animation_contract_drifted");
        if (!string.Equals(ReadParameter(action, "native_contract"), MonsterMuskNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "native_contract"), MonsterMuskNativeContract, StringComparison.Ordinal))
            reasons.Add("use_monster_musk_native_contract_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
