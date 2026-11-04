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
    private static MaterialTransferReservationProjection? ValidatePreparedMaterialTransfer(
        StrategyCommitmentLedger? current, SnapshotEnvelope snapshot,
        ReservationPortfolioSupportingTransitionSettlementRequest request,
        ReservationPortfolioMaterialRelocation[] relocations, ICollection<string> errors,
        out MaterialInventoryGraph? afterGraph)
    {
        afterGraph = null;
        var prepared = current?.PreparedMaterialTransfer;
        if (request.PreparedMaterialTransferSha256 is null &&
            (prepared is null || prepared.PortfolioId != request.PortfolioId && relocations.Length == 0))
            return null;
        if (request.PreparedMaterialTransferSha256 is null)
        {
            errors.Add("prepared_material_transfer_binding_required");
            return null;
        }
        if (current is null || prepared is null)
        {
            errors.Add("prepared_material_transfer_missing");
            return null;
        }
        if (!PreparedMaterialTransferContract.IsBound(current, request.PreparedMaterialTransferSha256,
                request.PortfolioId, request.GoalId, request.RouteSourceDecisionId) ||
            prepared.SourceStateHash == snapshot.StateHash || snapshot.GameTick < prepared.SourceGameTick ||
            relocations.Length == 0 ||
            JsonSerializer.Serialize(prepared.MaterialRelocations) != JsonSerializer.Serialize(relocations))
        {
            errors.Add("prepared_material_transfer_binding_drifted");
            return null;
        }

        // The pinned ledger revision/hash protects other claims; only two inventories were retained.
        var intent = prepared.Intent;
        var view = new StrategyCommitmentLedger
        {
            LedgerId = current.LedgerId, SaveId = current.SaveId, PlayerId = current.PlayerId,
            Revision = current.Revision, SourceStateHash = current.SourceStateHash,
            History = current.History ?? Array.Empty<StrategyCommitmentHistoryEntry>(),
            MaterialReservations = current.MaterialReservations.Where(row =>
                row.NodeId == intent.SourceNodeId || row.NodeId == intent.DestinationNodeId).ToArray()
        };
        var projector = new MaterialTransferProjector();
        var projection = projector.ProjectReservations(prepared.BeforeInventoryGraph, intent, view, relocations);
        foreach (var reason in projection.BlockingReasons) errors.Add(reason);
        if (JsonSerializer.Serialize(projection) != JsonSerializer.Serialize(prepared.ReservationProjection))
            errors.Add("prepared_material_transfer_projection_drifted");
        if (projection.Status == "projected" && !PreparedMaterialTransferContract.CustodyPositionsBound(current, projection))
            errors.Add("output_custody_rebind_requires_retained_terminal_lifecycle");
        if (!MaterialInventoryGraphReader.TryRead(snapshot, out afterGraph))
            errors.Add("prepared_material_transfer_after_graph_unavailable");
        else
        {
            var physical = projector.Project(prepared.BeforeInventoryGraph, intent);
            foreach (var reason in MaterialTransferProjector.VerifyNativeSlots(prepared.BeforeInventoryGraph, afterGraph!, physical))
                errors.Add(reason);
        }
        return errors.Count == 0 ? projection : null;
    }

    private static void AppendMaterialTransferHistory(StrategyCommitmentLedger ledger,
        ReservationPortfolioSupportingTransitionSettlementRequest request,
        MaterialTransferReservationProjection projection, string updatedAt)
    {
        var relocatedIds = request.MaterialRelocations.Select(row => row.MaterialReservationId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var change in projection.ReservationTransitions.Where(row => !relocatedIds.Contains(row.ReservationId)))
        {
            var row = ledger.MaterialReservations.Single(value => value.ReservationId == change.ReservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(ledger, row.ReservationId, row.Revision,
                row.SourceDecisionId, "material_reservation_rebound_after_native_transfer", updatedAt,
                request.PreparedMaterialTransferSha256!);
        }
        StrategyCommitmentLedgerSupport.AppendHistory(ledger, request.PortfolioId, ledger.Revision,
            request.RouteSourceDecisionId, "material_transfer_preparation_completed", updatedAt,
            request.PreparedMaterialTransferSha256!);
    }
}
