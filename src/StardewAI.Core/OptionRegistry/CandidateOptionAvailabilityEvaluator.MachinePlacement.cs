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
        private EventCandidate[] MachinePlacementCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var context = ReadStateFieldValue(snapshot, "player", "machine_placement");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentLocationId = ReadStateFieldString(snapshot, "player", "location_id");
            var projectionFingerprint = ReadString(context.Value, "static_projection_fingerprint");
            var routeCandidates = RouteConnectorCandidates(
                snapshot,
                int.MaxValue);
            return rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .SelectMany(row => new[]
                    {
                        BuildMachinePlacementCandidate(
                            snapshot,
                            row,
                            currentLocationId,
                            projectionFingerprint,
                            commitmentLedger)
                    }
                    .Concat(BuildRemoteMachinePlacementCandidates(
                        snapshot,
                        row,
                        currentLocationId,
                        routeCandidates,
                        commitmentLedger)))
                .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private EventCandidate BuildMachinePlacementCandidate(
            SnapshotEnvelope snapshot,
            JsonElement row,
            string currentLocationId,
            string projectionFingerprint,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var slotIndex = ReadInt(row, "inventory_slot_index", -1);
            var itemId = ReadString(row, "item_id");
            var qualifiedItemId = ReadString(row, "qualified_item_id");
            var stack = Math.Max(0, ReadInt(row, "stack"));
            var location = CurrentMachinePlacementLocation(row, currentLocationId);
            var selection = location.HasValue
                ? SelectMachinePlacementTile(snapshot, location.Value, currentLocationId)
                : null;
            var reservationGuard =
                new MachinePlacementMaterialReservationGuard().Evaluate(
                    commitmentLedger,
                    slotIndex,
                    qualifiedItemId);
            var blockReasons = new List<string>();
            if (commitmentLedger is null ||
                string.IsNullOrWhiteSpace(commitmentLedger.LedgerId))
            {
                blockReasons.Add("machine_placement_strategy_ledger_unavailable");
            }
            if (slotIndex < 0 ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                stack < 1)
            {
                blockReasons.Add("machine_placement_inventory_identity_unavailable");
            }
            if (!location.HasValue)
            {
                blockReasons.Add("machine_placement_current_location_projection_unavailable");
            }
            else
            {
                if (!string.Equals(
                        ReadString(location.Value, "placement_probe_status"),
                        "native_legal_tiles_available",
                        StringComparison.Ordinal))
                {
                    blockReasons.Add("machine_placement_no_native_legal_tile_in_current_location");
                }
                if (ReadBool(location.Value, "machine_operational_context_valid") != true)
                {
                    blockReasons.Add("machine_placement_operational_context_invalid");
                }
            }
            if (selection is null)
            {
                blockReasons.Add("machine_placement_reachable_exact_tile_unavailable");
            }
            if (!reservationGuard.Ready &&
                reservationGuard.ReservationIds.Length > 0)
            {
                blockReasons.Add("machine_placement_inventory_item_reserved");
            }

            var targetX = selection?.Target.X;
            var targetY = selection?.Target.Y;
            var standX = selection?.Stand.X;
            var standY = selection?.Stand.Y;
            return new EventCandidate
            {
                CandidateId = "machine-place:" + currentLocationId + ":slot" + slotIndex +
                    ":" + qualifiedItemId +
                    (targetX.HasValue && targetY.HasValue
                        ? ":" + targetX.Value + "," + targetY.Value
                        : string.Empty),
                Kind = "place_machine_item",
                Available = blockReasons.Count == 0,
                LocationId = currentLocationId,
                TileX = targetX,
                TileY = targetY,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                SlotIndex = slotIndex,
                Quantity = 1,
                EstimatedTicks = selection is null
                    ? 30
                    : Math.Max(
                        90,
                        (Math.Abs(
                            ReadStateFieldInt(snapshot, "player", "tile_x") -
                            selection.Stand.X) +
                         Math.Abs(
                            ReadStateFieldInt(snapshot, "player", "tile_y") -
                            selection.Stand.Y)) * 60 + 30),
                EnergyCost = 0,
                AvailabilityClass = "transparent_machine_native_exact_placement",
                ExpectedEffect = "player.inventory[" + slotIndex + "].stack_decreases=1" +
                    ";farm.machines[" + currentLocationId + ":" +
                    (targetX?.ToString() ?? "?") + "," +
                    (targetY?.ToString() ?? "?") + "].qualified_item_id=" +
                    qualifiedItemId +
                    (standX.HasValue && standY.HasValue
                        ? ";move_to_adjacent=" + standX.Value + "," + standY.Value
                        : string.Empty) +
                    ";native_contract=Utility.tryToPlaceItem",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("inventory_slot_index", slotIndex.ToString()),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("item_id", itemId),
                    Parameter("inventory_stack_before", stack.ToString()),
                    Parameter("location_id", currentLocationId),
                    Parameter("target_tile_x", targetX?.ToString() ?? string.Empty),
                    Parameter("target_tile_y", targetY?.ToString() ?? string.Empty),
                    Parameter("stand_tile_x", standX?.ToString() ?? string.Empty),
                    Parameter("stand_tile_y", standY?.ToString() ?? string.Empty),
                    Parameter("machine_placement_projection_fingerprint", projectionFingerprint),
                    Parameter(
                        "placement_probe_status",
                        location.HasValue
                            ? ReadString(location.Value, "placement_probe_status")
                            : string.Empty),
                    Parameter("native_contract", "Utility.playerCanPlaceItemHere->Object.placementAction->Farmer.reduceActiveItemByOne"),
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

        private static JsonElement? CurrentMachinePlacementLocation(
            JsonElement row,
            string currentLocationId)
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
                        currentLocationId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return location;
                }
            }
            return null;
        }

        private MachinePlacementTileSelection? SelectMachinePlacementTile(
            SnapshotEnvelope snapshot,
            JsonElement location,
            string locationId)
        {
            if (!location.TryGetProperty(
                    "static_legal_tile_ranges",
                    out var ranges) ||
                ranges.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var legalTiles = ranges.EnumerateArray()
                .Where(range => range.ValueKind == JsonValueKind.Object)
                .SelectMany(range =>
                {
                    var y = ReadInt(range, "y");
                    var startX = ReadInt(range, "start_x");
                    var endX = ReadInt(range, "end_x", startX - 1);
                    return endX < startX
                        ? Array.Empty<CandidateTile>()
                        : Enumerable.Range(startX, endX - startX + 1)
                            .Select(x => new CandidateTile(x, y));
                })
                .OrderBy(tile =>
                    Math.Abs(playerX - tile.X) +
                    Math.Abs(playerY - tile.Y))
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X);
            foreach (var target in legalTiles)
            {
                var stand = FindBestMachineStandTile(
                    snapshot,
                    locationId,
                    target.X,
                    target.Y);
                if (stand.Tile is not null)
                {
                    return new MachinePlacementTileSelection(
                        target,
                        stand.Tile);
                }
            }
            return null;
        }

        private sealed record MachinePlacementTileSelection(
            CandidateTile Target,
            CandidateTile Stand);
    }
}
