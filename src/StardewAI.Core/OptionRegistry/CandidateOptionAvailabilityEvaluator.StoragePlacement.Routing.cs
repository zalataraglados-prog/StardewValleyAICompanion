using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[]
            BuildRemoteStoragePlacementCandidates(
                SnapshotEnvelope snapshot,
                JsonElement row,
                string currentLocationId,
                EventCandidate[] routeCandidates,
                StrategyCommitmentLedger? commitmentLedger)
        {
            if (!row.TryGetProperty(
                    "locations",
                    out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

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
            var storageRole = StorageRole(row);
            var reservationGuard =
                new InventoryPlacementMaterialReservationGuard()
                    .Evaluate(
                        commitmentLedger,
                        slotIndex,
                        qualifiedItemId);
            return locations.EnumerateArray()
                .Where(location =>
                    location.ValueKind ==
                        JsonValueKind.Object &&
                    !string.Equals(
                        ReadString(
                            location,
                            "location_id"),
                        currentLocationId,
                        StringComparison.OrdinalIgnoreCase))
                .Select(location =>
                    BuildRemoteStoragePlacementCandidate(
                        snapshot,
                        location,
                        currentLocationId,
                        routeCandidates,
                        slotIndex,
                        itemId,
                        qualifiedItemId,
                        stack,
                        nativeBranch,
                        storageRole,
                        reservationGuard))
                .ToArray();
        }

        private EventCandidate
            BuildRemoteStoragePlacementCandidate(
                SnapshotEnvelope snapshot,
                JsonElement location,
                string currentLocationId,
                EventCandidate[] routeCandidates,
                int slotIndex,
                string itemId,
                string qualifiedItemId,
                int stack,
                string nativeBranch,
                string storageRole,
                InventoryPlacementMaterialReservationGuardResult
                    reservationGuard)
        {
            var targetLocationId = ReadString(
                location,
                "location_id");
            var routePlan = FindResolvedRoutePlan(
                snapshot,
                currentLocationId,
                targetLocationId,
                routeCandidates);
            var route = routePlan?.FirstConnectorCandidate;
            var blockReasons = new List<string>();
            if (string.IsNullOrWhiteSpace(
                    reservationGuard.LedgerId))
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
            if (!string.Equals(
                    ReadString(
                        location,
                        "placement_probe_status"),
                    "native_legal_tiles_available",
                    StringComparison.Ordinal) ||
                ReadInt(
                    location,
                    "static_legal_tile_count") < 1)
            {
                blockReasons.Add(
                    "storage_placement_no_native_legal_tile_in_target_location");
            }
            if (!reservationGuard.Ready &&
                reservationGuard.ReservationIds.Length > 0)
            {
                blockReasons.Add(
                    "storage_placement_inventory_item_reserved");
            }
            if (route is null)
            {
                blockReasons.Add(
                    "storage_placement_cross_map_route_unavailable");
            }
            else
            {
                blockReasons.AddRange(route.BlockReasons);
            }

            var continuation = new[]
            {
                Parameter(
                    "continuation.option_id",
                    "executor.place_storage"),
                Parameter(
                    "continuation.storage_location_id",
                    targetLocationId),
                Parameter(
                    "continuation.storage_inventory_slot_index",
                    slotIndex.ToString()),
                Parameter(
                    "continuation.storage_qualified_item_id",
                    qualifiedItemId),
                Parameter(
                    "continuation.storage_item_id",
                    itemId),
                Parameter(
                    "continuation.native_storage_branch",
                    nativeBranch),
                Parameter(
                    "continuation.storage_role",
                    storageRole),
                Parameter(
                    "storage_route.remaining_connector_count",
                    (routePlan?.Path.Length ?? 0).ToString()),
                Parameter(
                    "storage_route.snapshot_policy",
                    "fresh_snapshot_after_each_connector")
            };
            return new EventCandidate
            {
                CandidateId =
                    "storage-place-route:" +
                    targetLocationId +
                    ":slot" + slotIndex + ":" +
                    qualifiedItemId +
                    ":via:" +
                    (route?.TileX?.ToString() ?? "none") +
                    "," +
                    (route?.TileY?.ToString() ?? "none"),
                Kind = "route_connector_tile",
                Available = route is not null &&
                    blockReasons.Count == 0,
                LocationId = currentLocationId,
                TileX = route?.TileX,
                TileY = route?.TileY,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                SlotIndex = slotIndex,
                Quantity = 1,
                EstimatedTicks =
                    route?.EstimatedTicks ?? -1,
                EnergyCost = 0,
                AvailabilityClass =
                    route is not null &&
                    blockReasons.Count == 0
                        ? "transparent_storage_placement_cross_map_route_step"
                        : "transparent_storage_placement_cross_map_route_blocked",
                ExpectedEffect =
                    "storage_placement_route_target_location=" +
                    targetLocationId +
                    ";storage_inventory_slot_index=" +
                    slotIndex +
                    ";storage_qualified_item_id=" +
                    qualifiedItemId +
                    ";storage_role=" + storageRole +
                    ";one_connector_then_fresh_snapshot=true" +
                    ";exact_tile_selected_after_target_map_load=true",
                AllowedNow = route?.AllowedNow,
                AllowedToday = route?.AllowedToday,
                NextOpenTime = route?.NextOpenTime,
                EffectiveOpenTime =
                    route?.EffectiveOpenTime,
                ClosesAt = route?.ClosesAt,
                WaitCost = route?.WaitCost,
                GateReasons =
                    route?.GateReasons ??
                    Array.Empty<string>(),
                BlockReasons = blockReasons
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Parameters = (route?.Parameters ??
                        Array.Empty<
                            SmallModelActionParameter>())
                    .Concat(continuation)
                    .ToArray()
            };
        }
    }
}
