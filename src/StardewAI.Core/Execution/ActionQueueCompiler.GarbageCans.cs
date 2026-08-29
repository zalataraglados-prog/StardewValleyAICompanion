using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string GarbageCanDataPayloadSha256 = "34621d9c92c472019c6e0a6bae4ac86a62576b7bccae4b9191590ed11e46911f";
    private const string GarbageCanNativeContract =
        "GameLocation.checkAction -> performAction Garbage -> CheckGarbage -> TryGetGarbageItem -> CheckedGarbage/stat/output/native NPC reaction; no direct checked-set, stat, friendship, inventory, debris, or RNG mutation";

    private static CompiledActionStep[] CompileRummageGarbageStep(SmallModelAction action)
    {
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        if (!x.HasValue || !y.HasValue) return Array.Empty<CompiledActionStep>();
        var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
        return new[]
        {
            Step("rummage_garbage", "current_location(" + x.Value + "," + y.Value + "):native_Garbage_CheckGarbage",
                "current_location.garbage_cans[" + ReadParameter(action, "garbage_can_id") + "].checked_today=true;trashCansChecked+=1;predicted_output=" + ReadParameter(action, "expected_output_json"), estimatedTicks)
        };
    }

    private static string[] ValidateRummageGarbagePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.rummage_garbage") return Array.Empty<string>();
        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var actionX = ReadIntParameter(action, "interaction_tile_x");
        var actionY = ReadIntParameter(action, "interaction_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var safeSlot = ReadIntParameter(action, "safe_slot_index");
        var restoreSlot = ReadIntParameter(action, "restore_slot_index");
        var checkedBefore = ReadParameter(action, "expected_checked_today_before");
        var statBefore = ReadIntParameter(action, "expected_trash_cans_checked_before");
        var statDelta = ReadIntParameter(action, "expected_trash_cans_checked_delta");
        var luck = double.TryParse(ReadParameter(action, "expected_daily_luck"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLuck)
            ? parsedLuck
            : (double?)null;
        if (!targetX.HasValue || !targetY.HasValue || !actionX.HasValue || !actionY.HasValue ||
            !standX.HasValue || !standY.HasValue || !safeSlot.HasValue || !restoreSlot.HasValue ||
            checkedBefore is null || !statBefore.HasValue || !statDelta.HasValue || !luck.HasValue)
            return new[] { "rummage_garbage_typed_target_fields_required" };
        if (targetX != actionX || targetY != actionY || Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1)
            reasons.Add("rummage_garbage_interaction_geometry_drifted");
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("rummage_garbage_menu_must_be_clear");
        if (!string.Equals(ReadParameter(action, "garbage_can_data_payload_sha256"), GarbageCanDataPayloadSha256, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "garbage_can_data_contract_status"), "exact_locked_base_1.6.15", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "garbage_can_prediction_status"), "exact_native_non_mutating_prediction", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "garbage_can_native_contract"), GarbageCanNativeContract, StringComparison.Ordinal))
            reasons.Add("rummage_garbage_native_contract_incomplete");
        if (!string.Equals(ReadParameter(action, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("rummage_garbage_target_location_mismatch");

        var cans = ReadStateFieldValue(snapshot, "current_location", "garbage_cans");
        var target = cans.HasValue && cans.Value.ValueKind == JsonValueKind.Array
            ? cans.Value.EnumerateArray().FirstOrDefault(can => ReadInt(can, "tile_x") == targetX && ReadInt(can, "tile_y") == targetY)
            : default;
        if (target.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("rummage_garbage_target_not_found_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        var freshOutput = target.TryGetProperty("expected_output", out var output) ? output.GetRawText() : "null";
        var freshReaction = target.TryGetProperty("reacting_npc", out var reaction) ? reaction.GetRawText() : "null";
        if (!string.Equals(ReadString(target, "rummage_status"), "ready", StringComparison.Ordinal))
            reasons.Add("rummage_garbage_not_ready_by_transparent_state");
        if (!string.Equals(ReadString(target, "action"), ReadParameter(action, "garbage_can_action"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(target, "garbage_can_id"), ReadParameter(action, "garbage_can_id"), StringComparison.Ordinal) ||
            ReadBool(target, "checked_today").ToString().ToLowerInvariant() != checkedBefore ||
            ReadInt(target, "trash_cans_checked_before") != statBefore || ReadInt(target, "expected_trash_cans_checked_delta") != statDelta ||
            Math.Abs(ReadDouble(target, "daily_luck") - luck.Value) > 1e-12 ||
            ReadBool(target, "alleyway_buffet_read").ToString().ToLowerInvariant() != ReadParameter(action, "expected_alleyway_buffet_read") ||
            ReadBool(target, "predicted_item_produced").ToString().ToLowerInvariant() != ReadParameter(action, "predicted_item_produced") ||
            !string.Equals(ReadString(target, "selected_entry_id"), ReadParameter(action, "selected_entry_id"), StringComparison.Ordinal) ||
            ReadBool(target, "selected_ignore_base_chance").ToString().ToLowerInvariant() != ReadParameter(action, "selected_ignore_base_chance") ||
            ReadBool(target, "selected_mega_success").ToString().ToLowerInvariant() != ReadParameter(action, "selected_mega_success") ||
            ReadBool(target, "selected_double_mega_success").ToString().ToLowerInvariant() != ReadParameter(action, "selected_double_mega_success") ||
            !string.Equals(ReadString(target, "output_delivery"), ReadParameter(action, "output_delivery"), StringComparison.Ordinal) ||
            !FruitTreeJsonEquivalent(freshOutput, ReadParameter(action, "expected_output_json") ?? string.Empty) ||
            !FruitTreeJsonEquivalent(freshReaction, ReadParameter(action, "reacting_npc_json") ?? string.Empty) ||
            ReadInt(target, "safe_slot_index", -1) != safeSlot || ReadInt(target, "restore_slot_index", -1) != restoreSlot ||
            !string.Equals(ReadString(target, "projection_fingerprint"), ReadParameter(action, "garbage_can_projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("rummage_garbage_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
