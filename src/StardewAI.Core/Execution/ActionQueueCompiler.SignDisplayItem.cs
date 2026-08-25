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
    private const string SignDisplayItemRuntimeType = "StardewValley.Objects.Sign";
    private const string SignDisplayItemNativeContract =
        "GameLocation.checkAction->Sign.checkForAction(CurrentItem.getOne,no_inventory_consumption)->displayItem/displayType";

    private static CompiledActionStep[] CompileSetSignDisplayItemStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        var qid = ReadParameter(action, "qualified_item_id");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(qid))
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "set_sign_display_item",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + qid,
                "current_location.objects[" + x.Value + "," + y.Value + "].sign_state.display_item=" + qid +
                    ";current_location.objects[" + x.Value + "," + y.Value + "].sign_state.display_type=" + ReadParameter(action, "expected_display_type") +
                    ";player.inventory[" + slot.Value + "].stack_and_state=unchanged",
                45)
        };
    }

    private static string[] ValidateSetSignDisplayItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.set_sign_display_item")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var stack = ReadIntParameter(action, "expected_stack_before");
        var quality = ReadIntParameter(action, "source_quality");
        var expectedDisplayType = ReadIntParameter(action, "expected_display_type");
        var previousDisplayType = ReadIntParameter(action, "previous_display_type");
        var location = ReadParameter(action, "target_location");
        var itemId = ReadParameter(action, "item_id");
        var qid = ReadParameter(action, "qualified_item_id");
        var sourceType = ReadParameter(action, "source_runtime_type");
        var sourceHash = ReadParameter(action, "source_state_sha256");
        var targetQid = ReadParameter(action, "target_qualified_item_id");
        var targetHash = ReadParameter(action, "target_state_sha256");
        if (!slot.HasValue || !targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            !stack.HasValue || stack.Value < 1 || !quality.HasValue || !expectedDisplayType.HasValue ||
            expectedDisplayType.Value is < 1 or > 5 || !previousDisplayType.HasValue ||
            string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(qid) ||
            string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceHash) ||
            string.IsNullOrWhiteSpace(targetQid) || string.IsNullOrWhiteSpace(targetHash) ||
            !TryBoolParameter(action, "replace_existing_display", out var replaceExisting) ||
            !TryBoolParameter(action, "allow_replace_existing_display", out var allowReplacement))
        {
            return new[] { "set_sign_display_item_typed_projection_required" };
        }

        if (!string.Equals(ReadParameter(action, "target_runtime_type"), SignDisplayItemRuntimeType, StringComparison.Ordinal))
        {
            reasons.Add("set_sign_display_item_exact_base_sign_required");
        }
        if (replaceExisting != allowReplacement)
        {
            reasons.Add(replaceExisting
                ? "set_sign_display_item_replacement_not_authorized"
                : "set_sign_display_item_unexpected_replacement_authorization");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "sign_display_reason")))
        {
            reasons.Add("set_sign_display_item_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), SignDisplayItemNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("set_sign_display_item_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("set_sign_display_item_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("set_sign_display_item_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("set_sign_display_item_adjacent_stand_geometry_invalid");
        }

        var target = FindSignDisplayTarget(snapshot, targetX.Value, targetY.Value);
        if (!target.HasValue ||
            !string.Equals(ReadString(target.Value, "type"), SignDisplayItemRuntimeType, StringComparison.Ordinal) ||
            !target.Value.TryGetProperty("sign_state", out var signState) || signState.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(signState, "placement_kind"), "display_item_sign", StringComparison.Ordinal) ||
            !string.Equals(ReadString(signState, "status"), "available", StringComparison.Ordinal) ||
            !signState.TryGetProperty("display_assignment", out var assignment) || assignment.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(assignment, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("set_sign_display_item_target_not_ready_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (!string.Equals(ReadString(assignment, "target_location"), location, StringComparison.OrdinalIgnoreCase) ||
            ReadInt(assignment, "target_tile_x") != targetX.Value || ReadInt(assignment, "target_tile_y") != targetY.Value ||
            !string.Equals(ReadString(assignment, "target_runtime_type"), SignDisplayItemRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(assignment, "target_qualified_item_id"), targetQid, StringComparison.Ordinal) ||
            !string.Equals(ReadString(assignment, "target_state_sha256"), targetHash, StringComparison.Ordinal) ||
            !string.Equals(ReadString(assignment, "target_projection_fingerprint"), ReadParameter(action, "target_projection_fingerprint"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(assignment, "native_contract"), SignDisplayItemNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("set_sign_display_item_target_projection_drifted");
        }
        if (ReadBool(assignment, "replace_existing_display") != replaceExisting ||
            ReadInt(assignment, "previous_display_type") != previousDisplayType.Value ||
            !string.Equals(ReadString(assignment, "previous_display_item_qualified_item_id"), ReadParameter(action, "previous_display_item_qualified_item_id") ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(ReadString(assignment, "previous_display_item_runtime_type"), ReadParameter(action, "previous_display_item_runtime_type") ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(ReadString(assignment, "previous_display_item_state_sha256"), ReadParameter(action, "previous_display_item_state_sha256") ?? string.Empty, StringComparison.Ordinal))
        {
            reasons.Add("set_sign_display_item_previous_payload_drifted");
        }

        var row = FindSignDisplayInventoryRow(assignment, slot.Value, qid);
        if (!row.HasValue || ReadInt(row.Value, "stack") != stack.Value || ReadInt(row.Value, "quality") != quality.Value ||
            !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "source_runtime_type"), sourceType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "source_state_status"), "exact_live_direct_serialization", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "source_state_sha256"), sourceHash, StringComparison.Ordinal) ||
            ReadInt(row.Value, "expected_display_type") != expectedDisplayType.Value ||
            ReadInt(row.Value, "expected_source_stack_after") != stack.Value ||
            ReadInt(row.Value, "expected_display_stack") != 1)
        {
            reasons.Add("set_sign_display_item_source_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? FindSignDisplayTarget(SnapshotEnvelope snapshot, int x, int y)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var item in objects.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && ReadInt(item, "tile_x") == x && ReadInt(item, "tile_y") == y)
            {
                return item;
            }
        }
        return null;
    }

    private static JsonElement? FindSignDisplayInventoryRow(JsonElement assignment, int slot, string qid)
    {
        if (!assignment.TryGetProperty("inventory_rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "inventory_slot_index", -1) == slot &&
                string.Equals(ReadString(row, "qualified_item_id"), qid, StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }
}
