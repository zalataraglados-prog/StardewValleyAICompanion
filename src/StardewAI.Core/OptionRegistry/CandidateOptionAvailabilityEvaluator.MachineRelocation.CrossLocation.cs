using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private IEnumerable<EventCandidate>
            BuildCrossLocationMachineRelocationCandidates(
                SnapshotEnvelope snapshot,
                JsonElement source,
                IReadOnlyCollection<JsonElement> sourceLocationMachines,
                IReadOnlyCollection<JsonElement> allMachines,
                IReadOnlyCollection<JsonElement> relocationRows,
                string sourceLocationId,
                string placementProjectionFingerprint,
                EventCandidate[] routeCandidates)
        {
            var sourceX = ReadInt(source, "tile_x");
            var sourceY = ReadInt(source, "tile_y");
            var qualifiedItemId = ReadString(
                source,
                "qualified_item_id");
            var itemId = ReadString(source, "item_id");
            var sourcePeers = sourceLocationMachines
                .Where(row =>
                    ReadInt(row, "tile_x") != sourceX ||
                    ReadInt(row, "tile_y") != sourceY)
                .ToArray();
            if (sourcePeers.Length == 0 ||
                string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return Array.Empty<EventCandidate>();
            }

            var relocationRow = relocationRows.FirstOrDefault(row =>
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase));
            if (relocationRow.ValueKind != JsonValueKind.Object ||
                !relocationRow.TryGetProperty(
                    "locations",
                    out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var sourceStand = FindBestMachineStandTile(
                snapshot,
                sourceLocationId,
                sourceX,
                sourceY);
            if (sourceStand.Tile is null)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentClusterDistance = NearestMachineDistance(
                sourceX,
                sourceY,
                sourcePeers);
            var playerX = ReadStateFieldInt(
                snapshot,
                "player",
                "tile_x");
            var playerY = ReadStateFieldInt(
                snapshot,
                "player",
                "tile_y");
            var interactionCount =
                Math.Max(
                    1,
                    (ReadBool(source, "machine_has_input") == true ? 1 : 0) +
                    (ReadBool(source, "machine_has_output") == true ? 1 : 0));

            return locations.EnumerateArray()
                .Where(location =>
                    location.ValueKind == JsonValueKind.Object &&
                    !string.Equals(
                        ReadString(location, "location_id"),
                        sourceLocationId,
                        StringComparison.OrdinalIgnoreCase) &&
                    ReadBool(
                        location,
                        "location_is_player_controlled") == true &&
                    string.Equals(
                        ReadString(
                            location,
                            "placement_probe_status"),
                        "native_legal_tiles_available",
                        StringComparison.Ordinal) &&
                    ReadBool(
                        location,
                        "machine_operational_context_valid") == true)
                .Select(location => BuildCrossLocationMachineRelocationCandidate(
                    snapshot,
                    source,
                    sourceStand.Tile,
                    currentClusterDistance,
                    interactionCount,
                    playerX,
                    playerY,
                    allMachines,
                    location,
                    sourceLocationId,
                    sourceX,
                    sourceY,
                    qualifiedItemId,
                    itemId,
                    placementProjectionFingerprint,
                    routeCandidates))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!);
        }

        private EventCandidate?
            BuildCrossLocationMachineRelocationCandidate(
                SnapshotEnvelope snapshot,
                JsonElement source,
                CandidateTile sourceStand,
                int currentClusterDistance,
                int interactionCount,
                int playerX,
                int playerY,
                IReadOnlyCollection<JsonElement> allMachines,
                JsonElement targetLocation,
                string sourceLocationId,
                int sourceX,
                int sourceY,
                string qualifiedItemId,
                string itemId,
                string placementProjectionFingerprint,
                EventCandidate[] routeCandidates)
        {
            var targetLocationId = ReadString(
                targetLocation,
                "location_id");
            var targetPeers = allMachines.Where(machine =>
                string.Equals(
                    MachineLocationId(machine),
                    targetLocationId,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (targetPeers.Length == 0)
            {
                return null;
            }

            var routePlan = FindResolvedRoutePlan(
                snapshot,
                sourceLocationId,
                targetLocationId,
                routeCandidates);
            var route = routePlan?.FirstConnectorCandidate;
            if (routePlan is null ||
                route is null ||
                !route.Available ||
                route.EstimatedTicks < 0)
            {
                return null;
            }
            var routeEvidence = BuildRelocationRouteEvidence(
                snapshot,
                routePlan,
                route);
            if (routeEvidence is null)
            {
                return null;
            }

            var target = SelectCrossLocationRelocationTarget(
                snapshot,
                targetLocation,
                targetPeers,
                routeEvidence.FinalArrivalX,
                routeEvidence.FinalArrivalY);
            if (target is null ||
                target.ClusterDistance >= currentClusterDistance)
            {
                return null;
            }

            var savedDistance =
                currentClusterDistance - target.ClusterDistance;
            var savedTicksPerCycle =
                savedDistance * 60 * interactionCount;
            var sourceApproachTicks =
                (Math.Abs(playerX - sourceStand.X) +
                 Math.Abs(playerY - sourceStand.Y)) * 60;
            var targetApproachTicks =
                target.RouteDistanceTiles * 60;
            var relocationCostTicks =
                sourceApproachTicks +
                routeEvidence.EstimatedTicks +
                targetApproachTicks +
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
                "layout:" + sourceLocationId + ":" + sourceX + "," +
                sourceY + "->" + targetLocationId + ":" +
                target.Target.X + "," + target.Target.Y + ":" +
                qualifiedItemId;
            return new EventCandidate
            {
                CandidateId =
                    "machine-relocate:" + sourceLocationId + ":" +
                    sourceX + "," + sourceY + "->" +
                    targetLocationId + ":" + target.Target.X + "," +
                    target.Target.Y + ":" + qualifiedItemId,
                Kind = "relocate_machine_item",
                Available = true,
                LocationId = sourceLocationId,
                TileX = sourceX,
                TileY = sourceY,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                Quantity = 1,
                EstimatedTicks = relocationCostTicks,
                EnergyCost = 0,
                AvailabilityClass =
                    "transparent_machine_layout_positive_cross_location_route_benefit",
                ExpectedEffect =
                    "farm.machines[" + sourceLocationId + ":" +
                    sourceX + "," + sourceY + "]=missing" +
                    ";machine_recovery[" + qualifiedItemId +
                    "]=debris_or_native_auto_collected_inventory" +
                    ";relocation_target=" + targetLocationId + ":" +
                    target.Target.X + "," + target.Target.Y +
                    ";relocation_route_connector_count=" +
                    routeEvidence.Segments.Length +
                    ";layout_saved_ticks_per_service_cycle=" +
                    savedTicksPerCycle +
                    ";layout_break_even_cycles=" + breakEvenCycles +
                    ";layout_net_benefit_ticks=" + netBenefitTicks +
                    ";continuation=fresh_snapshot_then_route_then_exact_native_placement",
                Parameters = new[]
                {
                    Parameter("location_id", sourceLocationId),
                    Parameter("stand_tile_x", sourceStand.X.ToString()),
                    Parameter("stand_tile_y", sourceStand.Y.ToString()),
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
                        targetLocationId),
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
                        "relocation_target_route_distance_tiles",
                        target.RouteDistanceTiles.ToString()),
                    Parameter(
                        "relocation_route_connector_count",
                        routeEvidence.Segments.Length.ToString()),
                    Parameter(
                        "relocation_route_connector_kind",
                        ReadParameter(
                            route.Parameters,
                            "connector_kind")),
                    Parameter(
                        "relocation_route_expected_target_location",
                        targetLocationId),
                    Parameter(
                        "relocation_route_estimated_ticks",
                        routeEvidence.EstimatedTicks.ToString()),
                    Parameter(
                        "relocation_route_segments_json",
                        JsonSerializer.Serialize(
                            routeEvidence.Segments)),
                    Parameter(
                        "relocation_target_arrival_tile_x",
                        routeEvidence.FinalArrivalX.ToString()),
                    Parameter(
                        "relocation_target_arrival_tile_y",
                        routeEvidence.FinalArrivalY.ToString()),
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
                        "existing_machine_cluster_resolved_route_over_eight_cycles"),
                    Parameter(
                        "relocation_target_selection_policy",
                        "resolved_route_final_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked"),
                    Parameter(
                        "layout_time_estimate_policy",
                        "source_approach_plus_resolved_route_static_bfs_plus_target_static_bfs_runtime_rechecked")
                }
            };
        }
    }
}
