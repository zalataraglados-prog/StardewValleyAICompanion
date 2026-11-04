using System;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static bool IsMaterialTransferApproachCandidate(OptionAvailabilityCandidate candidate) =>
        candidate.OptionId == "inventory.transfer_item" && candidate.Parameters.Count(row =>
            row.Name == MaterialTransferIntentBinder.PhaseParameter) == 1 && candidate.Parameters.Any(row =>
            row.Name == MaterialTransferIntentBinder.PhaseParameter && row.Value == MaterialTransferIntentBinder.ApproachPhase);

    private EventCandidate[] MaterialTransferApproachCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
    {
        if (!MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent) ||
            !MaterialTransferIntentBinder.TryReadGraph(snapshot, out var graph))
            return new[] { BlockedMaterialTransferCandidate(parameters, "material_transfer_approach_source_unavailable") };
        var physical = new MaterialTransferProjector().Project(graph!, intent!);
        if (physical.BlockingReasons.Length != 1 || physical.BlockingReasons[0] != "material_transfer_player_not_in_chest_location")
            return new[] { BlockedMaterialTransferCandidate(parameters, "material_transfer_approach_not_required_or_source_invalid") };
        var chest = graph!.InventoryNodes.Single(node =>
            (node.NodeId == intent!.SourceNodeId || node.NodeId == intent.DestinationNodeId) && node.InventoryKind == "chest");
        var access = graph.AccessPoints.Single(row => row.NodeId == chest.NodeId);
        if (!access.ActorUseAuthorized || ActiveMenuOpenForCandidate(snapshot))
            return new[] { BlockedMaterialTransferCandidate(parameters, "material_transfer_approach_access_unavailable") };
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "locations", "route_graph")) ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "locations", "route_connectors")))
            return new[] { BlockedMaterialTransferCandidate(parameters, "material_transfer_approach_route_unavailable") };
        var current = ReadStateFieldString(snapshot, "player", "location_id");
        var route = FindResolvedRoutePlan(snapshot, current, chest.LocationId, RouteConnectorCandidates(snapshot, int.MaxValue));
        if (route?.FirstActionCandidate is null)
            return new[] { BlockedMaterialTransferCandidate(parameters, "material_transfer_approach_route_unavailable") };
        var first = route.FirstActionCandidate;
        var result = CloneCandidate(first,
            candidateId: "material-transfer-approach:" + chest.NodeId + ":" + intent!.QualifiedItemId + ":via:" + first.CandidateId,
            expectedEffect: first.ExpectedEffect + ";material_transfer_approach_only=true;material_transfer_target_node=" + chest.NodeId,
            parameters: first.Parameters.Concat(new[]
            {
                Parameter("material_transfer.approach_only", "true"),
                Parameter("material_transfer.target_node_id", chest.NodeId),
                Parameter("material_transfer.source_node_id", intent.SourceNodeId),
                Parameter("material_transfer.destination_node_id", intent.DestinationNodeId),
                Parameter("material_transfer.snapshot_policy", "one_route_action_then_fresh_replan")
            }).ToArray());
        result.QualifiedItemId = intent.QualifiedItemId;
        result.Quantity = intent.Quantity;
        return new[] { result };
    }
}
