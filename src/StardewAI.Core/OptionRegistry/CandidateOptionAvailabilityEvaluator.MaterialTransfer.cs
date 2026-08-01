using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] MaterialTransferCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters)
    {
        if (parameters.Length == 0)
        {
            return Array.Empty<EventCandidate>();
        }

        var reasons = new List<string>();
        if (!MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent) ||
            intent is null)
        {
            return new[]
            {
                BlockedMaterialTransferCandidate(
                    parameters,
                    "material_transfer_typed_intent_required")
            };
        }

        if (!MaterialTransferIntentBinder.TryReadGraph(snapshot, out var graph))
        {
            return new[]
            {
                BlockedMaterialTransferCandidate(
                    parameters,
                    "material_transfer_graph_unavailable")
            };
        }

        var projection = new MaterialTransferProjector().Project(graph!, intent!);
        reasons.AddRange(projection.BlockingReasons);

        var chestNodeIds = graph!.InventoryNodes
            .Where(node =>
                (node.NodeId == intent!.SourceNodeId || node.NodeId == intent.DestinationNodeId) &&
                node.InventoryKind == "chest")
            .Select(node => node.NodeId)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var chestNodeId = chestNodeIds.Length == 1 ? chestNodeIds[0] : string.Empty;
        var accessRows = string.IsNullOrWhiteSpace(chestNodeId)
            ? Array.Empty<MaterialInventoryAccessPoint>()
            : graph.AccessPoints.Where(row => row.NodeId == chestNodeId).ToArray();
        var access = accessRows.Length == 1 ? accessRows[0] : null;
        if (access is null || !access.TileX.HasValue || !access.TileY.HasValue)
        {
            reasons.Add("material_transfer_chest_access_not_unique");
        }

        if (ActiveMenuOpenForCandidate(snapshot))
        {
            reasons.Add("material_transfer_active_menu_open");
        }

        CandidateTile? stand = null;
        if (access?.TileX is int targetX && access.TileY is int targetY)
        {
            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (!string.Equals(currentLocation, access.LocationId, StringComparison.Ordinal))
            {
                reasons.Add("material_transfer_player_not_in_chest_location");
            }

            stand = FindBestStandTile(snapshot, targetX, targetY);
            if (stand is null)
            {
                reasons.Add("material_transfer_adjacent_stand_tile_unavailable");
            }
            else
            {
                reasons.AddRange(
                    CompilerProbeBlockingReasons(
                            snapshot,
                            MachineStandTileProbeCandidate(snapshot, stand))
                        .Where(reason => reason != "missing_required_state"));
            }
        }

        var normalizedParameters = MaterialTransferCandidateParameters(
            intent!,
            access,
            stand);
        var distinctReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        var expectedEffect = projection.Status == "projected"
            ? "material_transfer_projected=true" +
              ";source_stack_after=" + projection.SourceStackAfter +
              ";destination_quantity_before=" + projection.DestinationQuantityBefore +
              ";destination_quantity_after=" + projection.DestinationQuantityAfter +
              (access?.TileX is int x && access.TileY is int y
                  ? ";chest_tile=" + x + "," + y
                  : string.Empty) +
              (stand is not null
                  ? ";route_stand_tile=" + stand.X + "," + stand.Y
                  : string.Empty)
            : "material_transfer_projected=false";
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null
            ? 0
            : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "transfer:" + intent.SourceNodeId + ":" +
                    intent.SourceSlotIndex + "->" + intent.DestinationNodeId +
                    ":" + intent.Quantity,
                Kind = "transfer_inventory_item",
                Available = distinctReasons.Length == 0,
                LocationId = access?.LocationId ?? string.Empty,
                TileX = access?.TileX,
                TileY = access?.TileY,
                ExpectedEffect = expectedEffect,
                QualifiedItemId = intent.QualifiedItemId,
                SlotIndex = intent.SourceSlotIndex,
                Quantity = intent.Quantity,
                EstimatedTicks = Math.Max(120, distance * 16 + 120),
                EnergyCost = 0,
                AvailabilityClass = "explicit_material_transfer_intent",
                AllowedNow = distinctReasons.Length == 0,
                AllowedToday = distinctReasons.Length == 0,
                BlockReasons = distinctReasons,
                Parameters = normalizedParameters
            }
        };
    }

    private static EventCandidate BlockedMaterialTransferCandidate(
        SmallModelActionParameter[] parameters,
        string reason) => new()
    {
        CandidateId = "transfer:invalid_intent",
        Kind = "transfer_inventory_item",
        Available = false,
        AvailabilityClass = "explicit_material_transfer_intent",
        AllowedNow = false,
        AllowedToday = false,
        BlockReasons = new[] { reason },
        Parameters = parameters
    };

    private static SmallModelActionParameter[] MaterialTransferCandidateParameters(
        MaterialTransferIntent intent,
        MaterialInventoryAccessPoint? access,
        CandidateTile? stand)
    {
        var result = new List<SmallModelActionParameter>
        {
            Parameter("source_node_id", intent.SourceNodeId),
            Parameter("destination_node_id", intent.DestinationNodeId),
            Parameter("source_slot_index", intent.SourceSlotIndex.ToString()),
            Parameter("qualified_item_id", intent.QualifiedItemId),
            Parameter("quality", intent.Quality.ToString()),
            Parameter("quantity", intent.Quantity.ToString()),
            Parameter("expected_source_stack", intent.ExpectedSourceStack.ToString())
        };
        if (access is not null)
        {
            result.Add(Parameter("location_id", access.LocationId));
            if (access.TileX.HasValue && access.TileY.HasValue)
            {
                result.Add(Parameter("target_tile_x", access.TileX.Value.ToString()));
                result.Add(Parameter("target_tile_y", access.TileY.Value.ToString()));
            }
        }
        if (stand is not null)
        {
            result.Add(Parameter("stand_tile_x", stand.X.ToString()));
            result.Add(Parameter("stand_tile_y", stand.Y.ToString()));
        }

        return result.ToArray();
    }

}
