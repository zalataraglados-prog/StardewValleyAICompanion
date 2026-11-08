using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private int remainingGraphNodes = NativeInventoryGraphLimits.MaximumNodeOccurrences;
        private int remainingGraphEdges = NativeInventoryGraphLimits.MaximumEdgeOccurrences;

        private NativeLiveInventoryGraphState CaptureInventoryGraphState()
        {
            var roots = farmer.Items.ToArray();
            var graph = NativeLiveItemPayloadCapture.CaptureInventoryGraph(roots, farmer,
                remainingPayloadCharacters, remainingGraphNodes, remainingGraphEdges);
            remainingPayloadCharacters -= graph.Characters;
            remainingGraphNodes -= graph.NodeOccurrences; remainingGraphEdges -= graph.EdgeOccurrences;
            if (farmer.Items.Count != roots.Length || roots.Where((item, index) => !ReferenceEquals(item, farmer.Items[index])).Any())
                throw new InvalidDataException("Inventory roots changed during raw graph capture.");
            return graph;
        }

        private ActionOriginInventoryGraphState ReadInventoryGraphPayload(NativeLiveInventoryGraphState graph,
            IReadOnlyList<ActionOriginInventoryPayloadSlot> slots)
        {
            var rootIds = slots.Where(slot => slot.IsEmpty == false).ToDictionary(slot => slot.ItemReferenceId!, StringComparer.Ordinal);
            var nodes = new List<ActionOriginInventoryGraphNode>();
            foreach (var node in graph.Nodes)
            {
                var id = ReferenceIds.For(node.Reference);
                var wire = new ActionOriginInventoryGraphNode
                {
                    ReferenceId = id, RuntimeType = node.RuntimeType.FullName,
                    AttachmentCount = node.AttachmentCount, EnchantmentCount = node.EnchantmentCount,
                    CombinedRingCount = node.CombinedRingCount
                };
                if (node.Reference is Item item && node.Item is { } raw)
                {
                    wire.Kind = "item";
                    if (rootIds.TryGetValue(id, out var root))
                    {
                        wire.QualifiedItemId = root.QualifiedItemId; wire.Quality = root.Quality;
                        wire.Stack = root.Stack; wire.IsTool = root.IsTool;
                    }
                    else
                    {
                        // All raw values were already captured before any virtual header/XML read.
                        wire.QualifiedItemId = item.QualifiedItemId; wire.Quality = item.Quality;
                        wire.Stack = item.Stack; wire.IsTool = item is Tool;
                        wire.ItemState = ReadItemTransientPayload(raw);
                    }
                }
                else if (node.Enchantment is { } enchantment)
                {
                    wire.Kind = "enchantment";
                    wire.EnchantmentState = new()
                    {
                        Applied = enchantment.Applied, DisplayName = ObservedText(enchantment.DisplayName), Level = enchantment.Level
                    };
                }
                else throw new InvalidDataException("Graph node lacks its typed native raw state.");
                nodes.Add(wire);
            }
            return new()
            {
                SchemaVersion = "native_inventory_graph.v1", ActorReferenceId = ReferenceIds.For(farmer),
                Nodes = nodes.ToArray(), Edges = graph.Edges.Select(edge => new ActionOriginInventoryGraphEdge
                {
                    ParentReferenceId = ReferenceIds.For(edge.Parent), Role = edge.Role, Ordinal = edge.Ordinal,
                    Target = GraphReference(edge.Target)
                }).ToArray(),
                ActorEnchantments = graph.ActorEnchantments.Select(value => GraphReference(value)).ToArray(),
                NodeOccurrences = graph.NodeOccurrences, EdgeOccurrences = graph.EdgeOccurrences
            };
        }

        private static ActionOriginGraphReference GraphReference(object? value) => value is null
            ? new() { IsNull = true } : new() { IsNull = false, ReferenceId = ReferenceIds.For(value) };
    }
}
