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
    private const string GrassInventoryRuntimeType = "StardewValley.Object";
    private const string GrassPlacedRuntimeType = "StardewValley.TerrainFeatures.Grass";
    private const string GrassNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)297|(O)BlueGrassStarter)->terrainFeatures.Add(Grass(type,4))";

    private static CompiledActionStep[] CompilePlantGrassStep(SmallModelAction action)
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
                "plant_grass",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + qualifiedItemId,
                "current_location.terrain_features[" + x.Value + "," + y.Value + "].runtime_type=" + GrassPlacedRuntimeType +
                    ";grass_type=" + ReadParameter(action, "expected_grass_type") +
                    ";number_of_weeds=" + ReadParameter(action, "expected_initial_number_of_weeds") +
                    ";player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlantGrassPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.plant_grass")
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
        var expectedGrassType = ReadIntParameter(action, "expected_grass_type");
        var expectedWeeds = ReadIntParameter(action, "expected_initial_number_of_weeds");
        var baselineReachable = ReadIntParameter(action, "baseline_reachable_tile_count");
        var reachableAfter = ReadIntParameter(action, "reachable_tile_count_after_placement");
        var protectedCount = ReadIntParameter(action, "protected_access_group_count");
        var routeDistance = ReadIntParameter(action, "route_distance_tiles");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !expectedStack.HasValue || expectedStack.Value < 1 || !expectedGrassType.HasValue ||
            expectedGrassType.Value is not (1 or 7) || expectedWeeds != 4 ||
            !baselineReachable.HasValue || !reachableAfter.HasValue || !protectedCount.HasValue ||
            !routeDistance.HasValue || string.IsNullOrWhiteSpace(location) ||
            !TryBoolParameter(action, "expected_passable", out var expectedPassable))
        {
            reasons.Add("plant_grass_typed_target_fields_required");
            return reasons.ToArray();
        }

        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        var itemId = ReadParameter(action, "item_id");
        var expectedVariantType = string.Equals(qualifiedItemId, "(O)297", StringComparison.Ordinal) ? 1 :
            string.Equals(qualifiedItemId, "(O)BlueGrassStarter", StringComparison.Ordinal) ? 7 : -1;
        if (string.IsNullOrWhiteSpace(itemId) ||
            !string.Equals(qualifiedItemId, "(O)" + itemId, StringComparison.Ordinal) ||
            expectedVariantType != expectedGrassType.Value ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), GrassInventoryRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "target_runtime_type"), GrassPlacedRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "placement_sound"), "dirtyHit", StringComparison.Ordinal) ||
            !expectedPassable)
        {
            reasons.Add("plant_grass_inventory_or_variant_identity_drifted");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "grass_layout_reason")))
        {
            reasons.Add("plant_grass_layout_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), GrassNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("plant_grass_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("plant_grass_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("plant_grass_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - x.Value) + Math.Abs(standY.Value - y.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("plant_grass_adjacent_stand_geometry_invalid");
        }

        var context = ReadStateFieldValue(snapshot, "player", "grass_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("plant_grass_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("plant_grass_projection_fingerprint_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, qualifiedItemId ?? string.Empty);
        JsonElement? locationRow = null;
        if (!row.HasValue || ReadInt(row.Value, "stack") != expectedStack.Value ||
            !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), GrassInventoryRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "placed_runtime_type"), GrassPlacedRuntimeType, StringComparison.Ordinal) ||
            ReadInt(row.Value, "expected_grass_type", -1) != expectedGrassType.Value ||
            ReadInt(row.Value, "expected_initial_number_of_weeds", -1) != expectedWeeds.Value ||
            !string.Equals(ReadString(row.Value, "placement_sound"), "dirtyHit", StringComparison.Ordinal) ||
            !ReadBool(row.Value, "expected_passable"))
        {
            reasons.Add("plant_grass_inventory_or_variant_identity_drifted");
        }
        else
        {
            locationRow = PlacementLocationRow(row.Value, location!);
            var range = locationRow.HasValue ? PlacementRangeAt(locationRow.Value, x.Value, y.Value) : null;
            if (!locationRow.HasValue ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal) ||
                !range.HasValue)
            {
                reasons.Add("plant_grass_exact_tile_not_native_legal");
            }
        }

        if (locationRow.HasValue)
        {
            var layout = new StoragePlacementLayoutProjection().ValidateExactCurrentMapPassablePlacement(
                snapshot, locationRow.Value, x.Value, y.Value, standX.Value, standY.Value, "grass_placement");
            if (!string.Equals(layout.Status, "available", StringComparison.Ordinal) ||
                layout.TargetTileX != x.Value || layout.TargetTileY != y.Value ||
                layout.StandTileX != standX.Value || layout.StandTileY != standY.Value ||
                layout.BaselineReachableTileCount != baselineReachable.Value ||
                layout.ReachableTileCountAfterPlacement != reachableAfter.Value ||
                layout.ProtectedAccessGroupCount != protectedCount.Value ||
                layout.RouteDistanceTiles != routeDistance.Value ||
                !string.Equals(layout.ProjectionBasis, ReadParameter(action, "layout_projection_basis"), StringComparison.Ordinal))
            {
                reasons.Add("plant_grass_passable_layout_drifted");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
