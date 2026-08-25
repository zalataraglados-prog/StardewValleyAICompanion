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
    private const string FurnitureNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Furniture.placementAction->Object.placementAction->location.furniture_or_table.heldObject";

    private static CompiledActionStep[] CompilePlaceFurnitureStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(location))
        {
            return Array.Empty<CompiledActionStep>();
        }
        var endpoint = ReadParameter(action, "placement_endpoint");
        var expected = string.Equals(endpoint, "table_held_object", StringComparison.Ordinal)
            ? "current_location.furniture[" + ReadParameter(action, "table_index") + "].held_object=" + ReadParameter(action, "qualified_item_id")
            : "current_location.furniture.add(anchor=" + ReadParameter(action, "expected_anchor_x") + "," +
                ReadParameter(action, "expected_anchor_y") + ";runtime_type=" + ReadParameter(action, "target_runtime_type") + ")";
        return new[]
        {
            Step(
                "place_furniture",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":rotation" +
                    ReadParameter(action, "desired_current_rotation"),
                expected + ";player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlaceFurniturePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_furniture")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var stack = ReadIntParameter(action, "inventory_stack_before");
        var inventoryRotation = ReadIntParameter(action, "inventory_current_rotation");
        var desiredRotation = ReadIntParameter(action, "desired_current_rotation");
        var rotationSteps = ReadIntParameter(action, "rotation_steps_from_inventory");
        var anchorX = ReadIntParameter(action, "expected_anchor_x");
        var anchorY = ReadIntParameter(action, "expected_anchor_y");
        var footprintWidth = ReadIntParameter(action, "footprint_width");
        var footprintHeight = ReadIntParameter(action, "footprint_height");
        var baseline = ReadIntParameter(action, "baseline_reachable_tile_count");
        var reachableAfter = ReadIntParameter(action, "reachable_tile_count_after_placement");
        var protectedCount = ReadIntParameter(action, "protected_access_group_count");
        var routeDistance = ReadIntParameter(action, "route_distance_tiles");
        if (!slot.HasValue || !targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            !stack.HasValue || stack.Value < 1 || !inventoryRotation.HasValue || !desiredRotation.HasValue ||
            !rotationSteps.HasValue || rotationSteps.Value < 0 || rotationSteps.Value > 3 || !anchorX.HasValue ||
            !anchorY.HasValue || !footprintWidth.HasValue || footprintWidth.Value < 1 || !footprintHeight.HasValue ||
            footprintHeight.Value < 1 || !baseline.HasValue || !reachableAfter.HasValue || !protectedCount.HasValue ||
            !routeDistance.HasValue || !TryBoolParameter(action, "can_free_place_furniture", out var canFreePlace) ||
            !TryBoolParameter(action, "expected_passable", out var expectedPassable))
        {
            reasons.Add("place_furniture_typed_target_rotation_and_layout_fields_required");
            return reasons.ToArray();
        }

        var location = ReadParameter(action, "target_location");
        var qid = ReadParameter(action, "qualified_item_id");
        var itemId = ReadParameter(action, "item_id");
        var inventoryType = ReadParameter(action, "inventory_runtime_type");
        var targetType = ReadParameter(action, "target_runtime_type");
        var endpoint = ReadParameter(action, "placement_endpoint");
        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(itemId) ||
            string.IsNullOrWhiteSpace(inventoryType) || string.IsNullOrWhiteSpace(targetType) ||
            endpoint is not ("location_furniture" or "table_held_object"))
        {
            reasons.Add("place_furniture_exact_identity_and_endpoint_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "furniture_layout_reason")))
        {
            reasons.Add("place_furniture_layout_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), FurnitureNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_furniture_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_furniture_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_furniture_requires_loaded_target_location");
        }

        var context = ReadStateFieldValue(snapshot, "player", "furniture_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_furniture_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("place_furniture_projection_fingerprint_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, qid ?? string.Empty);
        JsonElement? rotation = null;
        JsonElement? range = null;
        if (!row.HasValue || !ReadBool(row.Value, "runtime_type_supported") || ReadInt(row.Value, "stack") != stack.Value ||
            ReadInt(row.Value, "inventory_current_rotation", -1) != inventoryRotation.Value ||
            !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), inventoryType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "expected_placed_runtime_type"), targetType, StringComparison.Ordinal))
        {
            reasons.Add("place_furniture_inventory_or_factory_identity_drifted");
        }
        else if (row.Value.TryGetProperty("rotations", out var rotations) && rotations.ValueKind == JsonValueKind.Array)
        {
            rotation = rotations.EnumerateArray().FirstOrDefault(candidate =>
                ReadInt(candidate, "desired_current_rotation", -1) == desiredRotation.Value &&
                ReadInt(candidate, "rotation_steps_from_inventory", -1) == rotationSteps.Value);
            if (rotation.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(rotation.Value, "location_id"), location, StringComparison.OrdinalIgnoreCase) ||
                ReadBool(rotation.Value, "can_free_place_furniture") != canFreePlace)
            {
                reasons.Add("place_furniture_rotation_or_location_projection_drifted");
                rotation = null;
            }
            else
            {
                range = PlacementRangeAt(rotation.Value, targetX.Value, targetY.Value);
            }
        }
        else
        {
            reasons.Add("place_furniture_rotation_projection_unavailable");
        }

        if (!range.HasValue ||
            targetX.Value + ReadInt(range.Value, "anchor_offset_x") != anchorX.Value ||
            targetY.Value + ReadInt(range.Value, "anchor_offset_y") != anchorY.Value ||
            ReadInt(range.Value, "footprint_width") != footprintWidth.Value ||
            ReadInt(range.Value, "footprint_height") != footprintHeight.Value ||
            ReadBool(range.Value, "expected_passable") != expectedPassable ||
            !string.Equals(ReadString(range.Value, "placement_endpoint"), endpoint, StringComparison.Ordinal) ||
            ReadInt(range.Value, "table_index", -1) != ReadIntParameter(action, "table_index").GetValueOrDefault(-1) ||
            ReadInt(range.Value, "table_tile_x", -1) != ReadIntParameter(action, "table_tile_x").GetValueOrDefault(-1) ||
            ReadInt(range.Value, "table_tile_y", -1) != ReadIntParameter(action, "table_tile_y").GetValueOrDefault(-1))
        {
            reasons.Add("place_furniture_exact_target_footprint_or_endpoint_drifted");
        }

        if (rotation.HasValue && range.HasValue)
        {
            var layout = new StoragePlacementLayoutProjection().ValidateExactCurrentMapFurniturePlacement(
                snapshot, rotation.Value, range.Value, targetX.Value, targetY.Value, standX.Value, standY.Value, canFreePlace);
            if (layout.Status != "available" || layout.TargetTileX != targetX.Value || layout.TargetTileY != targetY.Value ||
                layout.StandTileX != standX.Value || layout.StandTileY != standY.Value ||
                layout.BaselineReachableTileCount != baseline.Value || layout.ReachableTileCountAfterPlacement != reachableAfter.Value ||
                layout.ProtectedAccessGroupCount != protectedCount.Value || layout.RouteDistanceTiles != routeDistance.Value ||
                !string.Equals(layout.ProjectionBasis, ReadParameter(action, "layout_projection_basis"), StringComparison.Ordinal))
            {
                reasons.Add("place_furniture_route_or_access_layout_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
