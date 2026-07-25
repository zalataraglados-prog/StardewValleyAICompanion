using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static JsonElement? StoragePlacementRow(
            JsonElement context,
            int slotIndex,
            string qualifiedItemId)
        {
            if (!context.TryGetProperty(
                    "rows",
                    out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    ReadInt(
                        row,
                        "inventory_slot_index",
                        -1) == slotIndex &&
                    string.Equals(
                        ReadString(
                            row,
                            "qualified_item_id"),
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
            return null;
        }

        private static JsonElement?
            StoragePlacementLocation(
                JsonElement row,
                string locationId)
        {
            if (!row.TryGetProperty(
                    "locations",
                    out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var location in locations.EnumerateArray())
            {
                if (location.ValueKind ==
                        JsonValueKind.Object &&
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

        private static bool
            StoragePlacementIdentityMatches(
                SmallModelAction action,
                JsonElement row,
                string? itemId,
                int expectedStack)
        {
            return expectedStack >= 1 &&
                ReadInt(row, "stack") == expectedStack &&
                string.Equals(
                    ReadString(row, "item_id"),
                    itemId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(
                        row,
                        "native_storage_branch"),
                    ReadParameter(
                        action,
                        "native_storage_branch"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(
                        row,
                        "placed_runtime_type"),
                    ReadParameter(
                        action,
                        "placed_runtime_type"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(
                        row,
                        "special_chest_type"),
                    ReadParameter(
                        action,
                        "special_chest_type"),
                    StringComparison.Ordinal) &&
                ReadInt(row, "actual_capacity") ==
                    ReadIntParameter(
                        action,
                        "actual_capacity") &&
                string.Equals(
                    ReadStorageRole(row),
                    ReadParameter(action, "storage_role"),
                    StringComparison.Ordinal);
        }

        private static bool StoragePlacementLayoutMatches(
            SmallModelAction action,
            StoragePlacementLayoutResult layout,
            int targetX,
            int targetY,
            int standX,
            int standY)
        {
            return string.Equals(
                    layout.Status,
                    "available",
                    StringComparison.Ordinal) &&
                layout.TargetTileX == targetX &&
                layout.TargetTileY == targetY &&
                layout.StandTileX == standX &&
                layout.StandTileY == standY &&
                layout.RouteDistanceTiles ==
                    ReadIntParameter(
                        action,
                        "route_distance_tiles") &&
                string.Equals(
                    layout.ProjectionBasis,
                    ReadParameter(
                        action,
                        "layout_projection_basis"),
                    StringComparison.Ordinal);
        }

        private static bool
            StoragePlacementReservationMatches(
                SmallModelAction action,
                InventoryPlacementMaterialReservationGuardResult
                    reservationGuard)
        {
            return string.Equals(
                    ReadParameter(
                        action,
                        "commitment_ledger_id"),
                    reservationGuard.LedgerId,
                    StringComparison.Ordinal) &&
                ReadIntParameter(
                    action,
                    "commitment_ledger_revision") ==
                    reservationGuard.LedgerRevision &&
                string.Equals(
                    ReadParameter(
                        action,
                        "material_reservation_ledger_id"),
                    reservationGuard.LedgerId,
                    StringComparison.Ordinal) &&
                ReadIntParameter(
                    action,
                    "material_reservation_ledger_revision") ==
                    reservationGuard.LedgerRevision &&
                string.Equals(
                    ReadParameter(
                        action,
                        "material_reservation_guard_status"),
                    reservationGuard.Status,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(
                        action,
                        "material_reservation_ids_json"),
                    JsonSerializer.Serialize(
                        reservationGuard.ReservationIds),
                    StringComparison.Ordinal);
        }

        private static string ReadStorageRole(
            JsonElement row)
        {
            if (ReadBool(row, "shipping_storage"))
            {
                return "shipping";
            }
            if (ReadBool(row, "fridge_storage"))
            {
                return "fridge";
            }
            if (ReadBool(row, "shared_global_storage"))
            {
                return "shared_global";
            }
            if (ReadBool(row, "ordinary_material_storage"))
            {
                return "ordinary_material";
            }
            return "special_storage";
        }
    }
}
