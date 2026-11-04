using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public static partial class MuseumOutputCustodyTransferRequestBuilder
{
    private sealed record TransferSource(MaterialReservation Custody, MaterialInventoryGraph Graph, MaterialTransferIntent Intent, string Position);

    private static TransferSource? ReadSource(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        string reservationId, string destinationNodeId, out string[] errors)
    {
        errors = Array.Empty<string>();
        if (ledger.PreparedMaterialTransfer is not null)
        {
            errors = new[] { "museum_custody_transfer_preparation_already_pending" };
            return null;
        }
        var rows = ledger.MaterialReservations.Where(row => row.ReservationId == reservationId).ToArray();
        if (rows.Length != 1 || !MaterialOutputCustodyContract.TryGetPositionBinding(rows[0], ledger, out var position))
        {
            errors = new[] { "museum_custody_transfer_source_not_authorized" };
            return null;
        }
        var custody = rows[0];
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.SaveId.Value != ledger.SaveId ||
            snapshot.PlayerId.Status != FieldStatus.Available || snapshot.PlayerId.Value != ledger.PlayerId ||
            string.IsNullOrWhiteSpace(snapshot.StateHash) || !MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            graph!.PlayerId.ToString(CultureInfo.InvariantCulture) != ledger.PlayerId)
        {
            errors = new[] { "museum_custody_transfer_snapshot_identity_invalid" };
            return null;
        }
        var nodes = graph.InventoryNodes.Where(node => node.NodeId == custody.NodeId).ToArray();
        var slots = nodes.Length == 1 ? nodes[0].Slots.Where(slot => slot.SlotIndex == custody.SlotIndex &&
            slot.QualifiedItemId == custody.QualifiedItemId).ToArray() : Array.Empty<MaterialInventorySlot>();
        if (slots.Length != 1)
        {
            errors = new[] { "museum_custody_transfer_source_slot_not_unique" };
            return null;
        }
        return new(custody, graph, new MaterialTransferIntent
        {
            SourceNodeId = custody.NodeId, SourceSlotIndex = custody.SlotIndex, DestinationNodeId = destinationNodeId,
            QualifiedItemId = custody.QualifiedItemId, Quantity = custody.Quantity,
            Quality = slots[0].Quality, ExpectedSourceStack = slots[0].Stack
        }, position);
    }
}
