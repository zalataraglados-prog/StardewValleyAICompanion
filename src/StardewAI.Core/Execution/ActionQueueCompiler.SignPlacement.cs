using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string SignPlacementNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(sign_item_or_TextSign)->location.objects";

    private static CompiledActionStep[] CompilePlaceSignStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        var qid = ReadParameter(action, "qualified_item_id");
        var kind = ReadParameter(action, "placement_kind");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(qid) || kind is not ("display_item_sign" or "text_sign"))
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step("place_sign", location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + qid,
                "current_location.objects[" + x.Value + "," + y.Value + "].sign_state.placement_kind=" + kind +
                ";current_location.objects[" + x.Value + "," + y.Value + "].qualified_item_id=" + qid +
                ";player.inventory[" + slot.Value + "].stack_decreases=1", 60)
        };
    }

    private static string[] ValidatePlaceSignPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_sign")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var stack = ReadIntParameter(action, "inventory_stack_before");
        var displayType = ReadIntParameter(action, "expected_display_type");
        var baseline = ReadIntParameter(action, "baseline_reachable_tile_count");
        var after = ReadIntParameter(action, "reachable_tile_count_after_placement");
        var protectedCount = ReadIntParameter(action, "protected_access_group_count");
        var distance = ReadIntParameter(action, "route_distance_tiles");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !stack.HasValue || stack.Value < 1 || !displayType.HasValue || displayType.Value != 0 ||
            !baseline.HasValue || !after.HasValue || !protectedCount.HasValue || !distance.HasValue ||
            !TryBoolParameter(action, "expected_passable", out var expectedPassable) ||
            !TryBoolParameter(action, "expected_display_item_empty", out var expectedDisplayEmpty) ||
            !TryBoolParameter(action, "expected_show_next_index", out var expectedShowNext))
        {
            reasons.Add("place_sign_typed_target_and_layout_fields_required");
            return reasons.ToArray();
        }

        var location = ReadParameter(action, "target_location");
        var qid = ReadParameter(action, "qualified_item_id");
        var itemId = ReadParameter(action, "item_id");
        var inventoryType = ReadParameter(action, "inventory_runtime_type");
        var targetType = ReadParameter(action, "target_runtime_type");
        var kind = ReadParameter(action, "placement_kind");
        var expectedText = ReadParameter(action, "expected_sign_text");
        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(itemId) ||
            !string.Equals(inventoryType, "StardewValley.Object", StringComparison.Ordinal) ||
            kind is not ("display_item_sign" or "text_sign") || string.IsNullOrWhiteSpace(targetType) ||
            !expectedDisplayEmpty || expectedPassable || !string.IsNullOrEmpty(expectedText) ||
            (kind == "display_item_sign" && expectedShowNext) || (kind == "text_sign" && !expectedShowNext))
        {
            reasons.Add("place_sign_exact_empty_sign_identity_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "sign_layout_reason")))
        {
            reasons.Add("place_sign_layout_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), SignPlacementNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_sign_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_sign_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_sign_requires_loaded_target_location");
        }

        var context = ReadStateFieldValue(snapshot, "player", "sign_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_sign_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("place_sign_projection_fingerprint_drifted");
        }
        var row = PlacementInventoryRow(context.Value, slot.Value, qid ?? string.Empty);
        JsonElement? locationRow = null;
        if (!row.HasValue || ReadInt(row.Value, "stack") != stack.Value ||
            !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), inventoryType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "expected_placed_runtime_type"), targetType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "placement_kind"), kind, StringComparison.Ordinal) ||
            ReadBool(row.Value, "expected_passable") != expectedPassable ||
            ReadBool(row.Value, "expected_display_item_empty") != expectedDisplayEmpty ||
            ReadInt(row.Value, "expected_display_type", -1) != displayType.Value ||
            !string.Equals(ReadString(row.Value, "expected_sign_text"), expectedText, StringComparison.Ordinal) ||
            ReadBool(row.Value, "expected_show_next_index") != expectedShowNext)
        {
            reasons.Add("place_sign_inventory_data_or_branch_identity_drifted");
        }
        else
        {
            locationRow = PlacementLocationRow(row.Value, location!);
            var range = locationRow.HasValue ? PlacementRangeAt(locationRow.Value, x.Value, y.Value) : null;
            if (!locationRow.HasValue ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal) ||
                !range.HasValue)
            {
                reasons.Add("place_sign_exact_tile_not_native_legal");
            }
        }
        if (locationRow.HasValue)
        {
            var layout = new StoragePlacementLayoutProjection().ValidateExactCurrentMapBlockingPlacement(
                snapshot, locationRow.Value, x.Value, y.Value, standX.Value, standY.Value, "sign_placement");
            if (layout.Status != "available" || layout.TargetTileX != x.Value || layout.TargetTileY != y.Value ||
                layout.StandTileX != standX.Value || layout.StandTileY != standY.Value ||
                layout.BaselineReachableTileCount != baseline.Value || layout.ReachableTileCountAfterPlacement != after.Value ||
                layout.ProtectedAccessGroupCount != protectedCount.Value || layout.RouteDistanceTiles != distance.Value ||
                !string.Equals(layout.ProjectionBasis, ReadParameter(action, "layout_projection_basis"), StringComparison.Ordinal))
            {
                reasons.Add("place_sign_route_or_access_layout_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
