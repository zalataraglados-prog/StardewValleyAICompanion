using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedEnchantmentState(bool Applied, string? DisplayName, int Level);
public sealed record NativeObservedInventoryGraphNode(string ReferenceId, string RuntimeType, string Kind,
    NativeObservedInventoryPayloadSlot? Header, NativeObservedItemTransientState? Item,
    NativeObservedEnchantmentState? Enchantment, int? AttachmentCount, int? EnchantmentCount, int? CombinedRingCount);
public sealed record NativeObservedInventoryGraphEdge(string ParentReferenceId, string Role, int Ordinal, string? TargetReferenceId);
public sealed record NativeObservedInventoryGraphEndpoint(string ActorReferenceId,
    IReadOnlyList<NativeObservedInventoryGraphNode> Nodes, IReadOnlyList<NativeObservedInventoryGraphEdge> Edges,
    IReadOnlyList<string?> ActorEnchantments, int NodeOccurrences, int EdgeOccurrences, int XmlEnchantmentOccurrences);
public sealed record NativeInventoryGraphSummary(string ScopeId, string CaptureSessionId,
    NativeInventoryPayloadSummary Payload, NativeInventoryTransientSummary Transients,
    NativeObservedInventoryGraphEndpoint Before, NativeObservedInventoryGraphEndpoint After)
{
    public bool InventoryGraphSubsetVerified => true;
    public bool FullTransientStateVerified => false;
    public bool NativeOutcomeVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    public static NativeInventoryGraphSummary ReadInventoryGraph(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick, string locationId, string actorId)
    {
        Require(request.ActionOriginInventoryGraph && request.ActionOriginInventoryTransients,
            "native inventory graph was not explicitly requested with its original transient scope");
        var characters = 0;
        var scope = ReadInventoryTransientScope(request, receipt, earliestTick, latestTick, locationId, actorId,
            requireObjectState: true, ref characters, retainXml: true);
        var nodeOccurrences = 0; var edgeOccurrences = 0;
        var before = ReadInventoryGraphEndpoint(receipt.InventoryPayloadBefore!.GraphState, scope.Payload.Before,
            scope.Transients.Before, scope.BeforeXml!, actorId, scope.References, ref characters, ref nodeOccurrences, ref edgeOccurrences);
        var after = ReadInventoryGraphEndpoint(receipt.InventoryPayloadAfter!.GraphState, scope.Payload.After,
            scope.Transients.After, scope.AfterXml!, actorId, scope.References, ref characters, ref nodeOccurrences, ref edgeOccurrences);
        return new(scope.Transients.ScopeId, scope.Transients.CaptureSessionId, scope.Payload, scope.Transients, before, after);
    }

    private static NativeObservedInventoryGraphEndpoint ReadInventoryGraphEndpoint(ActionOriginInventoryGraphState? wire,
        NativeObservedInventoryPayloadState payload, NativeObservedInventoryTransientEndpoint transients,
        Dictionary<Guid, XElement> rootXml, string actorId, Dictionary<Guid, string> references,
        ref int characters, ref int nodeOccurrences, ref int edgeOccurrences)
    {
        Require(wire is not null && wire.SchemaVersion == "native_inventory_graph.v1" &&
            wire.Nodes is { Length: <= NativeInventoryGraphLimits.MaximumNodeOccurrences } &&
            wire.Edges is { Length: <= NativeInventoryGraphLimits.MaximumEdgeOccurrences } &&
            wire.ActorEnchantments is { Length: <= NativeInventoryGraphLimits.MaximumCollection } &&
            wire.NodeOccurrences is >= 0 && wire.EdgeOccurrences is >= 0 &&
            wire.NodeOccurrences <= NativeInventoryGraphLimits.MaximumNodeOccurrences - nodeOccurrences &&
            wire.EdgeOccurrences <= NativeInventoryGraphLimits.MaximumEdgeOccurrences - edgeOccurrences &&
            wire.Nodes.Length <= wire.NodeOccurrences && wire.Edges.Length + wire.ActorEnchantments.Length <= wire.EdgeOccurrences,
            "inventory graph schema, availability or aggregate occurrence budget is incomplete");
        BindTransientReference(wire!.ActorReferenceId, "StardewValley.Farmer", references);
        Require(GraphId(wire.ActorReferenceId) == GraphId(transients.Selection.ActorReferenceId), "graph actor reference differs from original selection");
        var roots = payload.Slots.Where(slot => !slot.IsEmpty).ToDictionary(slot => GraphId(slot.ItemReferenceId));
        var nodes = new Dictionary<Guid, NativeObservedInventoryGraphNode>();
        foreach (var raw in wire.Nodes!)
        {
            Require(raw is not null, "inventory graph node is unavailable");
            var node = ReadInventoryGraphNode(raw!, roots, transients, actorId, wire.ActorReferenceId!, references, ref characters);
            Require(nodes.TryAdd(GraphId(node.ReferenceId), node), "inventory graph contains duplicate physical nodes");
        }
        Require(roots.Keys.All(nodes.ContainsKey), "inventory graph omits an original inventory root");
        var edges = new List<NativeObservedInventoryGraphEdge>();
        var edgeKeys = new HashSet<(Guid Parent, string Role, int Ordinal)>();
        foreach (var raw in wire.Edges!)
        {
            Require(raw is not null && raw.Ordinal is >= 0 && raw.Role is NativeInventoryGraphRoles.HeldObject or
                NativeInventoryGraphRoles.LastInputItem or NativeInventoryGraphRoles.Attachments or
                NativeInventoryGraphRoles.Enchantments or NativeInventoryGraphRoles.CombinedRings,
                "inventory graph edge role or ordinal is unavailable");
            var parent = GraphId(raw!.ParentReferenceId);
            Require(nodes.ContainsKey(parent) && edgeKeys.Add((parent, raw.Role!, raw.Ordinal!.Value)),
                "inventory graph edge parent is unavailable or its ordinal repeats");
            var target = ReadGraphReference(raw.Target, nodes, enchantmentOnly: false);
            edges.Add(new(nodes[parent].ReferenceId, raw.Role!, raw.Ordinal!.Value, target));
        }
        var membership = wire.ActorEnchantments!.Select(reference => ReadGraphReference(reference, nodes, enchantmentOnly: true)).ToArray();
        var traversal = new InventoryGraphXmlTraversal(nodes, edges);
        foreach (var root in roots) traversal.VisitRoot(root.Key, rootXml[root.Key]);
        traversal.VisitMembership(membership);
        Require(traversal.ReachedCount == nodes.Count && traversal.NodeOccurrences == wire.NodeOccurrences &&
            traversal.EdgeOccurrences == wire.EdgeOccurrences, "inventory graph reachability or native occurrence counts differ");
        nodeOccurrences += traversal.NodeOccurrences; edgeOccurrences += traversal.EdgeOccurrences;
        return new(wire.ActorReferenceId!, nodes.Values.ToList().AsReadOnly(), edges.AsReadOnly(),
            Array.AsReadOnly(membership), traversal.NodeOccurrences, traversal.EdgeOccurrences, traversal.XmlEnchantmentOccurrences);
    }

    private static Guid GraphId(string? value)
    {
        Require(value is { Length: 32 } && Guid.TryParseExact(value, "N", out _), "inventory graph physical reference is unavailable");
        return Guid.ParseExact(value!, "N");
    }

    private static string? ReadGraphReference(ActionOriginGraphReference? reference,
        Dictionary<Guid, NativeObservedInventoryGraphNode> nodes, bool enchantmentOnly)
    {
        Require(reference?.IsNull is not null, "inventory graph reference availability is missing");
        if (reference!.IsNull!.Value)
        {
            Require(reference.ReferenceId is null, "null graph reference contains a fabricated identity");
            return null;
        }
        Require(nodes.TryGetValue(GraphId(reference.ReferenceId), out var node) && (!enchantmentOnly || node.Kind == "enchantment"),
            "inventory graph reference targets an unknown or incompatible node");
        return node!.ReferenceId;
    }
}
