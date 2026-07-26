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
        private EventCandidate[] BuildRemoteMachinePlacementCandidates(
            SnapshotEnvelope snapshot,
            JsonElement row,
            string currentLocationId,
            EventCandidate[] routeCandidates,
            StrategyCommitmentLedger? commitmentLedger,
            MachineRelocationIntent? relocationIntent)
        {
            if (!row.TryGetProperty("locations", out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var slotIndex = ReadInt(row, "inventory_slot_index", -1);
            var itemId = ReadString(row, "item_id");
            var qualifiedItemId = ReadString(row, "qualified_item_id");
            var stack = Math.Max(0, ReadInt(row, "stack"));
            var reservationGuard =
                new InventoryPlacementMaterialReservationGuard().Evaluate(
                    commitmentLedger,
                    slotIndex,
                    qualifiedItemId);
            return locations.EnumerateArray()
                .Where(location =>
                    location.ValueKind == JsonValueKind.Object &&
                    !string.Equals(
                        ReadString(location, "location_id"),
                        currentLocationId,
                        StringComparison.OrdinalIgnoreCase) &&
                    (relocationIntent is null ||
                     string.Equals(
                         ReadString(location, "location_id"),
                         relocationIntent.TargetLocationId,
                         StringComparison.OrdinalIgnoreCase)))
                .Select(location =>
                    BuildRemoteMachinePlacementCandidate(
                        snapshot,
                        location,
                        currentLocationId,
                        routeCandidates,
                        slotIndex,
                        itemId,
                        qualifiedItemId,
                        stack,
                        reservationGuard,
                        relocationIntent))
                .ToArray();
        }

        private EventCandidate BuildRemoteMachinePlacementCandidate(
            SnapshotEnvelope snapshot,
            JsonElement location,
            string currentLocationId,
            EventCandidate[] routeCandidates,
            int slotIndex,
            string itemId,
            string qualifiedItemId,
            int stack,
            InventoryPlacementMaterialReservationGuardResult reservationGuard,
            MachineRelocationIntent? relocationIntent)
        {
            var targetLocationId = ReadString(location, "location_id");
            var routePlan = relocationIntent is null
                ? FindResolvedRoutePlan(
                    snapshot,
                    currentLocationId,
                    targetLocationId,
                    routeCandidates)
                : FindCommittedRelocationRoutePlan(
                    snapshot,
                    currentLocationId,
                    targetLocationId,
                    routeCandidates,
                    relocationIntent);
            var route = routePlan?.FirstConnectorCandidate;
            var blockReasons = new List<string>();
            if (string.IsNullOrWhiteSpace(reservationGuard.LedgerId))
            {
                blockReasons.Add(
                    "machine_placement_strategy_ledger_unavailable");
            }
            if (slotIndex < 0 ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                stack < 1)
            {
                blockReasons.Add(
                    "machine_placement_inventory_identity_unavailable");
            }
            if (!string.Equals(
                    ReadString(location, "placement_probe_status"),
                    "native_legal_tiles_available",
                    StringComparison.Ordinal) ||
                ReadInt(location, "static_legal_tile_count") < 1)
            {
                blockReasons.Add(
                    "machine_placement_no_native_legal_tile_in_target_location");
            }
            if (ReadBool(
                    location,
                    "machine_operational_context_valid") != true)
            {
                blockReasons.Add(
                    "machine_placement_operational_context_invalid");
            }
            if (relocationIntent is not null &&
                (!string.Equals(
                    relocationIntent.TargetLocationId,
                    targetLocationId,
                    StringComparison.OrdinalIgnoreCase) ||
                 !MachinePlacementRangeContains(
                     location,
                     relocationIntent.TargetTileX,
                     relocationIntent.TargetTileY)))
            {
                blockReasons.Add(
                    "machine_relocation_remote_exact_target_unavailable");
            }
            if (!reservationGuard.Ready &&
                reservationGuard.ReservationIds.Length > 0)
            {
                blockReasons.Add(
                    "machine_placement_inventory_item_reserved");
            }
            if (route is null)
            {
                blockReasons.Add(
                    relocationIntent is null
                        ? "machine_placement_cross_map_route_unavailable"
                        : "machine_relocation_committed_route_drifted");
            }
            else
            {
                blockReasons.AddRange(route.BlockReasons);
            }

            var continuation = new[]
            {
                Parameter(
                    "continuation.option_id",
                    "executor.place_machine"),
                Parameter(
                    "continuation.machine_location_id",
                    targetLocationId),
                Parameter(
                    "continuation.machine_inventory_slot_index",
                    slotIndex.ToString()),
                Parameter(
                    "continuation.machine_qualified_item_id",
                    qualifiedItemId),
                Parameter(
                    "continuation.machine_item_id",
                    itemId),
                Parameter(
                    "continuation.relocation_intent_id",
                    relocationIntent?.IntentId ?? string.Empty),
                Parameter(
                    "machine_route.remaining_connector_count",
                    (routePlan?.Path.Length ?? 0).ToString()),
                Parameter(
                    "machine_route.committed_segment_index",
                    relocationIntent is null || routePlan is null
                        ? string.Empty
                        : (relocationIntent.RouteConnectorCount -
                           routePlan.Path.Length).ToString()),
                Parameter(
                    "machine_route.snapshot_policy",
                    "fresh_snapshot_after_each_connector")
            };
            return new EventCandidate
            {
                CandidateId =
                    "machine-place-route:" + targetLocationId +
                    ":slot" + slotIndex + ":" + qualifiedItemId +
                    (relocationIntent is null
                        ? string.Empty
                        : ":intent=" + relocationIntent.IntentId) +
                    ":via:" + (route?.TileX?.ToString() ?? "none") +
                    "," + (route?.TileY?.ToString() ?? "none"),
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
                EstimatedTicks = route?.EstimatedTicks ?? -1,
                EnergyCost = 0,
                AvailabilityClass = route is not null &&
                    blockReasons.Count == 0
                        ? "transparent_machine_placement_cross_map_route_step"
                        : "transparent_machine_placement_cross_map_route_blocked",
                ExpectedEffect =
                    "machine_placement_route_target_location=" +
                    targetLocationId +
                    ";machine_inventory_slot_index=" + slotIndex +
                    ";machine_qualified_item_id=" + qualifiedItemId +
                    ";one_connector_then_fresh_snapshot=true" +
                    ";exact_tile_selected_after_target_map_load=true",
                AllowedNow = route?.AllowedNow,
                AllowedToday = route?.AllowedToday,
                NextOpenTime = route?.NextOpenTime,
                EffectiveOpenTime = route?.EffectiveOpenTime,
                ClosesAt = route?.ClosesAt,
                WaitCost = route?.WaitCost,
                GateReasons =
                    route?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = blockReasons
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Parameters = (route?.Parameters ??
                        Array.Empty<SmallModelActionParameter>())
                    .Concat(continuation)
                    .ToArray()
            };
        }
    }
}
