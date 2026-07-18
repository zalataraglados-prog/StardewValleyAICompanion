using System;
using System.Collections.Generic;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidatePanOreSpotPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.pan_ore_spot")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var slot = ReadIntParameter(action, "tool_slot_index");
        var expectedJson = ReadParameter(action, "expected_output_items_json");
        var expectedStatsJson = ReadParameter(action, "expected_stat_increments_json");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue || !slot.HasValue || slot < 0 ||
            ReadParameter(action, "required_tool_kind") != "Pan" || ReadParameter(action, "target_runtime_type") != "StardewValley.Tools.Pan" ||
            string.IsNullOrWhiteSpace(expectedJson) || string.IsNullOrWhiteSpace(expectedStatsJson) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "native_receipt_callbacks_status")) || !ReadIntParameter(action, "expected_times_panned_before").HasValue ||
            !ReadIntParameter(action, "expected_times_panned_after").HasValue || !ReadIntParameter(action, "expected_mining_experience_delta").HasValue ||
            !ReadIntParameter(action, "expected_foraging_experience_delta").HasValue || string.IsNullOrWhiteSpace(ReadParameter(action, "post_use_ore_pan_point_status")))
        {
            return new[] { "pan_ore_spot_typed_projection_required" };
        }
        if (Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1)
        {
            reasons.Add("pan_ore_spot_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("pan_ore_spot_menu_must_be_clear");
        }
        if (!string.Equals(ReadParameter(action, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("pan_ore_spot_target_location_mismatch");
        }
        var value = ReadStateFieldValue(snapshot, "current_location", "panning");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("pan_ore_spot_projection_unavailable");
            return reasons.ToArray();
        }
        var pan = value.Value;
        if (ReadString(pan, "status") != "exact" || ReadBool(pan, "ore_pan_point_active") != true ||
            ReadInt(pan, "ore_pan_point_x") != x || ReadInt(pan, "ore_pan_point_y") != y || ReadInt(pan, "pan_tool_slot_index") != slot ||
            ReadInt(pan, "pan_upgrade_level") != ReadIntParameter(action, "pan_upgrade_level") ||
            ReadString(pan, "pan_enchantments_json") != ReadParameter(action, "pan_enchantments_json") ||
            ReadString(pan, "expected_output_items_json") != expectedJson ||
            ReadString(pan, "expected_receipt_stat_increments_json") != expectedStatsJson ||
            ReadString(pan, "native_receipt_callbacks_status") != ReadParameter(action, "native_receipt_callbacks_status"))
        {
            reasons.Add("pan_ore_spot_reward_projection_drifted");
        }
        if (ReadInt(pan, "times_panned_before") != ReadIntParameter(action, "expected_times_panned_before") ||
            ReadInt(pan, "times_panned_after") != ReadIntParameter(action, "expected_times_panned_after") ||
            ReadInt(pan, "mining_experience_before") != ReadIntParameter(action, "expected_mining_experience_before") ||
            ReadInt(pan, "mining_experience_delta") != ReadIntParameter(action, "expected_mining_experience_delta") ||
            ReadInt(pan, "foraging_experience_before") != ReadIntParameter(action, "expected_foraging_experience_before") ||
            ReadInt(pan, "foraging_experience_delta") != ReadIntParameter(action, "expected_foraging_experience_delta") ||
            ReadString(pan, "post_use_ore_pan_point_status") != ReadParameter(action, "post_use_ore_pan_point_status"))
        {
            reasons.Add("pan_ore_spot_side_effect_projection_drifted");
        }
        return reasons.ToArray();
    }
}
