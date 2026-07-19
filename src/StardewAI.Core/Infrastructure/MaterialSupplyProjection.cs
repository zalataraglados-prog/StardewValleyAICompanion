using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Infrastructure;

public sealed class MaterialSupplyProjection
{
    public MaterialSupplyProjectionResult Project(
        MaterialInventoryGraph graph,
        IEnumerable<MaterialReservation>? reservations = null)
    {
        var blockingReasons = new List<string>();
        if (!string.Equals(graph.SchemaVersion, "material_inventory_graph.v1", StringComparison.Ordinal))
        {
            blockingReasons.Add("material_inventory_graph_schema_unsupported:" + graph.SchemaVersion);
        }

        var availableNodes = graph.InventoryNodes
            .Where(node => string.Equals(node.SupplyState, "available", StringComparison.Ordinal))
            .ToArray();
        var duplicateNodeIds = graph.InventoryNodes
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        blockingReasons.AddRange(duplicateNodeIds.Select(id => "material_inventory_duplicate_node_id:" + id));

        var slotMap = availableNodes
            .SelectMany(node => node.Slots.Select(slot => new SlotRef(node.NodeId, slot)))
            .GroupBy(row => SlotKey(row.NodeId, row.Slot.SlotIndex), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in slotMap.Where(pair => pair.Value.Length > 1).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            blockingReasons.Add("material_inventory_duplicate_slot:" + duplicate.Key);
        }

        var reservedBySlot = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var reservation in reservations ?? Array.Empty<MaterialReservation>())
        {
            var key = SlotKey(reservation.NodeId, reservation.SlotIndex);
            if (reservation.Quantity <= 0)
            {
                blockingReasons.Add("material_reservation_quantity_not_positive:" + reservation.ReservationId);
                continue;
            }
            if (!slotMap.TryGetValue(key, out var slots) || slots.Length != 1)
            {
                blockingReasons.Add("material_reservation_slot_unavailable:" + reservation.ReservationId);
                continue;
            }
            if (!string.Equals(slots[0].Slot.QualifiedItemId, reservation.QualifiedItemId, StringComparison.Ordinal))
            {
                blockingReasons.Add("material_reservation_item_mismatch:" + reservation.ReservationId);
                continue;
            }

            var previous = reservedBySlot.TryGetValue(key, out var reserved) ? reserved : 0;
            var combined = (long)previous + reservation.Quantity;
            if (combined > int.MaxValue)
            {
                blockingReasons.Add("material_reservation_quantity_overflow:" + key);
                reservedBySlot[key] = int.MaxValue;
                continue;
            }
            reservedBySlot[key] = (int)combined;
        }

        var projectedSlots = slotMap
            .Where(pair => pair.Value.Length == 1)
            .Select(pair =>
            {
                var row = pair.Value[0];
                var reserved = reservedBySlot.TryGetValue(pair.Key, out var quantity) ? quantity : 0;
                if (reserved > row.Slot.Stack)
                {
                    blockingReasons.Add("material_reservation_exceeds_stack:" + pair.Key);
                }
                return new MaterialSupplySlot
                {
                    NodeId = row.NodeId,
                    SlotIndex = row.Slot.SlotIndex,
                    QualifiedItemId = row.Slot.QualifiedItemId,
                    Stack = row.Slot.Stack,
                    ReservedQuantity = reserved,
                    AvailableQuantity = Math.Max(0, row.Slot.Stack - reserved)
                };
            })
            .OrderBy(row => row.NodeId, StringComparer.Ordinal)
            .ThenBy(row => row.SlotIndex)
            .ToArray();
        var quantities = projectedSlots
            .GroupBy(row => row.QualifiedItemId, StringComparer.Ordinal)
            .Select(group => new MaterialSupplyQuantity
            {
                QualifiedItemId = group.Key,
                TotalQuantity = group.Sum(row => row.Stack),
                ReservedQuantity = group.Sum(row => row.ReservedQuantity),
                AvailableQuantity = group.Sum(row => row.AvailableQuantity)
            })
            .OrderBy(row => row.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();
        var distinctReasons = blockingReasons.Distinct(StringComparer.Ordinal).ToArray();
        return new MaterialSupplyProjectionResult
        {
            Status = distinctReasons.Length == 0 ? "available" : "blocked",
            Slots = projectedSlots,
            Quantities = quantities,
            BlockingReasons = distinctReasons
        };
    }

    private static string SlotKey(string nodeId, int slotIndex) => nodeId + "#" + slotIndex;

    private sealed record SlotRef(string NodeId, MaterialInventorySlot Slot);
}
