using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    internal static (PickupFixture Stock, ActionOriginObservationReceipt Receipt, TrainingExecutionResult Result) WateringOwnerStock()
    {
        var f = WateringMetadataFixture();
        var reference = Guid.NewGuid().ToString("N");
        foreach (var (payload, snapshot) in new[] { (f.Receipt.InventoryPayloadBefore!, f.Stock.Before),
                     (f.Receipt.InventoryPayloadAfter!, f.Stock.After) })
        {
            var seed = JsonSerializer.Deserialize<ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(payload.Slots![0]))!;
            seed.SlotIndex = 2; seed.ItemReferenceId = reference; seed.QualifiedItemId = "(O)745"; seed.Stack = 40;
            var xml = XElement.Parse(seed.SerializedStateXml!); xml.Element("itemId")!.Value = "745";
            xml.Element("name")!.Value = "Strawberry Seeds"; xml.Element("stack")!.Value = "40"; SetXml(seed, xml);
            payload.Slots[2] = seed;
            var graph = payload.GraphState!; var original = graph.Nodes!.Single(row => row.ReferenceId == payload.Slots[0].ItemReferenceId);
            var node = JsonSerializer.Deserialize<ActionOriginInventoryGraphNode>(JsonSerializer.Serialize(original))!;
            node.ReferenceId = reference; node.QualifiedItemId = seed.QualifiedItemId; node.Stack = 40;
            var edges = graph.Edges!.Where(row => row.ParentReferenceId == original.ReferenceId).Select(row =>
                JsonSerializer.Deserialize<ActionOriginInventoryGraphEdge>(JsonSerializer.Serialize(row))!).ToArray();
            foreach (var edge in edges) { Assert.True(edge.Target!.IsNull); edge.ParentReferenceId = reference; }
            graph.Nodes = [.. graph.Nodes, node]; graph.Edges = [.. graph.Edges, .. edges];
            graph.NodeOccurrences++; graph.EdgeOccurrences += edges.Length;
            Edit(snapshot, "player", "inventory", rows =>
            {
                var row = rows[0]!.DeepClone().AsObject(); row["slot_index"] = 2; row["qualified_item_id"] = "(O)745";
                row["item_id"] = "745"; row["display_name"] = "Strawberry Seeds"; row["stack"] = 40;
                row["context_tags"] = new JsonArray("item_strawberry_seeds"); rows[2] = row;
            });
            Edit(snapshot, "player", "inventory_capacity", row =>
            { row["occupied_item_stacks"] = 3; row["empty_slots"] = 0; row["has_empty_slot"] = false; });
            Edit(snapshot, "farm", "material_inventory_graph", row =>
            {
                var seedRow = row["inventory_nodes"]![0]!["slots"]![0]!.DeepClone();
                seedRow["slot_index"] = 2; seedRow["qualified_item_id"] = "(O)745"; seedRow["item_id"] = "745";
                seedRow["display_name"] = "Strawberry Seeds"; seedRow["stack"] = 40;
                seedRow["context_tags"] = new JsonArray("item_strawberry_seeds");
                row["inventory_nodes"]![0]!["slots"]!.AsArray().Add(seedRow);
                row["quantity_rows"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { qualified_item_id = "(O)745", quality = 0,
                    available_quantity = 40, ready_output_quantity = 0, in_process_quantity = 0, restricted_quantity = 0, source_slot_count = 1 }));
                row["quantity_rows"] = new JsonArray(row["quantity_rows"]!.AsArray()
                    .OrderBy(value => value!["qualified_item_id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .Select(value => value!.DeepClone()).ToArray());
                row["default_shared_resource_policy"] = "deny_without_explicit_authorization";
                row["physical_inventory_count"] = row["inventory_nodes"]!.AsArray().Count;
                row["access_point_count"] = row["access_points"]!.AsArray().Count;
            });
        }
        f.Receipt.InventoryBefore = [.. f.Receipt.InventoryBefore, new() { QualifiedItemId = "(O)745", Quality = 0, Quantity = 40 }];
        f.Receipt.InventoryAfter = [.. f.Receipt.InventoryAfter, new() { QualifiedItemId = "(O)745", Quality = 0, Quantity = 40 }];
        Rebind(f.Stock); f.Receipt.BeforeStateHash = f.Stock.Before.StateHash;
        f.Result.BeforeStateHash = f.Stock.Before.StateHash; f.Result.ProductAfterStateHash = f.Stock.After.StateHash;
        Assert.True(f.Read().PhysicalInventoryDeltaVerified);
        return (f.Stock, f.Receipt, f.Result);
    }
}
