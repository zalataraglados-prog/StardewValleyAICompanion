using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private const int MachineLayoutEvaluationCycles = 8;
        private const int MachineLayoutActionOverheadTicks = 120;

        private EventCandidate[] MachineRelocationCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            if (!RoutePathPreviewAvailable(snapshot))
            {
                return Array.Empty<EventCandidate>();
            }

            var activeIntent =
                commitmentLedger?.MachineRelocationIntents
                    .FirstOrDefault(row => string.Equals(
                        row.Status,
                        StrategyCommitmentStatuses.Active,
                        StringComparison.Ordinal));
            if (activeIntent is not null &&
                !MachineExistsAt(
                    snapshot,
                    activeIntent.SourceLocationId,
                    activeIntent.SourceTileX,
                    activeIntent.SourceTileY,
                    activeIntent.QualifiedItemId))
            {
                return Array.Empty<EventCandidate>();
            }

            var machinesValue = ReadStateFieldValue(
                snapshot,
                "farm",
                "machines");
            var placementValue = ReadStateFieldValue(
                snapshot,
                "player",
                "machine_placement");
            if (!machinesValue.HasValue ||
                machinesValue.Value.ValueKind != JsonValueKind.Array ||
                !placementValue.HasValue ||
                placementValue.Value.ValueKind != JsonValueKind.Object ||
                !placementValue.Value.TryGetProperty(
                    "relocation_rows",
                    out var relocationRows) ||
                relocationRows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentLocationId = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            var machines = machinesValue.Value.EnumerateArray()
                .Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        MachineLocationId(row),
                        currentLocationId,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (machines.Length < 2)
            {
                return Array.Empty<EventCandidate>();
            }

            var rows = relocationRows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .ToArray();
            var proposals = machines
                .Where(machine =>
                    ReadBool(machine, "removal_safe_now") == true &&
                    string.Equals(
                        ReadString(machine, "removal_status"),
                        "safe_idle_native_pickaxe",
                        StringComparison.Ordinal))
                .Select(machine => BuildMachineRelocationCandidate(
                    snapshot,
                    machine,
                    machines,
                    rows,
                    currentLocationId,
                    ReadString(
                        placementValue.Value,
                        "static_projection_fingerprint")))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .OrderByDescending(candidate =>
                    ReadCandidateInt(
                        candidate,
                        "layout_net_benefit_ticks"))
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Take(1)
                .ToArray();
            return proposals;
        }

        private EventCandidate? BuildMachineRelocationCandidate(
            SnapshotEnvelope snapshot,
            JsonElement source,
            IReadOnlyCollection<JsonElement> locationMachines,
            IReadOnlyCollection<JsonElement> relocationRows,
            string locationId,
            string placementProjectionFingerprint)
        {
            var sourceX = ReadInt(source, "tile_x");
            var sourceY = ReadInt(source, "tile_y");
            var qualifiedItemId = ReadString(
                source,
                "qualified_item_id");
            var peers = locationMachines
                .Where(row =>
                    ReadInt(row, "tile_x") != sourceX ||
                    ReadInt(row, "tile_y") != sourceY)
                .ToArray();
            if (peers.Length == 0 ||
                string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return null;
            }

            var relocationRow = relocationRows.FirstOrDefault(row =>
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase));
            if (relocationRow.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var location = CurrentMachinePlacementLocation(
                relocationRow,
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
                    "machine_operational_context_valid") != true)
            {
                return null;
            }

            var sourceStand = FindBestMachineStandTile(
                snapshot,
                locationId,
                sourceX,
                sourceY);
            if (sourceStand.Tile is null)
            {
                return null;
            }

            var currentClusterDistance = NearestMachineDistance(
                sourceX,
                sourceY,
                peers);
            var target = SelectRelocationTarget(
                snapshot,
                location.Value,
                locationId,
                sourceX,
                sourceY,
                peers);
            if (target is null ||
                target.ClusterDistance >= currentClusterDistance)
            {
                return null;
            }

            var interactionCount =
                Math.Max(
                    1,
                    (ReadBool(source, "machine_has_input") == true ? 1 : 0) +
                    (ReadBool(source, "machine_has_output") == true ? 1 : 0));
            var savedDistance =
                currentClusterDistance - target.ClusterDistance;
            var savedTicksPerCycle =
                savedDistance * 60 * interactionCount;
            var playerX = ReadStateFieldInt(
                snapshot,
                "player",
                "tile_x");
            var playerY = ReadStateFieldInt(
                snapshot,
                "player",
                "tile_y");
            var relocationCostTicks =
                (Math.Abs(playerX - sourceStand.Tile.X) +
                 Math.Abs(playerY - sourceStand.Tile.Y) +
                 Math.Abs(sourceStand.Tile.X - target.Stand.X) +
                 Math.Abs(sourceStand.Tile.Y - target.Stand.Y)) * 60 +
                MachineLayoutActionOverheadTicks;
            var netBenefitTicks =
                savedTicksPerCycle * MachineLayoutEvaluationCycles -
                relocationCostTicks;
            if (savedTicksPerCycle <= 0 || netBenefitTicks <= 0)
            {
                return null;
            }

            var breakEvenCycles =
                (relocationCostTicks + savedTicksPerCycle - 1) /
                savedTicksPerCycle;
            var relocationIntentId =
                "layout:" + locationId + ":" + sourceX + "," +
                sourceY + "->" + target.Target.X + "," +
                target.Target.Y + ":" + qualifiedItemId;
            var itemId = ReadString(relocationRow, "item_id");
            return new EventCandidate
            {
                CandidateId = "machine-relocate:" + locationId + ":" +
                    sourceX + "," + sourceY + "->" +
                    target.Target.X + "," + target.Target.Y + ":" +
                    qualifiedItemId,
                Kind = "relocate_machine_item",
                Available = true,
                LocationId = locationId,
                TileX = sourceX,
                TileY = sourceY,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                Quantity = 1,
                EstimatedTicks = relocationCostTicks,
                EnergyCost = 0,
                AvailabilityClass =
                    "transparent_machine_layout_positive_route_benefit",
                ExpectedEffect =
                    "farm.machines[" + locationId + ":" + sourceX +
                    "," + sourceY + "]=missing" +
                    ";machine_recovery[" + qualifiedItemId +
                    "]=debris_or_native_auto_collected_inventory" +
                    ";relocation_target=" + locationId + ":" +
                    target.Target.X + "," + target.Target.Y +
                    ";layout_saved_ticks_per_service_cycle=" +
                    savedTicksPerCycle +
                    ";layout_break_even_cycles=" + breakEvenCycles +
                    ";layout_net_benefit_ticks=" + netBenefitTicks +
                    ";continuation=fresh_snapshot_then_exact_native_placement",
                Parameters = new[]
                {
                    Parameter("location_id", locationId),
                    Parameter("stand_tile_x", sourceStand.Tile.X.ToString()),
                    Parameter("stand_tile_y", sourceStand.Tile.Y.ToString()),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("item_id", itemId),
                    Parameter(
                        "tool_slot_index",
                        ReadInt(source, "removal_tool_slot_index", -1)
                            .ToString()),
                    Parameter(
                        "tool_qualified_item_id",
                        ReadString(
                            source,
                            "removal_tool_qualified_item_id")),
                    Parameter(
                        "native_contract",
                        ReadString(source, "removal_native_contract")),
                    Parameter(
                        "machine_removal_projection_fingerprint",
                        ReadString(
                            source,
                            "removal_projection_fingerprint")),
                    Parameter(
                        "machine_placement_projection_fingerprint",
                        placementProjectionFingerprint),
                    Parameter(
                        "relocation_intent_id",
                        relocationIntentId),
                    Parameter(
                        "relocation_target_location_id",
                        locationId),
                    Parameter(
                        "relocation_target_tile_x",
                        target.Target.X.ToString()),
                    Parameter(
                        "relocation_target_tile_y",
                        target.Target.Y.ToString()),
                    Parameter(
                        "relocation_target_stand_tile_x",
                        target.Stand.X.ToString()),
                    Parameter(
                        "relocation_target_stand_tile_y",
                        target.Stand.Y.ToString()),
                    Parameter(
                        "layout_current_cluster_distance",
                        currentClusterDistance.ToString()),
                    Parameter(
                        "layout_target_cluster_distance",
                        target.ClusterDistance.ToString()),
                    Parameter(
                        "layout_service_interactions_per_cycle",
                        interactionCount.ToString()),
                    Parameter(
                        "layout_saved_ticks_per_service_cycle",
                        savedTicksPerCycle.ToString()),
                    Parameter(
                        "layout_relocation_cost_ticks",
                        relocationCostTicks.ToString()),
                    Parameter(
                        "layout_evaluation_cycles",
                        MachineLayoutEvaluationCycles.ToString()),
                    Parameter(
                        "layout_break_even_cycles",
                        breakEvenCycles.ToString()),
                    Parameter(
                        "layout_net_benefit_ticks",
                        netBenefitTicks.ToString()),
                    Parameter(
                        "layout_benefit_policy",
                        "nearest_machine_service_route_over_eight_cycles")
                }
            };
        }

    }
}
