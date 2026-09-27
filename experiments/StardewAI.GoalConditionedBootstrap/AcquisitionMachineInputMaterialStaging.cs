using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionMachineInputMaterialStaging
{
    public static PolicyEventCandidatePrediction[] BuildCandidates(
        SnapshotEnvelope snapshot,
        AcquisitionRouteTargetDateReservation reservation,
        out string[] blockingReasons)
    {
        var reasons = new List<string>();
        var claims = reservation.ClaimSet?.MaterialClaims ??
            Array.Empty<MaterialReservationUpsertRequest>();
        if (!TryReadGraph(snapshot, out var graph))
        {
            blockingReasons = new[]
            {
                "machine_input_staging_material_graph_unavailable"
            };
            return Array.Empty<PolicyEventCandidatePrediction>();
        }
        var playerNodes = graph!.InventoryNodes.Where(node =>
                node.InventoryKind == "player_inventory" &&
                node.OwnerPlayerId == graph.PlayerId &&
                node.SupplyState == "available" &&
                node.ActorUseAuthorized)
            .ToArray();
        if (playerNodes.Length != 1)
        {
            blockingReasons = new[]
            {
                "machine_input_staging_player_node_not_unique"
            };
            return Array.Empty<PolicyEventCandidatePrediction>();
        }

        var staged = claims.Select(claim => new
            {
                Claim = claim,
                Nodes = graph.InventoryNodes.Where(node =>
                        node.NodeId == claim.NodeId &&
                        node.InventoryKind == "chest" &&
                        node.SupplyState == "available" &&
                        node.ActorUseAuthorized &&
                        node.LocationId == playerNodes[0].LocationId &&
                        IsCurrentNormalChest(
                            graph,
                            node.NodeId,
                            node.LocationId))
                    .ToArray()
            })
            .Where(value => value.Nodes.Length > 0)
            .OrderBy(value => value.Claim.ReservationId,
                StringComparer.Ordinal)
            .ToArray();
        if (staged.Length == 0)
        {
            blockingReasons = Array.Empty<string>();
            return Array.Empty<PolicyEventCandidatePrediction>();
        }

        var selected = staged[0];
        if (selected.Nodes.Length != 1)
        {
            blockingReasons = new[]
            {
                "machine_input_staging_source_node_not_unique"
            };
            return Array.Empty<PolicyEventCandidatePrediction>();
        }
        var slots = selected.Nodes[0].Slots.Where(slot =>
                slot.SlotIndex == selected.Claim.SlotIndex &&
                slot.QualifiedItemId == selected.Claim.QualifiedItemId &&
                slot.Stack >= selected.Claim.Quantity)
            .ToArray();
        if (slots.Length != 1)
        {
            blockingReasons = new[]
            {
                "machine_input_staging_source_slot_not_unique"
            };
            return Array.Empty<PolicyEventCandidatePrediction>();
        }
        var intent = new MaterialTransferIntent
        {
            SourceNodeId = selected.Claim.NodeId,
            DestinationNodeId = playerNodes[0].NodeId,
            SourceSlotIndex = selected.Claim.SlotIndex,
            QualifiedItemId = selected.Claim.QualifiedItemId,
            Quality = slots[0].Quality,
            Quantity = selected.Claim.Quantity,
            ExpectedSourceStack = slots[0].Stack
        };
        var projection = new MaterialTransferProjector().Project(graph, intent);
        if (projection.Status != "projected")
        {
            reasons.AddRange(projection.BlockingReasons.Select(reason =>
                "machine_input_staging:" + reason));
        }
        if (projection.DestinationSlotChanges.Length != 1)
        {
            reasons.Add(
                "machine_input_staging_requires_single_destination_slot");
        }
        if (reasons.Count > 0)
        {
            blockingReasons = reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Array.Empty<PolicyEventCandidatePrediction>();
        }

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "inventory.transfer_item",
                    Parameters = IntentParameters(intent)
                }
            },
            includeExecutorCalibrationOptions: true);
        var candidates = new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability)
            .Where(candidate =>
                candidate.OptionId == "inventory.transfer_item" &&
                candidate.Kind == "transfer_inventory_item" &&
                candidate.Available)
            .ToArray();
        if (candidates.Length != 1)
        {
            reasons.AddRange(availability.Options.SelectMany(option =>
                option.BlockingReasons.Concat(
                    option.EventCandidates.SelectMany(candidate =>
                        candidate.BlockReasons))));
            reasons.Add(
                "machine_input_staging_candidate_not_uniquely_available");
        }
        blockingReasons = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return blockingReasons.Length == 0
            ? candidates
            : Array.Empty<PolicyEventCandidatePrediction>();
    }

    public static bool TryReadGraph(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph)
    {
        graph = null;
        if (!snapshot.State.TryGetValue("farm", out var farm) ||
            farm.ValueKind != JsonValueKind.Object ||
            !farm.TryGetProperty("material_inventory_graph", out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.GetString() is not ("available" or "derived") ||
            !envelope.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        try
        {
            graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.GetRawText(),
                JsonDefaults.Options);
            return graph is
            {
                SchemaVersion: "material_inventory_graph.v1",
                Status: "available"
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static SmallModelActionParameter[] IntentParameters(
        MaterialTransferIntent intent) => new[]
        {
            Parameter("source_node_id", intent.SourceNodeId),
            Parameter("destination_node_id", intent.DestinationNodeId),
            Parameter("source_slot_index", intent.SourceSlotIndex.ToString()),
            Parameter("qualified_item_id", intent.QualifiedItemId),
            Parameter("quality", intent.Quality.ToString()),
            Parameter("quantity", intent.Quantity.ToString()),
            Parameter("expected_source_stack",
                intent.ExpectedSourceStack.ToString())
        };

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private static bool IsCurrentNormalChest(
        MaterialInventoryGraph graph,
        string nodeId,
        string locationId)
    {
        var access = graph.AccessPoints.Where(row => row.NodeId == nodeId)
            .ToArray();
        return access is
        [
            {
                AccessKind: "placed_chest",
                SpecialChestType: "None",
                LockedByOtherPlayer: false,
                LocationIsCurrent: true,
                TileX: not null,
                TileY: not null
            }
        ] && access[0].LocationId == locationId;
    }
}
