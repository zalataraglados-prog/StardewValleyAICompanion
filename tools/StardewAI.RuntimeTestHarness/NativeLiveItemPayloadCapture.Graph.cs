using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewValley;
using StardewValley.Enchantments;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLiveInventoryGraphNode(object Reference, Type RuntimeType,
    NativeLiveItemTransientState? Item, NativeLiveEnchantmentTransientState? Enchantment,
    int? AttachmentCount, int? EnchantmentCount, int? CombinedRingCount);
internal sealed record NativeLiveInventoryGraphEdge(object Parent, string Role, int Ordinal, object? Target);
internal sealed record NativeLiveInventoryGraphState(IReadOnlyList<Item?> Roots,
    IReadOnlyList<NativeLiveInventoryGraphNode> Nodes, IReadOnlyList<NativeLiveInventoryGraphEdge> Edges,
    IReadOnlyList<BaseEnchantment?> ActorEnchantments, int Characters, int NodeOccurrences, int EdgeOccurrences);

internal static partial class NativeLiveItemPayloadCapture
{
    internal static NativeLiveInventoryGraphState CaptureInventoryGraph(IReadOnlyList<Item?> roots, Farmer actor,
        int maximumCharacters, int maximumNodeOccurrences, int maximumEdgeOccurrences)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(actor);
        if (maximumCharacters is < 0 or > NativeInventoryGraphLimits.MaximumCharacters ||
            maximumNodeOccurrences is < 0 or > NativeInventoryGraphLimits.MaximumNodeOccurrences ||
            maximumEdgeOccurrences is < 0 or > NativeInventoryGraphLimits.MaximumEdgeOccurrences)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters), "Native inventory graph remaining budget is invalid.");
        if (roots.Count > NativeInventoryGraphLimits.MaximumRoots)
            throw new InvalidDataException("Native inventory graph root count exceeds its bound.");
        var copiedRoots = new List<Item?>(roots.Count);
        foreach (var root in roots)
        {
            if (copiedRoots.Count >= NativeInventoryGraphLimits.MaximumRoots)
                throw new InvalidDataException("Native inventory graph root count exceeds its bound.");
            copiedRoots.Add(root);
        }
        if (copiedRoots.Count != roots.Count)
            throw new InvalidDataException("Native inventory graph roots changed during capture.");
        var capture = new InventoryGraphCapture(maximumCharacters, maximumNodeOccurrences, maximumEdgeOccurrences);
        foreach (var root in copiedRoots) new GraphCheck(capture).Visit(root, 1);

        var membership = actor.enchantments ?? throw new InvalidDataException("Native actor enchantment list is unavailable.");
        if (membership.Count > NativeInventoryGraphLimits.MaximumCollection)
            throw new InvalidDataException("Native actor enchantment list exceeds its bound.");
        var members = new List<BaseEnchantment?>(membership.Count);
        var membershipCheck = new GraphCheck(capture);
        foreach (var enchantment in membership)
        {
            if (members.Count >= NativeInventoryGraphLimits.MaximumCollection)
                throw new InvalidDataException("Native actor enchantment list exceeds its bound.");
            capture.MembershipPosition();
            members.Add(enchantment);
            membershipCheck.VisitEnchantment(enchantment, 1);
        }
        if (members.Count != membership.Count || !ReferenceEquals(actor.enchantments, membership))
            throw new InvalidDataException("Native actor enchantment list changed during capture.");
        return capture.Finish(copiedRoots.AsReadOnly(), members.AsReadOnly());
    }

    internal static NativeLiveItemPayload CaptureGraphRoot(Item item, NativeLiveInventoryGraphState graph, int maximumCharacters)
    {
        if (maximumCharacters is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(graph);
        if (!SupportedItem(item.GetType()) || !graph.Roots.Any(root => ReferenceEquals(root, item)))
            throw new InvalidDataException("Native inventory graph does not contain this physical admitted root.");
        // The caller keeps raw capture and later headers/XML in the original scope. No second
        // traversal or claim of atomicity across that interval is hidden inside this method.
        using var writer = new CappedNativeObservationWriter(maximumCharacters);
        var payload = CaptureWithWriter(item, writer, maximumCharacters);
        ValidateXml(payload.Xml, maximumCharacters);
        return payload;
    }

    private sealed class InventoryGraphCapture : IGraphObserver
    {
        private sealed class Node
        {
            internal readonly object Reference;
            internal readonly Type RuntimeType;
            internal readonly NativeLiveItemTransientState? Item;
            internal readonly NativeLiveEnchantmentTransientState? Enchantment;
            internal int? Attachments, Enchantments, Rings;
            internal Node(object reference, NativeLiveItemTransientState? item, NativeLiveEnchantmentTransientState? enchantment)
            { Reference = reference; RuntimeType = reference.GetType(); Item = item; Enchantment = enchantment; }
            internal NativeLiveInventoryGraphNode Freeze() => new(Reference, RuntimeType, Item, Enchantment, Attachments, Enchantments, Rings);
        }
        private readonly int maximumCharacters, maximumNodes, maximumEdges;
        private readonly Dictionary<object, Node> nodes = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, Dictionary<(string Role, int Ordinal), NativeLiveInventoryGraphEdge>> parentEdges = new(ReferenceEqualityComparer.Instance);
        private readonly List<NativeLiveInventoryGraphEdge> edges = new();
        private int characters, nodeOccurrences, edgeOccurrences;
        internal InventoryGraphCapture(int maximumCharacters, int maximumNodes, int maximumEdges)
        { this.maximumCharacters = maximumCharacters; this.maximumNodes = maximumNodes; this.maximumEdges = maximumEdges; }

        private void VisitOccurrence()
        {
            if (++nodeOccurrences > maximumNodes)
                throw new InvalidDataException("Native inventory graph total node occurrence budget exceeded.");
        }
        private void ChargeCharacters(int count)
        {
            if (count > maximumCharacters - characters)
                throw new InvalidDataException("Native inventory graph raw character budget exceeded.");
            characters += count;
        }
        public void Item(Item item)
        {
            VisitOccurrence();
            if (item.itemId is null || item.itemId.GetType() != typeof(Netcode.NetString) || item.itemId.Value is null)
                throw new InvalidDataException("Native ItemId is unavailable or would require mutating legacy migration.");
            if (nodes.ContainsKey(item)) return;
            // Separate per-item cap from the aggregate budget. Zero aggregate budget is NOT
            // rounded up: only a genuinely zero-character state can pass ChargeCharacters.
            var state = NativeLiveItemTransientCapture.Capture(item, 65536);
            ChargeCharacters(state.Characters);
            nodes.Add(item, new(item, state, null));
        }
        public void Enchantment(BaseEnchantment enchantment)
        {
            VisitOccurrence();
            if (nodes.ContainsKey(enchantment)) return;
            var state = NativeLiveItemTransientCapture.CaptureEnchantment(enchantment, 65536);
            ChargeCharacters(state.Characters);
            nodes.Add(enchantment, new(enchantment, null, state));
        }
        public void Collection(object parent, string role, int count)
        {
            var node = nodes[parent];
            int? prior;
            switch (role)
            {
                case NativeInventoryGraphRoles.Attachments: prior = node.Attachments; node.Attachments = count; break;
                case NativeInventoryGraphRoles.Enchantments: prior = node.Enchantments; node.Enchantments = count; break;
                case NativeInventoryGraphRoles.CombinedRings: prior = node.Rings; node.Rings = count; break;
                default: throw new InvalidDataException("Unknown native graph collection role.");
            }
            if (prior.HasValue && prior.Value != count)
                throw new InvalidDataException("Revisited native graph collection count changed.");
        }
        public void Edge(object parent, string role, int ordinal, object? target)
        {
            ChargeEdge();
            var node = nodes[parent];
            int count = role switch
            {
                NativeInventoryGraphRoles.HeldObject or NativeInventoryGraphRoles.LastInputItem => 1,
                NativeInventoryGraphRoles.Attachments => node.Attachments!.Value,
                NativeInventoryGraphRoles.Enchantments => node.Enchantments!.Value,
                NativeInventoryGraphRoles.CombinedRings => node.Rings!.Value,
                _ => throw new InvalidDataException("Unknown native graph edge role.")
            };
            if (ordinal < 0 || ordinal >= count) throw new InvalidDataException("Native graph edge index differs from its collection count.");
            if (!parentEdges.TryGetValue(parent, out var entries))
                parentEdges.Add(parent, entries = new());
            var key = (role, ordinal);
            if (entries.TryGetValue(key, out var prior))
            {
                if (!ReferenceEquals(prior.Target, target))
                    throw new InvalidDataException("Revisited native graph edge target changed.");
            }
            else
            {
                var edge = new NativeLiveInventoryGraphEdge(parent, role, ordinal, target);
                entries.Add(key, edge); edges.Add(edge);
            }
        }
        private void ChargeEdge()
        {
            if (++edgeOccurrences > maximumEdges)
                throw new InvalidDataException("Native inventory graph total edge occurrence budget exceeded.");
        }
        internal void MembershipPosition() => ChargeEdge();
        internal NativeLiveInventoryGraphState Finish(IReadOnlyList<Item?> roots, IReadOnlyList<BaseEnchantment?> membership) =>
            new(roots, nodes.Values.Select(node => node.Freeze()).ToList().AsReadOnly(), edges.AsReadOnly(), membership,
                characters, nodeOccurrences, edgeOccurrences);
    }
}
