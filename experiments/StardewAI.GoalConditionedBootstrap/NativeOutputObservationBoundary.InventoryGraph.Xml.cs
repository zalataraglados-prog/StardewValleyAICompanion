using System.Globalization;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private sealed class InventoryGraphXmlTraversal
    {
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
        private readonly Dictionary<Guid, NativeObservedInventoryGraphNode> nodes;
        private readonly Dictionary<Guid, List<NativeObservedInventoryGraphEdge>> edges;
        private readonly HashSet<Guid> reached = new();
        private readonly HashSet<Guid> ancestors = new();
        private int rootOccurrences;
        internal int NodeOccurrences { get; private set; }
        internal int EdgeOccurrences { get; private set; }
        internal int XmlEnchantmentOccurrences { get; private set; }
        internal int ReachedCount => reached.Count;

        internal InventoryGraphXmlTraversal(Dictionary<Guid, NativeObservedInventoryGraphNode> nodes,
            List<NativeObservedInventoryGraphEdge> edges)
        {
            this.nodes = nodes;
            this.edges = edges.GroupBy(edge => GraphId(edge.ParentReferenceId)).ToDictionary(group => group.Key, group => group.ToList());
        }

        internal void VisitRoot(Guid id, XElement xml)
        {
            rootOccurrences = 0;
            Visit(id, xml, 1);
        }

        internal void VisitMembership(IReadOnlyList<string?> membership)
        {
            rootOccurrences = 0;
            foreach (var reference in membership)
            {
                CountEdge();
                if (reference is not null) Visit(GraphId(reference), null, 1);
            }
        }

        private void Visit(Guid id, XElement? xml, int depth)
        {
            Require(depth <= NativeInventoryGraphLimits.MaximumDepth &&
                ++rootOccurrences <= NativeInventoryGraphLimits.MaximumRootOccurrences &&
                ++NodeOccurrences <= NativeInventoryGraphLimits.MaximumNodeOccurrences && ancestors.Add(id),
                "inventory graph cycle, depth or repeated occurrence bound exceeded");
            reached.Add(id);
            try
            {
                var node = nodes[id];
                var outgoing = edges.GetValueOrDefault(id) ?? new();
                if (node.Kind == "enchantment")
                {
                    Require(outgoing.Count == 0, "enchantment graph node has fabricated Item edges");
                    if (xml is not null)
                    {
                        CheckXmlType(xml, node.RuntimeType, "BaseEnchantment");
                        Require(int.TryParse(ItemXmlValue(xml, "level"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) &&
                            level == node.Enchantment!.Level, "graph enchantment level differs from native XML");
                        XmlEnchantmentOccurrences++;
                    }
                    return;
                }
                var type = default(NativeInventoryPayloadType);
                Require(xml is not null && NativeInventoryPayloadTypes.TryGet(node.RuntimeType, out type),
                    "graph Item lacks a serialized inventory occurrence");
                var header = node.Header!;
                ValidateInventoryXmlIdentity(xml!, type, header.QualifiedItemId!, header.Quality!.Value, header.Stack!.Value);
                Require(type.IdentityRule == NativeInventoryPayloadIdentityRule.ObjectBigCraftable ||
                    !xml!.Elements("heldObject").Any() && !xml.Elements("lastInputItem").Any(), "graph XML has Object edges on another Item family");
                Require(type.IsTool || !xml!.Elements("attachments").Any(), "graph XML has attachments on a non-Tool");
                Require(type.HasEnchantmentGraphCollection || !xml!.Elements("enchantments").Any(), "graph XML has an unsupported Item enchantment list");
                Require(type.XmlRoot == "CombinedRing" || !xml!.Elements("combinedRings").Any(), "graph XML has combined-ring edges on another Item family");
                var used = 0;
                if (type.IdentityRule == NativeInventoryPayloadIdentityRule.ObjectBigCraftable)
                {
                    Single(node, outgoing, NativeInventoryGraphRoles.HeldObject, "heldObject", "Object", xml!, depth); used++;
                    Single(node, outgoing, NativeInventoryGraphRoles.LastInputItem, "lastInputItem", "Item", xml!, depth); used++;
                }
                if (type.IsTool)
                {
                    Array(node, outgoing, NativeInventoryGraphRoles.Attachments, "attachments", "Object", node.AttachmentCount!.Value, xml!, depth); used += node.AttachmentCount.Value;
                }
                if (type.HasEnchantmentGraphCollection)
                {
                    Enchantments(node, outgoing, xml!, depth); used += node.EnchantmentCount!.Value;
                }
                if (type.XmlRoot == "CombinedRing")
                {
                    Array(node, outgoing, NativeInventoryGraphRoles.CombinedRings, "combinedRings", "Ring", node.CombinedRingCount!.Value, xml!, depth);
                    used += node.CombinedRingCount.Value;
                }
                Require(used == outgoing.Count, "graph Item has extra or incompatible child edges");
            }
            finally { ancestors.Remove(id); }
        }

        private static void CheckXmlType(XElement element, string runtimeType, string declaredXmlType)
        {
            var expected = runtimeType[(runtimeType.LastIndexOf('.') + 1)..];
            var actual = element.Attribute(Xsi + "type")?.Value;
            Require(element.Attribute(Xsi + "nil") is null && (actual ?? declaredXmlType) == expected,
                "graph occurrence XML type differs from its native reference");
        }

        private NativeObservedInventoryGraphEdge Edge(List<NativeObservedInventoryGraphEdge> values, string role, int ordinal)
        {
            var matches = values.Where(edge => edge.Role == role && edge.Ordinal == ordinal).ToArray();
            Require(matches.Length == 1, "graph native child position is missing or repeated");
            CountEdge(); return matches[0];
        }

        private void CountEdge() => Require(++EdgeOccurrences <= NativeInventoryGraphLimits.MaximumEdgeOccurrences,
            "inventory graph repeated/null edge occurrence limit exceeded");

        private void Child(NativeObservedInventoryGraphEdge edge, XElement xml, string declaredType, int depth)
        {
            if (edge.TargetReferenceId is null)
            {
                Require(xml.Attribute(Xsi + "nil")?.Value == "true" && !xml.HasElements && string.IsNullOrWhiteSpace(xml.Value) &&
                    xml.Attributes().All(attribute => attribute.IsNamespaceDeclaration || attribute.Name == Xsi + "nil"),
                    "null graph array position is not the native nil element");
                return;
            }
            var id = GraphId(edge.TargetReferenceId);
            var node = nodes[id];
            var kind = declaredType == "BaseEnchantment" ? "enchantment" : "item";
            Require(node.Kind == kind, "graph child kind differs from native role");
            if (declaredType == "Object")
            {
                Require(NativeInventoryPayloadTypes.TryGet(node.RuntimeType, out var type) &&
                    type.IdentityRule == NativeInventoryPayloadIdentityRule.ObjectBigCraftable, "Object-only graph role contains another Item family");
            }
            if (declaredType == "Ring")
            {
                Require(NativeInventoryPayloadTypes.TryGet(node.RuntimeType, out var type) && type.XmlRoot is "Ring" or "CombinedRing",
                    "Ring-only graph role contains another Item family");
            }
            CheckXmlType(xml, node.RuntimeType, declaredType);
            Visit(id, xml, depth + 1);
        }

        private void Single(NativeObservedInventoryGraphNode node, List<NativeObservedInventoryGraphEdge> values,
            string role, string member, string declaredType, XElement xml, int depth)
        {
            var edge = Edge(values, role, 0);
            var elements = xml.Elements(member).ToArray();
            Require(edge.TargetReferenceId is null ? elements.Length == 0 : elements.Length == 1,
                "native inline graph member availability differs from XML");
            if (edge.TargetReferenceId is not null) Child(edge, elements[0], declaredType, depth);
        }

        private void Array(NativeObservedInventoryGraphNode node, List<NativeObservedInventoryGraphEdge> values,
            string role, string member, string childName, int count, XElement xml, int depth)
        {
            var wrappers = xml.Elements(member).ToArray();
            Require(wrappers.Length == 1 && wrappers[0].Attribute(Xsi + "nil") is null,
                "native graph array wrapper is unavailable");
            var children = wrappers[0].Elements().ToArray();
            Require(children.Length == count && children.All(child => child.Name == childName), "native graph array positions differ from XML");
            for (var index = 0; index < count; index++) Child(Edge(values, role, index), children[index], childName, depth);
        }

        private void Enchantments(NativeObservedInventoryGraphNode node, List<NativeObservedInventoryGraphEdge> values,
            XElement xml, int depth)
        {
            var elements = xml.Elements("enchantments").ToArray();
            var position = 0;
            for (var index = 0; index < node.EnchantmentCount!.Value; index++)
            {
                var edge = Edge(values, NativeInventoryGraphRoles.Enchantments, index);
                if (edge.TargetReferenceId is null) continue; // Native XmlElement list omits nulls, unlike XmlArray.
                Require(position < elements.Length, "native enchantment XML occurrence is missing");
                Child(edge, elements[position++], "BaseEnchantment", depth);
            }
            Require(position == elements.Length, "native enchantment XML has extra or reordered occurrences");
        }
    }
}
