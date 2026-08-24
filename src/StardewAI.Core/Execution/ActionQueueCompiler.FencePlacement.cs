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
    private const string FenceInventoryRuntimeType = "StardewValley.Object";
    private const string FencePlacedRuntimeType = "StardewValley.Fence";
    private const string FenceNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFenceItem)->Fence(tile,item_id,is_gate)";

    private static CompiledActionStep[] CompilePlaceFenceStep(SmallModelAction action)
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
                "place_fence",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + qualifiedItemId,
                "current_location.objects[" + x.Value + "," + y.Value + "].runtime_type=" + FencePlacedRuntimeType +
                    ";current_location.objects[" + x.Value + "," + y.Value + "].qualified_item_id=" + qualifiedItemId +
                    ";current_location.objects[" + x.Value + "," + y.Value + "].fence_state.draw_sum=" +
                    ReadParameter(action, "expected_draw_sum_after") + ";player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlaceFencePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_fence")
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
        var expectedDrawSum = ReadIntParameter(action, "expected_draw_sum_after");
        var expectedHealthMin = ReadDoubleParameter(action, "expected_health_min");
        var expectedHealthMax = ReadDoubleParameter(action, "expected_health_max");
        var expectedMaxHealthMin = ReadDoubleParameter(action, "expected_max_health_min");
        var expectedMaxHealthMax = ReadDoubleParameter(action, "expected_max_health_max");
        var baselineReachable = ReadIntParameter(action, "baseline_reachable_tile_count");
        var reachableAfter = ReadIntParameter(action, "reachable_tile_count_after_placement");
        var protectedCount = ReadIntParameter(action, "protected_access_group_count");
        var routeDistance = ReadIntParameter(action, "route_distance_tiles");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !expectedStack.HasValue || expectedStack.Value < 1 || !expectedDrawSum.HasValue ||
            !expectedHealthMin.HasValue || !expectedHealthMax.HasValue ||
            !expectedMaxHealthMin.HasValue || !expectedMaxHealthMax.HasValue ||
            !baselineReachable.HasValue || !reachableAfter.HasValue || !protectedCount.HasValue ||
            !routeDistance.HasValue || string.IsNullOrWhiteSpace(location) ||
            !TryBoolParameter(action, "expected_is_gate", out var expectedIsGate) ||
            !TryBoolParameter(action, "expected_gate_functional", out var expectedGateFunctional))
        {
            reasons.Add("place_fence_typed_target_fields_required");
            return reasons.ToArray();
        }

        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        var itemId = ReadParameter(action, "item_id");
        if (string.IsNullOrWhiteSpace(qualifiedItemId) || string.IsNullOrWhiteSpace(itemId) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), FenceInventoryRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "target_runtime_type"), FencePlacedRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "fence_data_key"), itemId, StringComparison.Ordinal))
        {
            reasons.Add("place_fence_exact_item_and_runtime_identity_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "fence_layout_reason")))
        {
            reasons.Add("place_fence_layout_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), FenceNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_fence_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_fence_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_fence_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - x.Value) + Math.Abs(standY.Value - y.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("place_fence_adjacent_stand_geometry_invalid");
        }

        var context = ReadStateFieldValue(snapshot, "player", "fence_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_fence_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(
                ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"),
                StringComparison.Ordinal))
        {
            reasons.Add("place_fence_projection_fingerprint_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, qualifiedItemId ?? string.Empty);
        JsonElement? locationRow = null;
        if (!row.HasValue || ReadInt(row.Value, "stack") != expectedStack.Value ||
            !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), FenceInventoryRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "placed_runtime_type"), FencePlacedRuntimeType, StringComparison.Ordinal) ||
            ReadBool(row.Value, "is_gate") != expectedIsGate ||
            !string.Equals(ReadString(row.Value, "fence_data_key"), itemId, StringComparison.Ordinal) ||
            !NearlyEqual(ReadDouble(row.Value, "expected_health_min"), expectedHealthMin.Value) ||
            !NearlyEqual(ReadDouble(row.Value, "expected_health_max"), expectedHealthMax.Value) ||
            !NearlyEqual(ReadDouble(row.Value, "expected_max_health_min"), expectedMaxHealthMin.Value) ||
            !NearlyEqual(ReadDouble(row.Value, "expected_max_health_max"), expectedMaxHealthMax.Value))
        {
            reasons.Add("place_fence_inventory_or_data_identity_drifted");
        }
        else
        {
            locationRow = PlacementLocationRow(row.Value, location!);
            var range = locationRow.HasValue ? PlacementRangeAt(locationRow.Value, x.Value, y.Value) : null;
            if (!locationRow.HasValue ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal) ||
                !range.HasValue)
            {
                reasons.Add("place_fence_exact_tile_not_native_legal");
            }
            else if (ReadInt(range.Value, "expected_draw_sum_after", -1) != expectedDrawSum.Value ||
                ReadBool(range.Value, "expected_gate_functional") != expectedGateFunctional)
            {
                reasons.Add("place_fence_neighbor_topology_drifted");
            }
        }

        if (expectedIsGate && !expectedGateFunctional)
        {
            reasons.Add("place_fence_gate_requires_functional_neighbor_topology");
        }
        if (locationRow.HasValue)
        {
            var layout = new StoragePlacementLayoutProjection()
                .ValidateExactCurrentMapBlockingPlacement(
                    snapshot,
                    locationRow.Value,
                    x.Value,
                    y.Value,
                    standX.Value,
                    standY.Value,
                    "fence_placement");
            if (!string.Equals(layout.Status, "available", StringComparison.Ordinal) ||
                layout.TargetTileX != x.Value || layout.TargetTileY != y.Value ||
                layout.StandTileX != standX.Value || layout.StandTileY != standY.Value ||
                layout.BaselineReachableTileCount != baselineReachable.Value ||
                layout.ReachableTileCountAfterPlacement != reachableAfter.Value ||
                layout.ProtectedAccessGroupCount != protectedCount.Value ||
                layout.RouteDistanceTiles != routeDistance.Value ||
                !string.Equals(layout.ProjectionBasis, ReadParameter(action, "layout_projection_basis"), StringComparison.Ordinal))
            {
                reasons.Add("place_fence_route_safe_layout_drifted");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) <= 0.0001d;
}
