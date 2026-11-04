using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution;

public sealed partial class MaterialTransferProjector
{
    private static MaterialTransferNativeSlotTransition[] ProjectNativeSlots(MaterialInventoryNode source,
        MaterialInventoryNode destination, MaterialTransferIntent intent, MaterialTransferSlotChange[] changes)
    {
        var transitions = new List<MaterialTransferNativeSlotTransition>();
        var sourceIndex = 0;
        foreach (var slot in source.Slots.OrderBy(slot => slot.SlotIndex))
        {
            var remaining = slot.Stack - (slot.SlotIndex == intent.SourceSlotIndex ? intent.Quantity : 0);
            int? afterIndex = remaining == 0 ? null : source.InventoryKind == "chest" ? sourceIndex++ : slot.SlotIndex;
            transitions.Add(Transition(source.NodeId, slot, slot.SlotIndex, afterIndex, remaining));
        }
        var destinationIndex = 0;
        foreach (var slot in destination.Slots.OrderBy(slot => slot.SlotIndex))
        {
            var afterIndex = destination.InventoryKind == "chest" ? destinationIndex++ : slot.SlotIndex;
            var change = changes.SingleOrDefault(value => value.SlotIndex == afterIndex);
            transitions.Add(Transition(destination.NodeId, slot, slot.SlotIndex, afterIndex, change?.StackAfter ?? slot.Stack));
        }
        var moved = source.Slots.Single(slot => slot.SlotIndex == intent.SourceSlotIndex);
        foreach (var change in changes.Where(value => value.StackBefore == 0))
        {
            var row = Transition(destination.NodeId, moved, null, change.SlotIndex, change.StackAfter);
            row.StackBefore = 0;
            transitions.Add(row);
        }
        return transitions.ToArray();
    }

    private static MaterialTransferNativeSlotTransition Transition(string nodeId, MaterialInventorySlot slot,
        int? beforeIndex, int? afterIndex, int afterStack) => new()
    {
        NodeId = nodeId, BeforeSlotIndex = beforeIndex, AfterSlotIndex = afterIndex,
        QualifiedItemId = slot.QualifiedItemId, Quality = slot.Quality, RuntimeType = slot.RuntimeType,
        StackBefore = slot.Stack, StackAfter = afterStack
    };

    public static string[] VerifyNativeSlots(MaterialInventoryGraph before, MaterialInventoryGraph after,
        MaterialTransferProjection projection) => VerifySlots(before, after, projection, beforeImage: false);

    public static string[] VerifyNativeBeforeSlots(MaterialInventoryGraph retained, MaterialInventoryGraph observed,
        MaterialTransferProjection projection) => VerifySlots(retained, observed, projection, beforeImage: true);

    private static string[] VerifySlots(MaterialInventoryGraph before, MaterialInventoryGraph after,
        MaterialTransferProjection projection, bool beforeImage)
    {
        if (after.SchemaVersion != "material_inventory_graph.v1" || after.Status != "available" ||
            after.PlayerId != before.PlayerId || projection.Status != "projected" ||
            projection.NativeSlotTransitions is not { Length: > 0 } transitions)
            return new[] { "material_transfer_native_slot_lineage_unavailable" };
        if (before.InventoryNodes is null || after.InventoryNodes is null ||
            before.InventoryNodes.Any(node => node is null) || after.InventoryNodes.Any(node => node is null) ||
            transitions.Any(row => row is null) || transitions.Select(row => row.NodeId).Distinct(StringComparer.Ordinal).Count() != 2)
            return new[] { "material_transfer_native_slot_lineage_unavailable" };
        var reasons = new List<string>();
        foreach (var group in transitions.GroupBy(row => row.NodeId, StringComparer.Ordinal))
        {
            var original = before.InventoryNodes.Where(node => node.NodeId == group.Key).ToArray();
            var current = after.InventoryNodes.Where(node => node.NodeId == group.Key).ToArray();
            if (original.Length != 1 || current.Length != 1 || current[0].SupplyState != "available" ||
                !current[0].ActorUseAuthorized || original[0].InventoryKind != current[0].InventoryKind ||
                original[0].OwnerPlayerId != current[0].OwnerPlayerId)
            {
                reasons.Add("material_transfer_native_node_identity_drifted");
                continue;
            }
            var expected = group.Where(row => beforeImage ? row.BeforeSlotIndex.HasValue : row.AfterSlotIndex.HasValue).ToArray();
            var slots = current[0].Slots;
            if (slots is null || slots.Length != expected.Length ||
                slots.Any(slot => slot is null) ||
                expected.Any(row => slots.Count(slot => slot.SlotIndex == (beforeImage ? row.BeforeSlotIndex : row.AfterSlotIndex) &&
                    slot.QualifiedItemId == row.QualifiedItemId && slot.Quality == row.Quality &&
                    slot.RuntimeType == row.RuntimeType && slot.Stack == (beforeImage ? row.StackBefore : row.StackAfter)) != 1))
                reasons.Add("material_transfer_native_slot_transition_mismatch");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
