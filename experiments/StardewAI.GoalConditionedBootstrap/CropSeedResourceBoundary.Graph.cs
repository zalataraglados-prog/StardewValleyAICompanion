using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static (JsonObject Graph, JsonObject SourceSlot, JsonArray SourceAccesses) ProjectGraph(SnapshotEnvelope before, SnapshotEnvelope after, MaterialInventoryGraph graph,
        MaterialTransferIntent intent, MaterialTransferProjection physical)
    {
        var envelope = Field(before, "farm", "material_inventory_graph")!;
        var expected = Value(envelope).AsObject();
        var nodes = Array(expected["inventory_nodes"], "inventory_nodes");
        var sourceAccesses = Array(expected["access_points"], "access points").DeepClone().AsArray();
        var source = Slot(Array(Unique(nodes, "node_id", intent.SourceNodeId)["slots"], "source slots"), intent.SourceSlotIndex);
        foreach (var group in physical.NativeSlotTransitions!.GroupBy(row => row.NodeId))
        {
            var node = Unique(nodes, "node_id", group.Key);
            var original = Array(node["slots"], "original slots");
            var slots = new JsonArray();
            foreach (var row in group.Where(row => row.AfterSlotIndex.HasValue).OrderBy(row => row.AfterSlotIndex))
            {
                var retained = (row.BeforeSlotIndex.HasValue ? Slot(original, row.BeforeSlotIndex.Value) : source).DeepClone().AsObject();
                retained["slot_index"] = row.AfterSlotIndex!.Value;
                retained["stack"] = row.StackAfter;
                slots.Add(retained);
            }
            node["slots"] = slots;
            if (node["inventory_kind"]?.GetValue<string>() != "chest") continue;
            foreach (var access in Array(expected["access_points"], "access points").OfType<JsonObject>()
                         .Where(row => row["node_id"]?.GetValue<string>() == group.Key))
                ProjectChestOccupancy(access, original.Count, slots.Count, Int(node, "capacity"));
        }
        var quantities = Array(expected["quantity_rows"], "quantity rows").OfType<JsonObject>().Where(row =>
            row["qualified_item_id"]?.GetValue<string>() == intent.QualifiedItemId && Int(row, "quality") == intent.Quality).ToArray();
        Require(quantities.Length == 1, "Owned seed transfer aggregate identity is not unique.");
        var nativeCount = VerifySourceQuantity(quantities[0], graph, intent.QualifiedItemId, intent.Quality);
        quantities[0]["source_slot_count"] = nativeCount + physical.NativeSlotTransitions!.Where(row =>
                row.QualifiedItemId == intent.QualifiedItemId && row.Quality == intent.Quality)
            .Sum(row => (row.AfterSlotIndex.HasValue ? 1 : 0) - (row.BeforeSlotIndex.HasValue ? 1 : 0));
        EqualField(envelope, Field(after, "farm", "material_inventory_graph"), "native material graph");
        return (expected, source, sourceAccesses);
    }

    private static int VerifySourceQuantity(JsonObject quantity, MaterialInventoryGraph graph, string itemId, int quality)
    {
        var native = graph.InventoryNodes.SelectMany(node => node.Slots.Select(slot => (Node: node, Slot: slot)))
            .Where(row => row.Slot.Stack > 0 && row.Slot.QualifiedItemId == itemId && row.Slot.Quality == quality).ToArray();
        Require(Int(quantity, "source_slot_count") == native.Length, "Owned seed transfer source aggregate is inconsistent.");
        foreach (var (field, state) in new[] { ("available_quantity", "available"), ("ready_output_quantity", "ready_output"),
                     ("in_process_quantity", "in_process") })
            Require(Int(quantity, field) == native.Where(row => row.Node.ActorUseAuthorized && row.Node.SupplyState == state)
                    .Sum(row => (long)row.Slot.Stack), "Owned seed transfer source aggregate amount is inconsistent: " + field);
        Require(Int(quantity, "restricted_quantity") == native.Where(row => !row.Node.ActorUseAuthorized).Sum(row => (long)row.Slot.Stack),
            "Owned seed transfer source restricted aggregate is inconsistent.");
        return native.Length;
    }

    private static void ProjectChestOccupancy(JsonObject access, int beforeCount, int afterCount, int capacity)
    {
        Require(Int(access, "occupied_slot_count") == beforeCount && Int(access, "free_slot_count") == Math.Max(0, capacity - beforeCount),
            "Owned seed transfer source chest occupancy is inconsistent.");
        access["occupied_slot_count"] = afterCount;
        access["free_slot_count"] = Math.Max(0, capacity - afterCount);
        var blocked = Array(access["relocation_blocking_reasons"], "relocation reasons").Count > 0;
        string Status(int count) => blocked ? "blocked" : count == 0 ? "native_remove_available_empty" : "native_shove_available_nonempty";
        string Hit(int count) => blocked ? "blocked_before_native_hit" : count == 0
            ? "empty_heavy_tool_remove_and_drop_chest_item" : "nonempty_heavy_tool_second_hit_or_hold_shove";
        Require(access["relocation_status"]?.GetValue<string>() == Status(beforeCount) &&
                access["native_hit_behavior"]?.GetValue<string>() == Hit(beforeCount),
            "Owned seed transfer source chest relocation echo is inconsistent.");
        access["relocation_status"] = Status(afterCount);
        access["native_hit_behavior"] = Hit(afterCount);
    }
}
