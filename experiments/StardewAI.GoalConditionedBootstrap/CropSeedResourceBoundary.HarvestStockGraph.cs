using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static readonly HashSet<string> HarvestMaterialSlotFields = JsonSerializer.SerializeToNode(new MaterialInventorySlot())!
        .AsObject().Select(row => row.Key).ToHashSet(StringComparer.Ordinal);

    private static void VerifyHarvestStockGraph(SnapshotEnvelope before, SnapshotEnvelope after, MaterialInventoryGraph first,
        MaterialInventoryGraph next, MaterialInventoryNode priorPlayer, MaterialInventoryNode player, IReadOnlyList<PhysicalStockDelta> changes)
    {
        var envelope = Field(before, "farm", "material_inventory_graph")!;
        var actual = Field(after, "farm", "material_inventory_graph")!;
        var graph = Value(envelope).AsObject();
        var rawPlayer = Unique(Array(graph["inventory_nodes"], "harvest graph nodes"), "node_id", priorPlayer.NodeId);
        var prior = Rows(rawPlayer["slots"]!);
        var current = Rows(Unique(Array(Value(actual)["inventory_nodes"], "harvest after graph nodes"), "node_id", player.NodeId)["slots"]!);
        var raw = Rows(Value(Field(after, "player", "inventory")!));
        var changed = changes.ToDictionary(row => row.SlotIndex);
        var projected = new JsonArray();
        foreach (var (index, slot) in current.OrderBy(row => row.Key))
        {
            Require(raw.TryGetValue(index, out var item), "Harvest material slot has no original raw inventory row.");
            foreach (var name in new[] { "item_id", "qualified_item_id", "display_name", "runtime_type", "context_tags", "stack", "maximum_stack_size", "quality", "sale_price" })
                Require(item!.ContainsKey(name) && item[name] is not null && JsonNode.DeepEquals(item[name], slot[name]),
                    "Harvest material slot differs from its observed raw item: " + name);
            JsonObject expected;
            if (prior.TryGetValue(index, out var priorSlot))
            {
                expected = priorSlot.DeepClone().AsObject();
                if (changed.TryGetValue(index, out var delta)) expected["stack"] = delta.StackAfter;
            }
            else
            {
                Require(changed.TryGetValue(index, out var delta) && delta.StackBefore == 0 &&
                        slot.Select(row => row.Key).ToHashSet(StringComparer.Ordinal).SetEquals(HarvestMaterialSlotFields),
                    "Harvest new material slot lacks its physical delta or has unknown metadata.");
                expected = slot.DeepClone().AsObject();
            }
            projected.Add(expected);
        }
        Require(prior.Keys.All(current.ContainsKey), "Harvest removed an existing material slot.");
        rawPlayer["slots"] = projected;
        ProjectStockQuantities(graph, first, next, changes);
        EqualField(envelope, actual, "harvest material graph, foreign custody or unknown metadata");
    }
}
