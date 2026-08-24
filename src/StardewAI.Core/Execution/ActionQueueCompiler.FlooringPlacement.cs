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
    private const string FlooringInventoryRuntimeType = "StardewValley.Object";
    private const string FlooringPlacedRuntimeType = "StardewValley.TerrainFeatures.Flooring";
    private const string FlooringNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFloorPathItem)->terrainFeatures.Add(Flooring)";

    private static CompiledActionStep[] CompilePlaceFlooringStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        if (!slot.HasValue || !x.HasValue || !y.HasValue ||
            string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "place_flooring",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + qualifiedItemId,
                "current_location.terrain_features[" + x.Value + "," + y.Value + "].runtime_type=" + FlooringPlacedRuntimeType +
                    ";current_location.terrain_features[" + x.Value + "," + y.Value + "].floor_data_key=" +
                    ReadParameter(action, "floor_data_key") + ";current_location.terrain_features[" + x.Value + "," + y.Value +
                    "].derived_neighbor_mask=" + ReadParameter(action, "expected_neighbor_mask_after") +
                    ";player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlaceFlooringPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_flooring")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var expectedStack = ReadIntParameter(action, "inventory_stack_before");
        var expectedMask = ReadIntParameter(action, "expected_neighbor_mask_after");
        var expectedViewMin = ReadIntParameter(action, "expected_which_view_min");
        var expectedViewMax = ReadIntParameter(action, "expected_which_view_max");
        var baselineReachable = ReadIntParameter(action, "baseline_reachable_tile_count");
        var reachableAfter = ReadIntParameter(action, "reachable_tile_count_after_placement");
        var protectedCount = ReadIntParameter(action, "protected_access_group_count");
        var routeDistance = ReadIntParameter(action, "route_distance_tiles");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !expectedStack.HasValue || expectedStack.Value < 1 || !expectedMask.HasValue ||
            !expectedViewMin.HasValue || !expectedViewMax.HasValue || expectedViewMin.Value < 0 ||
            expectedViewMax.Value < expectedViewMin.Value || expectedViewMax.Value > 15 ||
            !baselineReachable.HasValue || !reachableAfter.HasValue || !protectedCount.HasValue ||
            !routeDistance.HasValue || string.IsNullOrWhiteSpace(location) ||
            !TryBoolParameter(action, "expected_passable", out var expectedPassable))
        {
            reasons.Add("place_flooring_typed_target_fields_required");
            return reasons.ToArray();
        }

        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        var itemId = ReadParameter(action, "item_id");
        var floorDataKey = ReadParameter(action, "floor_data_key");
        var connectType = ReadParameter(action, "connect_type");
        if (string.IsNullOrWhiteSpace(qualifiedItemId) || string.IsNullOrWhiteSpace(itemId) ||
            string.IsNullOrWhiteSpace(floorDataKey) || string.IsNullOrWhiteSpace(connectType) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), FlooringInventoryRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "target_runtime_type"), FlooringPlacedRuntimeType, StringComparison.Ordinal) ||
            !expectedPassable)
        {
            reasons.Add("place_flooring_exact_item_data_and_runtime_identity_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "flooring_layout_reason")))
        {
            reasons.Add("place_flooring_layout_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), FlooringNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_flooring_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_flooring_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_flooring_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - x.Value) + Math.Abs(standY.Value - y.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("place_flooring_adjacent_stand_geometry_invalid");
        }

        var context = ReadStateFieldValue(snapshot, "player", "flooring_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_flooring_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("place_flooring_projection_fingerprint_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, qualifiedItemId ?? string.Empty);
        JsonElement? locationRow = null;
        if (!row.HasValue || ReadInt(row.Value, "stack") != expectedStack.Value ||
            !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), FlooringInventoryRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "placed_runtime_type"), FlooringPlacedRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "floor_data_key"), floorDataKey, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "floor_data_item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "connect_type"), connectType, StringComparison.Ordinal) ||
            !ReadBool(row.Value, "expected_passable") ||
            ReadInt(row.Value, "expected_which_view_min", -1) != expectedViewMin.Value ||
            ReadInt(row.Value, "expected_which_view_max", -1) != expectedViewMax.Value)
        {
            reasons.Add("place_flooring_inventory_or_data_identity_drifted");
        }
        else
        {
            locationRow = PlacementLocationRow(row.Value, location!);
            var range = locationRow.HasValue ? PlacementRangeAt(locationRow.Value, x.Value, y.Value) : null;
            if (!locationRow.HasValue ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal) ||
                !range.HasValue)
            {
                reasons.Add("place_flooring_exact_tile_not_native_legal");
            }
            else if (ReadInt(range.Value, "expected_neighbor_mask_after", -1) != expectedMask.Value)
            {
                reasons.Add("place_flooring_neighbor_topology_drifted");
            }
        }

        if (locationRow.HasValue)
        {
            var layout = new StoragePlacementLayoutProjection().ValidateExactCurrentMapPassablePlacement(
                snapshot, locationRow.Value, x.Value, y.Value, standX.Value, standY.Value, "flooring_placement");
            if (!string.Equals(layout.Status, "available", StringComparison.Ordinal) ||
                layout.TargetTileX != x.Value || layout.TargetTileY != y.Value ||
                layout.StandTileX != standX.Value || layout.StandTileY != standY.Value ||
                layout.BaselineReachableTileCount != baselineReachable.Value ||
                layout.ReachableTileCountAfterPlacement != reachableAfter.Value ||
                layout.ProtectedAccessGroupCount != protectedCount.Value ||
                layout.RouteDistanceTiles != routeDistance.Value ||
                !string.Equals(layout.ProjectionBasis, ReadParameter(action, "layout_projection_basis"), StringComparison.Ordinal))
            {
                reasons.Add("place_flooring_passable_layout_drifted");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
