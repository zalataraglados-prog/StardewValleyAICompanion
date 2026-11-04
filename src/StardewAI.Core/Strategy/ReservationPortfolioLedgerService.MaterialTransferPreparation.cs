using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    private static void PrepareMaterialTransfer(StrategyCommitmentLedger staged, SnapshotEnvelope snapshot,
        ReservationPortfolioCommitRequest request, ICollection<string> errors)
    {
        var preparation = request.MaterialTransferPreparation;
        if (preparation is null) return;
        if (staged.PreparedMaterialTransfer is not null)
        {
            errors.Add("material_transfer_preparation_already_pending");
            return;
        }
        if (preparation.Intent is null || preparation.MaterialRelocations is not { Length: > 0 } moves ||
            moves.Any(row => row is null) || string.IsNullOrWhiteSpace(preparation.RouteSourceDecisionId) ||
            request.SourceDecisionId != request.PortfolioId ||
            !MaterialInventoryGraphReader.TryRead(snapshot, out var graph))
        {
            errors.Add("material_transfer_preparation_invalid");
            return;
        }
        foreach (var move in moves)
        {
            var claims = staged.MaterialReservations.Where(row => row.ReservationId == move.MaterialReservationId).ToArray();
            if (claims.Length != 1 || claims[0].GoalId != request.GoalId ||
                claims[0].SourceDecisionId != preparation.RouteSourceDecisionId)
                errors.Add("material_transfer_preparation_claim_authority_invalid");
        }
        var projection = new MaterialTransferProjector().ProjectReservations(graph!, preparation.Intent, staged, moves);
        foreach (var reason in projection.BlockingReasons) errors.Add(reason);
        if (projection.Status == "projected" && !PreparedMaterialTransferContract.CustodyPositionsBound(staged, projection))
            errors.Add("output_custody_relocation_requires_retained_terminal_lifecycle");
        if (errors.Count > 0) return;

        var nodes = graph!.InventoryNodes.Where(node => node.NodeId == preparation.Intent.SourceNodeId ||
            node.NodeId == preparation.Intent.DestinationNodeId).ToArray();
        var compact = new MaterialInventoryGraph
        {
            PlayerId = graph.PlayerId,
            InventoryNodes = nodes,
            AccessPoints = graph.AccessPoints.Where(row => nodes.Any(node => node.NodeId == row.NodeId)).ToArray(),
            PhysicalInventoryCount = nodes.Length,
            DefaultSharedResourcePolicy = graph.DefaultSharedResourcePolicy
        };
        compact.AccessPointCount = compact.AccessPoints.Length;
        compact.DeduplicatedAccessPointCount = compact.AccessPoints.Length;
        // Deep-clone only the two physical inventories, never a full snapshot or world graph.
        var retained = JsonSerializer.Deserialize<PreparedMaterialTransfer>(JsonSerializer.Serialize(new PreparedMaterialTransfer
        {
            PortfolioId = request.PortfolioId,
            GoalId = request.GoalId,
            RouteSourceDecisionId = preparation.RouteSourceDecisionId,
            SourceStateHash = snapshot.StateHash,
            SourceGameTick = snapshot.GameTick,
            ExpectedLedgerRevision = staged.Revision,
            MaterialReservationsSha256 = PreparedMaterialTransferContract.ComputeMaterialReservationsSha256(staged),
            Intent = preparation.Intent,
            MaterialRelocations = moves,
            BeforeInventoryGraph = compact,
            ReservationProjection = projection
        }))!;
        retained.BindingSha256 = PreparedMaterialTransferContract.ComputeBindingSha256(retained);
        staged.PreparedMaterialTransfer = retained;
    }
}
