using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompilePlaceMachineStep(
            SmallModelAction action)
        {
            var slotIndex = ReadIntParameter(action, "inventory_slot_index");
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var locationId = ReadParameter(action, "location_id");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            if (!slotIndex.HasValue ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "place_machine",
                    locationId + "(" + targetX.Value + "," +
                    targetY.Value + "):slot" + slotIndex.Value + ":" +
                    qualifiedItemId,
                    "player.inventory[" + slotIndex.Value +
                    "].stack_decreases=1;farm.machines[" + locationId +
                    ":" + targetX.Value + "," + targetY.Value +
                    "].qualified_item_id=" + qualifiedItemId,
                    30)
            };
        }

        private static string[] ValidatePlaceMachinePlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            if (action.OptionId != "executor.place_machine")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var slotIndex = ReadIntParameter(action, "inventory_slot_index");
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var locationId = ReadParameter(action, "location_id");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            var itemId = ReadParameter(action, "item_id");
            var expectedStack = ReadIntParameter(action, "inventory_stack_before");
            if (!slotIndex.HasValue ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                !standX.HasValue ||
                !standY.HasValue ||
                string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !expectedStack.HasValue)
            {
                reasons.Add("place_machine_typed_target_fields_required");
                return reasons.ToArray();
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("place_machine_menu_must_be_clear");
            }
            if (!string.Equals(
                    locationId,
                    ReadStateFieldString(snapshot, "player", "location_id"),
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ReadParameter(action, "target_location"),
                    locationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("place_machine_requires_loaded_target_location");
            }
            if (Math.Abs(standX.Value - targetX.Value) +
                    Math.Abs(standY.Value - targetY.Value) != 1 ||
                MachinePlacementCollisionGridBlocks(
                    snapshot,
                    standX.Value,
                    standY.Value))
            {
                reasons.Add("place_machine_adjacent_stand_geometry_invalid");
            }

            var context = ReadStateFieldValue(
                snapshot,
                "player",
                "machine_placement");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("place_machine_projection_unavailable");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }
            if (!string.Equals(
                    ReadParameter(
                        action,
                        "machine_placement_projection_fingerprint"),
                    ReadString(
                        context.Value,
                        "static_projection_fingerprint"),
                    StringComparison.Ordinal))
            {
                reasons.Add("place_machine_projection_fingerprint_drifted");
            }

            var row = MachinePlacementRow(
                context.Value,
                slotIndex.Value,
                qualifiedItemId);
            if (!row.HasValue ||
                !string.Equals(
                    ReadString(row.Value, "item_id"),
                    itemId,
                    StringComparison.Ordinal) ||
                ReadInt(row.Value, "stack") != expectedStack.Value ||
                expectedStack.Value < 1)
            {
                reasons.Add("place_machine_inventory_identity_drifted");
            }
            else
            {
                var location = MachinePlacementLocation(
                    row.Value,
                    locationId);
                if (!location.HasValue ||
                    !string.Equals(
                        ReadString(
                            location.Value,
                            "placement_probe_status"),
                        "native_legal_tiles_available",
                        StringComparison.Ordinal) ||
                    ReadBool(
                        location.Value,
                        "machine_operational_context_valid") != true ||
                    !MachinePlacementRangeContains(
                        location.Value,
                        targetX.Value,
                        targetY.Value))
                {
                    reasons.Add("place_machine_exact_tile_not_native_legal");
                }
            }

            var reservationGuard =
                new MachinePlacementMaterialReservationGuard().Evaluate(
                    commitmentLedger,
                    slotIndex.Value,
                    qualifiedItemId);
            if (!reservationGuard.Ready)
            {
                reasons.Add("place_machine_material_reservation_guard_blocked");
            }
            if (!string.Equals(
                    ReadParameter(action, "commitment_ledger_id"),
                    reservationGuard.LedgerId,
                    StringComparison.Ordinal) ||
                ReadIntParameter(action, "commitment_ledger_revision") !=
                    reservationGuard.LedgerRevision ||
                !string.Equals(
                    ReadParameter(action, "material_reservation_ledger_id"),
                    reservationGuard.LedgerId,
                    StringComparison.Ordinal) ||
                ReadIntParameter(
                    action,
                    "material_reservation_ledger_revision") !=
                    reservationGuard.LedgerRevision ||
                !string.Equals(
                    ReadParameter(
                        action,
                        "material_reservation_guard_status"),
                    reservationGuard.Status,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadParameter(
                        action,
                        "material_reservation_ids_json"),
                    JsonSerializer.Serialize(
                        reservationGuard.ReservationIds),
                    StringComparison.Ordinal))
            {
                reasons.Add("place_machine_material_reservation_projection_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? MachinePlacementRow(
            JsonElement context,
            int slotIndex,
            string qualifiedItemId)
        {
            if (!context.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "inventory_slot_index", -1) == slotIndex &&
                    string.Equals(
                        ReadString(row, "qualified_item_id"),
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
            return null;
        }

        private static JsonElement? MachinePlacementLocation(
            JsonElement row,
            string locationId)
        {
            if (!row.TryGetProperty("locations", out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var location in locations.EnumerateArray())
            {
                if (location.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        ReadString(location, "location_id"),
                        locationId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return location;
                }
            }
            return null;
        }

        private static bool MachinePlacementRangeContains(
            JsonElement location,
            int x,
            int y)
        {
            if (!location.TryGetProperty(
                    "static_legal_tile_ranges",
                    out var ranges) ||
                ranges.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            foreach (var range in ranges.EnumerateArray())
            {
                if (range.ValueKind == JsonValueKind.Object &&
                    ReadInt(range, "y") == y &&
                    x >= ReadInt(range, "start_x") &&
                    x <= ReadInt(range, "end_x", -1))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MachinePlacementCollisionGridBlocks(
            SnapshotEnvelope snapshot,
            int x,
            int y)
        {
            var grid = ReadStateFieldValue(
                snapshot,
                "locations",
                "collision_grid");
            if (!grid.HasValue ||
                grid.Value.ValueKind != JsonValueKind.Object)
            {
                return true;
            }
            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 ||
                height <= 0 ||
                x < 0 ||
                y < 0 ||
                x >= width ||
                y >= height)
            {
                return true;
            }
            return grid.Value.TryGetProperty(
                    "notable_tiles",
                    out var tiles) &&
                tiles.ValueKind == JsonValueKind.Array &&
                tiles.EnumerateArray().Any(tile =>
                    ReadInt(tile, "tile_x") == x &&
                    ReadInt(tile, "tile_y") == y &&
                    ReadBool(tile, "collision_blocked"));
        }
    }
}
