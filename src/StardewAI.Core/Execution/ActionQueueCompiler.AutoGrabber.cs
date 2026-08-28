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
    private static readonly string[] AutoGrabberBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id",
        "auto_grabber_held_container_runtime_type", "auto_grabber_contents_before_json",
        "auto_grabber_transferable_contents_json", "auto_grabber_remaining_contents_json",
        "auto_grabber_content_stack_count_before", "auto_grabber_transferable_stack_count",
        "auto_grabber_expected_stack_count_after", "auto_grabber_content_quantity_before",
        "auto_grabber_expected_transfer_quantity", "auto_grabber_expected_quantity_after",
        "auto_grabber_expected_location_action_return", "safe_slot_index", "safe_slot_kind",
        "restore_slot_index", "interaction_kind", "expected_action_type", "native_contract",
        "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildAutoGrabberParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !AutoGrabberBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectAutoGrabberCompilerTarget(action, snapshot);
        var safeContext = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        if (target is null || !safeContext.HasValue || safeContext.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var safeKind = ReadString(safeContext.Value, "safe_slot_kind");
        var safeSlot = ReadInt(safeContext.Value, "safe_slot_index");
        var restoreSlot = ReadInt(safeContext.Value, "current_tool_index");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11 ||
            (safeKind != "empty" && safeKind != "tool"))
        {
            return parameters.ToArray();
        }

        parameters.AddRange(new[]
        {
            Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("item_id", target.ItemId),
            Parameter("qualified_item_id", target.QualifiedItemId),
            Parameter("auto_grabber_held_container_runtime_type", target.HeldContainerRuntimeType),
            Parameter("auto_grabber_contents_before_json", target.ContentsBeforeJson),
            Parameter("auto_grabber_transferable_contents_json", target.TransferableContentsJson),
            Parameter("auto_grabber_remaining_contents_json", target.RemainingContentsJson),
            Parameter("auto_grabber_content_stack_count_before", target.ContentStackCountBefore.ToString()),
            Parameter("auto_grabber_transferable_stack_count", target.TransferableStackCount.ToString()),
            Parameter("auto_grabber_expected_stack_count_after", target.ExpectedStackCountAfter.ToString()),
            Parameter("auto_grabber_content_quantity_before", target.ContentQuantityBefore.ToString()),
            Parameter("auto_grabber_expected_transfer_quantity", target.ExpectedTransferQuantity.ToString()),
            Parameter("auto_grabber_expected_quantity_after", target.ExpectedQuantityAfter.ToString()),
            Parameter("auto_grabber_expected_location_action_return", target.ExpectedLocationActionReturn ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileAutoGrabberStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundAutoGrabberAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var stacks = ReadIntParameter(bound, "auto_grabber_transferable_stack_count");
        var quantity = ReadIntParameter(bound, "auto_grabber_expected_transfer_quantity");
        if (!x.HasValue || !y.HasValue || !stacks.HasValue || stacks <= 0 ||
            !quantity.HasValue || quantity <= 0)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "collect_auto_grabber_contents",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):(BC)165",
                "auto_grabber.contents_stacks-=" + stacks.Value +
                    ";player.inventory_quantity+=" + quantity.Value +
                    ";unfittable_stacks_unchanged=true;selected_slot_restored=true;fresh_snapshot_replan_required=true",
                900)
        };
    }

    private static string[] ValidateAutoGrabberPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "animals.collect_auto_grabber_contents")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("auto_grabber_menu_must_be_clear");
        var safeContext = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        var safeKind = safeContext.HasValue && safeContext.Value.ValueKind == JsonValueKind.Object
            ? ReadString(safeContext.Value, "safe_slot_kind")
            : "unavailable";
        if (!safeContext.HasValue || safeContext.Value.ValueKind != JsonValueKind.Object ||
            (safeKind != "empty" && safeKind != "tool"))
        {
            reasons.Add("auto_grabber_safe_toolbar_slot_required");
        }

        var target = SelectAutoGrabberCompilerTarget(action, snapshot);
        var bound = BoundAutoGrabberAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != (safeContext.HasValue ? ReadInt(safeContext.Value, "safe_slot_index") : -1) ||
            ReadIntParameter(bound, "restore_slot_index") != (safeContext.HasValue ? ReadInt(safeContext.Value, "current_tool_index") : -1) ||
            ReadIntParameter(bound, "auto_grabber_content_stack_count_before") != target.ContentStackCountBefore ||
            ReadIntParameter(bound, "auto_grabber_transferable_stack_count") != target.TransferableStackCount ||
            ReadIntParameter(bound, "auto_grabber_expected_stack_count_after") != target.ExpectedStackCountAfter ||
            ReadIntParameter(bound, "auto_grabber_content_quantity_before") != target.ContentQuantityBefore ||
            ReadIntParameter(bound, "auto_grabber_expected_transfer_quantity") != target.ExpectedTransferQuantity ||
            ReadIntParameter(bound, "auto_grabber_expected_quantity_after") != target.ExpectedQuantityAfter ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "auto_grabber_held_container_runtime_type"), target.HeldContainerRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "auto_grabber_contents_before_json"), target.ContentsBeforeJson, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "auto_grabber_transferable_contents_json"), target.TransferableContentsJson, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "auto_grabber_remaining_contents_json"), target.RemainingContentsJson, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "auto_grabber_expected_location_action_return"), target.ExpectedLocationActionReturn ? "true" : "false", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safeKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal))
        {
            reasons.Add("auto_grabber_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundAutoGrabberAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildAutoGrabberParameters(action, snapshot)
        };

    private static AutoGrabberCompilerTarget? SelectAutoGrabberCompilerTarget(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var requestedX = ReadIntParameter(action, "target_tile_x");
        var requestedY = ReadIntParameter(action, "target_tile_y");
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!requestedX.HasValue || !requestedY.HasValue || !objects.HasValue ||
            objects.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var row = objects.Value.EnumerateArray().FirstOrDefault(item =>
            ReadInt(item, "tile_x") == requestedX.Value &&
            ReadInt(item, "tile_y") == requestedY.Value &&
            item.TryGetProperty("auto_grabber_collection", out var projection) &&
            projection.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(projection, "status"), "ready", StringComparison.Ordinal));
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty("auto_grabber_collection", out var value) ||
            !value.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var stand = stands.EnumerateArray()
            .Where(item => ReadBool(item, "available") == true)
            .Select(item => new
            {
                X = ReadInt(item, "tile_x"),
                Y = ReadInt(item, "tile_y"),
                Distance = Math.Abs(playerX - ReadInt(item, "tile_x")) + Math.Abs(playerY - ReadInt(item, "tile_y"))
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Y)
            .ThenBy(item => item.X)
            .FirstOrDefault();
        if (stand is null)
            return null;
        return new AutoGrabberCompilerTarget(
            requestedX.Value, requestedY.Value, stand.X, stand.Y,
            ReadString(value, "target_runtime_type"),
            ReadString(value, "canonical_item_id"),
            ReadString(value, "canonical_qualified_item_id"),
            ReadString(value, "held_container_runtime_type"),
            ReadString(value, "contents_before_json"),
            ReadString(value, "transferable_contents_json"),
            ReadString(value, "remaining_contents_json"),
            ReadInt(value, "content_stack_count_before"),
            ReadInt(value, "transferable_stack_count"),
            ReadInt(value, "expected_stack_count_after"),
            ReadInt(value, "content_quantity_before"),
            ReadInt(value, "expected_transfer_quantity"),
            ReadInt(value, "expected_quantity_after"),
            ReadBool(value, "expected_native_location_action_return") == true,
            ReadString(value, "interaction_kind"),
            ReadString(value, "expected_action_type"),
            ReadString(value, "native_contract"));
    }

    private sealed record AutoGrabberCompilerTarget(
        int TargetX, int TargetY, int StandX, int StandY,
        string RuntimeType, string ItemId, string QualifiedItemId, string HeldContainerRuntimeType,
        string ContentsBeforeJson, string TransferableContentsJson, string RemainingContentsJson,
        int ContentStackCountBefore, int TransferableStackCount, int ExpectedStackCountAfter,
        int ContentQuantityBefore, int ExpectedTransferQuantity, int ExpectedQuantityAfter,
        bool ExpectedLocationActionReturn, string InteractionKind, string ExpectedActionType,
        string NativeContract);
}
