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
    private const string TentKitQualifiedItemId = "(O)TentKit";
    private const string TentRuntimeType = "StardewValley.TerrainFeatures.Tent";
    private const string TentNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)TentKit)->largeTerrainFeatures.Add(Tent(rectangle.X+1,rectangle.Y+1))";

    private static CompiledActionStep[] CompilePlaceTentStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var anchorX = ReadIntParameter(action, "anchor_tile_x");
        var anchorY = ReadIntParameter(action, "anchor_tile_y");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !targetX.HasValue || !targetY.HasValue || !anchorX.HasValue || !anchorY.HasValue ||
            string.IsNullOrWhiteSpace(location))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "place_tent",
                location + ":probe(" + targetX.Value + "," + targetY.Value + "):anchor(" + anchorX.Value + "," + anchorY.Value + "):slot" + slot.Value,
                "current_location.large_terrain_features[" + anchorX.Value + "," + anchorY.Value + "].runtime_type=" +
                    TentRuntimeType + ";player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlaceTentPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_tent")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var stack = ReadIntParameter(action, "inventory_stack_before");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var direction = ReadIntParameter(action, "direction");
        var rectangleX = ReadIntParameter(action, "rectangle_x");
        var rectangleY = ReadIntParameter(action, "rectangle_y");
        var rectangleWidth = ReadIntParameter(action, "rectangle_width");
        var rectangleHeight = ReadIntParameter(action, "rectangle_height");
        var anchorX = ReadIntParameter(action, "anchor_tile_x");
        var anchorY = ReadIntParameter(action, "anchor_tile_y");
        var baselineReachable = ReadIntParameter(action, "baseline_reachable_tile_count");
        var reachableAfter = ReadIntParameter(action, "reachable_tile_count_after_placement");
        var protectedCount = ReadIntParameter(action, "protected_access_group_count");
        var routeDistance = ReadIntParameter(action, "route_distance_tiles");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !stack.HasValue || stack.Value < 1 || !targetX.HasValue || !targetY.HasValue ||
            !standX.HasValue || !standY.HasValue || !direction.HasValue || !rectangleX.HasValue || !rectangleY.HasValue ||
            !rectangleWidth.HasValue || !rectangleHeight.HasValue || !anchorX.HasValue || !anchorY.HasValue ||
            !baselineReachable.HasValue || !reachableAfter.HasValue || !protectedCount.HasValue || !routeDistance.HasValue ||
            string.IsNullOrWhiteSpace(location))
        {
            reasons.Add("place_tent_typed_target_fields_required");
            return reasons.ToArray();
        }

        if (!TentPlacementGeometryResolver.TryResolve(standX.Value, standY.Value, targetX.Value, targetY.Value, out var geometry) ||
            geometry.Direction != direction.Value || geometry.RectangleX != rectangleX.Value || geometry.RectangleY != rectangleY.Value ||
            geometry.RectangleWidth != rectangleWidth.Value || geometry.RectangleHeight != rectangleHeight.Value ||
            geometry.AnchorTileX != anchorX.Value || geometry.AnchorTileY != anchorY.Value)
        {
            reasons.Add("place_tent_directional_geometry_mismatch");
        }
        if (!string.Equals(ReadParameter(action, "qualified_item_id"), TentKitQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "placed_runtime_type"), TentRuntimeType, StringComparison.Ordinal))
        {
            reasons.Add("place_tent_exact_runtime_identity_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "tent_placement_reason")))
        {
            reasons.Add("place_tent_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), TentNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_tent_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_tent_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_tent_requires_loaded_target_location");
        }

        var context = ReadStateFieldValue(snapshot, "player", "tent_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_tent_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("place_tent_projection_fingerprint_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, TentKitQualifiedItemId);
        JsonElement? locationRow = null;
        if (!row.HasValue || ReadInt(row.Value, "stack") != stack.Value ||
            !ReadBool(row.Value, "exact_base_object") ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "placed_runtime_type"), TentRuntimeType, StringComparison.Ordinal))
        {
            reasons.Add("place_tent_inventory_identity_drifted");
        }
        else
        {
            locationRow = PlacementLocationRow(row.Value, location!);
            var directionRow = locationRow.HasValue
                ? TentDirectionRow(locationRow.Value, direction.Value)
                : null;
            if (!locationRow.HasValue || !ReadBool(locationRow.Value, "location_is_outdoors") ||
                ReadBool(locationRow.Value, "tomorrow_festival_blocked") ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_directional_stands_available", StringComparison.Ordinal) ||
                !directionRow.HasValue || !TentStandRangeContains(directionRow.Value, standX.Value, standY.Value))
            {
                reasons.Add("place_tent_exact_directional_stand_not_native_legal");
            }
            if (!locationRow.HasValue ||
                !string.Equals(ReadParameter(action, "tomorrow_season"), ReadString(locationRow.Value, "tomorrow_season"), StringComparison.Ordinal) ||
                ReadIntParameter(action, "tomorrow_day") != ReadInt(locationRow.Value, "tomorrow_day") ||
                !string.Equals(ReadParameter(action, "tomorrow_festival_id") ?? string.Empty,
                    ReadString(locationRow.Value, "tomorrow_festival_id"), StringComparison.Ordinal))
            {
                reasons.Add("place_tent_tomorrow_calendar_drifted");
            }
        }

        if (locationRow.HasValue)
        {
            var layout = new StoragePlacementLayoutProjection().ValidateExactCurrentMapPassableRectanglePlacement(
                snapshot,
                locationRow.Value,
                targetX.Value,
                targetY.Value,
                standX.Value,
                standY.Value,
                rectangleX.Value,
                rectangleY.Value,
                rectangleWidth.Value,
                rectangleHeight.Value,
                "tent_placement");
            if (!string.Equals(layout.Status, "available", StringComparison.Ordinal) ||
                layout.TargetTileX != targetX.Value || layout.TargetTileY != targetY.Value ||
                layout.StandTileX != standX.Value || layout.StandTileY != standY.Value ||
                layout.BaselineReachableTileCount != baselineReachable.Value ||
                layout.ReachableTileCountAfterPlacement != reachableAfter.Value ||
                layout.ProtectedAccessGroupCount != protectedCount.Value ||
                layout.RouteDistanceTiles != routeDistance.Value ||
                !string.Equals(layout.ProjectionBasis, ReadParameter(action, "layout_projection_basis"), StringComparison.Ordinal))
            {
                reasons.Add("place_tent_passable_rectangle_layout_drifted");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? TentDirectionRow(JsonElement location, int direction)
    {
        if (!location.TryGetProperty("direction_rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "direction", -1) == direction)
            {
                return row;
            }
        }
        return null;
    }

    private static bool TentStandRangeContains(JsonElement directionRow, int x, int y)
    {
        if (!directionRow.TryGetProperty("static_legal_stand_tile_ranges", out var ranges) || ranges.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        return ranges.EnumerateArray().Any(range =>
            range.ValueKind == JsonValueKind.Object && ReadInt(range, "y", -1) == y &&
            x >= ReadInt(range, "start_x", int.MaxValue) && x <= ReadInt(range, "end_x", int.MinValue));
    }
}
