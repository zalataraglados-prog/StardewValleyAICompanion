using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate BuildStoragePlacementCandidate(
            SnapshotEnvelope snapshot,
            JsonElement row,
            string currentLocationId,
            string projectionFingerprint,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var slotIndex = ReadInt(
                row,
                "inventory_slot_index",
                -1);
            var itemId = ReadString(row, "item_id");
            var qualifiedItemId = ReadString(
                row,
                "qualified_item_id");
            var stack = Math.Max(0, ReadInt(row, "stack"));
            var nativeBranch = ReadString(
                row,
                "native_storage_branch");
            var location = CurrentStoragePlacementLocation(
                row,
                currentLocationId);
            var layout = location.HasValue
                ? new StoragePlacementLayoutProjection()
                    .SelectCurrentMapTile(
                        snapshot,
                        location.Value)
                : StoragePlacementLayoutResult.Blocked(
                    new[]
                    {
                        "storage_placement_current_location_projection_unavailable"
                    });
            var reservationGuard =
                new InventoryPlacementMaterialReservationGuard()
                    .Evaluate(
                        commitmentLedger,
                        slotIndex,
                        qualifiedItemId);
            var blockReasons = new List<string>();
            if (commitmentLedger is null ||
                string.IsNullOrWhiteSpace(
                    commitmentLedger.LedgerId))
            {
                blockReasons.Add(
                    "storage_placement_strategy_ledger_unavailable");
            }
            if (slotIndex < 0 ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                string.IsNullOrWhiteSpace(nativeBranch) ||
                stack < 1)
            {
                blockReasons.Add(
                    "storage_placement_inventory_identity_unavailable");
            }
            if (!location.HasValue)
            {
                blockReasons.Add(
                    "storage_placement_current_location_projection_unavailable");
            }
            if (!string.Equals(
                    layout.Status,
                    "available",
                    StringComparison.Ordinal))
            {
                blockReasons.AddRange(layout.BlockingReasons);
            }
            if (!reservationGuard.Ready &&
                reservationGuard.ReservationIds.Length > 0)
            {
                blockReasons.Add(
                    "storage_placement_inventory_item_reserved");
            }

            var targetX = layout.TargetTileX;
            var targetY = layout.TargetTileY;
            var standX = layout.StandTileX;
            var standY = layout.StandTileY;
            var storageRole = StorageRole(row);
            return new EventCandidate
            {
                CandidateId =
                    "storage-place:" + currentLocationId +
                    ":slot" + slotIndex + ":" +
                    qualifiedItemId +
                    (targetX.HasValue && targetY.HasValue
                        ? ":" + targetX.Value + "," +
                          targetY.Value
                        : string.Empty),
                Kind = "place_storage_item",
                Available = blockReasons.Count == 0,
                LocationId = currentLocationId,
                TileX = targetX,
                TileY = targetY,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                SlotIndex = slotIndex,
                Quantity = 1,
                EstimatedTicks = layout.RouteDistanceTiles < 0
                    ? 30
                    : Math.Max(
                        90,
                        layout.RouteDistanceTiles * 60 + 30),
                EnergyCost = 0,
                AvailabilityClass =
                    "transparent_storage_native_route_safe_placement",
                ExpectedEffect =
                    "player.inventory[" + slotIndex +
                    "].stack_decreases=1" +
                    ";current_location.chests[" +
                    currentLocationId + ":" +
                    (targetX?.ToString() ?? "?") + "," +
                    (targetY?.ToString() ?? "?") +
                    "].qualified_item_id=" + qualifiedItemId +
                    ";storage_role=" + storageRole +
                    (standX.HasValue && standY.HasValue
                        ? ";move_to_adjacent=" +
                          standX.Value + "," + standY.Value
                        : string.Empty) +
                    ";native_contract=Utility.tryToPlaceItem",
                BlockReasons = blockReasons
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Parameters = new[]
                {
                    Parameter(
                        "inventory_slot_index",
                        slotIndex.ToString()),
                    Parameter(
                        "qualified_item_id",
                        qualifiedItemId),
                    Parameter("item_id", itemId),
                    Parameter(
                        "inventory_stack_before",
                        stack.ToString()),
                    Parameter("location_id", currentLocationId),
                    Parameter(
                        "target_tile_x",
                        targetX?.ToString() ?? string.Empty),
                    Parameter(
                        "target_tile_y",
                        targetY?.ToString() ?? string.Empty),
                    Parameter(
                        "stand_tile_x",
                        standX?.ToString() ?? string.Empty),
                    Parameter(
                        "stand_tile_y",
                        standY?.ToString() ?? string.Empty),
                    Parameter(
                        "storage_placement_projection_fingerprint",
                        projectionFingerprint),
                    Parameter(
                        "native_storage_branch",
                        nativeBranch),
                    Parameter(
                        "placed_runtime_type",
                        ReadString(row, "placed_runtime_type")),
                    Parameter(
                        "special_chest_type",
                        ReadString(row, "special_chest_type")),
                    Parameter(
                        "actual_capacity",
                        ReadInt(row, "actual_capacity").ToString()),
                    Parameter("storage_role", storageRole),
                    Parameter(
                        "layout_projection_basis",
                        layout.ProjectionBasis),
                    Parameter(
                        "route_distance_tiles",
                        layout.RouteDistanceTiles.ToString()),
                    Parameter(
                        "placement_probe_status",
                        location.HasValue
                            ? ReadString(
                                location.Value,
                                "placement_probe_status")
                            : string.Empty),
                    Parameter(
                        "native_contract",
                        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Chest.placementAction->Farmer.reduceActiveItemByOne"),
                    Parameter(
                        "commitment_ledger_id",
                        reservationGuard.LedgerId),
                    Parameter(
                        "commitment_ledger_revision",
                        reservationGuard.LedgerRevision.ToString()),
                    Parameter(
                        "material_reservation_guard_status",
                        reservationGuard.Status),
                    Parameter(
                        "material_reservation_ledger_id",
                        reservationGuard.LedgerId),
                    Parameter(
                        "material_reservation_ledger_revision",
                        reservationGuard.LedgerRevision.ToString()),
                    Parameter(
                        "material_reservation_ids_json",
                        JsonSerializer.Serialize(
                            reservationGuard.ReservationIds))
                }
            };
        }

    }
}
