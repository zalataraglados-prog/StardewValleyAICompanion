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
    private const string ReturnScepterNativeContract =
        "Farmer.BeginUsingTool->Tool.beginUsing(InstantUse)->Game1.toolAnimationDone->Wand.DoFunction->1000ms_wandWarpForReal->Utility.getHomeOfFarmer(player).getFrontDoorSpot->Game1.warpFarmer(Farm)";

    private static CompiledActionStep[] CompileUseReturnScepterStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "front_door_tile_x");
        var y = ReadIntParameter(action, "front_door_tile_y");
        if (!slot.HasValue || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("use_return_scepter",
                "Farm:" + x.Value + "," + y.Value + ":slot" + slot.Value + ":(T)ReturnScepter",
                "inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                ";home_location_id=" + ReadParameter(action, "home_location_id") +
                ";is_cabin=" + ReadParameter(action, "home_is_cabin"), 120)
        };
    }

    private static string[] ValidateUseReturnScepterPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_return_scepter")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var before = ReadIntParameter(action, "inventory_stack_before");
        var after = ReadIntParameter(action, "inventory_stack_after");
        if (!slot.HasValue || !before.HasValue || !after.HasValue ||
            !string.Equals(ReadParameter(action, "item_id"), "ReturnScepter", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "qualified_item_id"), "(T)ReturnScepter", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Tools.Wand", StringComparison.Ordinal))
            return new[] { "use_return_scepter_typed_fields_required" };
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("use_return_scepter_menu_must_be_clear");
        var currentLocation = ReadStateFieldValue(snapshot, "player", "location_id");
        if (!currentLocation.HasValue || currentLocation.Value.ValueKind != JsonValueKind.String ||
            !string.Equals(ReadParameter(action, "source_location_id"), currentLocation.Value.GetString(), StringComparison.Ordinal))
            reasons.Add("use_return_scepter_source_location_drifted");

        var context = ReadStateFieldValue(snapshot, "player", "return_scepter");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("use_return_scepter_projection_unavailable").Distinct(StringComparer.Ordinal).ToArray();
        if (!string.Equals(ReadParameter(action, "return_scepter_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("use_return_scepter_projection_fingerprint_drifted");
        if (!string.Equals(ReadString(context.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_return_scepter_native_effect_gate_blocked");

        JsonElement? row = null;
        if (context.Value.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            row = rows.EnumerateArray().FirstOrDefault(value => ReadInt(value, "inventory_slot_index", -1) == slot);
        if (!row.HasValue ||
            !string.Equals(ReadString(row.Value, "item_id"), "ReturnScepter", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "qualified_item_id"), "(T)ReturnScepter", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Tools.Wand", StringComparison.Ordinal) ||
            ReadInt(row.Value, "stack_before", -1) != before || ReadInt(row.Value, "stack_after", -1) != after ||
            before != 1 || after != 1 ||
            ReadBool(row.Value, "reusable_tool") != true)
            reasons.Add("use_return_scepter_inventory_identity_drifted");

        var destination = context.Value.GetProperty("destination");
        if (!string.Equals(ReadParameter(action, "target_location"), "Farm", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "destination_location_id"), ReadString(destination, "destination_location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "home_location_id"), ReadString(destination, "home_location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "home_runtime_type"), ReadString(destination, "home_runtime_type"), StringComparison.Ordinal) ||
            ReadIntParameter(action, "front_door_tile_x") != ReadInt(destination, "front_door_tile_x") ||
            ReadIntParameter(action, "front_door_tile_y") != ReadInt(destination, "front_door_tile_y") ||
            ReadBoolParameter(action, "home_is_cabin") != ReadBool(destination, "home_is_cabin") ||
            ReadBoolParameter(action, "already_at_destination") != ReadBool(destination, "already_at_destination") ||
            ReadBool(destination, "already_at_destination") != false)
            reasons.Add("use_return_scepter_destination_drifted");

        var animation = context.Value.GetProperty("animation_contract");
        if (ReadBoolParameter(action, "native_instant_use") != ReadBool(animation, "instant_use") ||
            ReadIntParameter(action, "native_facing_direction") != ReadInt(animation, "facing_direction") ||
            ReadIntParameter(action, "native_callback_delay_ms") != ReadInt(animation, "callback_delay_ms") ||
            ReadIntParameter(action, "native_freeze_pause_ms") != ReadInt(animation, "freeze_pause_ms") ||
            ReadIntParameter(action, "native_poof_sprite_count") != ReadInt(animation, "poof_sprite_count") ||
            ReadIntParameter(action, "native_trail_sprite_count") != ReadInt(animation, "trail_sprite_count") ||
            ReadIntParameter(action, "native_trail_delay_step_ms") != ReadInt(animation, "trail_delay_step_ms") ||
            ReadIntParameter(action, "native_trail_max_delay_ms") != ReadInt(animation, "trail_max_delay_ms") ||
            !string.Equals(ReadParameter(action, "native_sound"), ReadString(animation, "sound"), StringComparison.Ordinal))
            reasons.Add("use_return_scepter_animation_contract_drifted");
        if (!string.Equals(ReadParameter(action, "native_contract"), ReturnScepterNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "native_contract"), ReturnScepterNativeContract, StringComparison.Ordinal))
            reasons.Add("use_return_scepter_native_contract_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
