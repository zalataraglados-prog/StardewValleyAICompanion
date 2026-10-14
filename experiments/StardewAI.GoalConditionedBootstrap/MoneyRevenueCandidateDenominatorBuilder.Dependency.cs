using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCandidateDenominatorBuilder
{
    internal sealed record RevenueDirectionDefinition(
        string Status,
        string CompletionNodeId,
        string[] Blockers,
        RevenueActionNodeDefinition[] Nodes,
        RevenueDependencyNodeDefinition[] AllNodes,
        RevenueDependencyEdgeDefinition[] Edges);

    internal sealed record RevenueDependencyNodeDefinition(
        string NodeId,
        string NodeKind,
        string OptionId);

    internal sealed record RevenueDependencyEdgeDefinition(
        string FromNodeId,
        string ToNodeId,
        string EdgeKind);

    internal sealed record RevenueActionNodeDefinition(
        string NodeId,
        string NodeKind,
        string OptionId,
        bool PolicyChoice,
        bool DeterministicSupport,
        string[] CandidateKinds,
        string[] RequiredFieldPaths);

    internal static RevenueDirectionDefinition ReadDirection(JsonElement root)
    {
        Require(root.ValueKind == JsonValueKind.Object,
            "Dependency expansion root must be an object.");
        Require(ReadRequiredString(root, "schema_version") ==
                "goal_method_dependency_expansions.v1",
            "Dependency expansion schema is not supported.");
        Require(root.TryGetProperty("directions", out var directions) &&
                directions.ValueKind == JsonValueKind.Array,
            "Dependency expansion directions are missing.");
        var matches = directions.EnumerateArray()
            .Where(direction => direction.ValueKind == JsonValueKind.Object)
            .Where(direction => string.Equals(
                ReadOptionalString(direction, "direction_id"),
                "earn_money",
                StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1,
            "Expected exactly one earn_money dependency expansion.");
        var match = matches[0];
        Require(match.TryGetProperty("nodes", out var nodes) &&
                nodes.ValueKind == JsonValueKind.Array,
            "earn_money dependency nodes are missing.");

        var nodeRows = nodes.EnumerateArray()
            .Where(node => node.ValueKind == JsonValueKind.Object)
            .ToArray();
        var allNodes = nodeRows
            .Select(node => new RevenueDependencyNodeDefinition(
                ReadRequiredString(node, "id"),
                ReadRequiredString(node, "kind"),
                ReadOptionalString(node, "option_id")))
            .ToArray();
        Require(allNodes.Select(node => node.NodeId)
                .Distinct(StringComparer.Ordinal).Count() == allNodes.Length,
            "earn_money dependency node IDs are not unique.");
        var completionNodeId = ReadRequiredString(
            match,
            "completion_node_id");
        Require(allNodes.Any(node => node.NodeId == completionNodeId),
            "earn_money completion node is not present in the node set.");
        var actionNodes = nodeRows
            .Where(node => !string.IsNullOrWhiteSpace(
                ReadOptionalString(node, "option_id")))
            .Select(ReadActionNode)
            .ToArray();
        var edges = ReadEdges(match, allNodes);
        return new RevenueDirectionDefinition(
            ReadRequiredString(match, "status"),
            completionNodeId,
            ReadStringArray(match, "blockers"),
            actionNodes,
            allNodes,
            edges);
    }

    private static RevenueDependencyEdgeDefinition[] ReadEdges(
        JsonElement direction,
        RevenueDependencyNodeDefinition[] nodes)
    {
        Require(direction.TryGetProperty("edges", out var edges) &&
                edges.ValueKind == JsonValueKind.Array,
            "earn_money dependency edges are missing.");
        var knownNodeIds = nodes.Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var result = edges.EnumerateArray()
            .Select(edge => new RevenueDependencyEdgeDefinition(
                ReadRequiredString(edge, "from"),
                ReadRequiredString(edge, "to"),
                ReadRequiredString(edge, "kind")))
            .ToArray();
        Require(result.All(edge =>
                knownNodeIds.Contains(edge.FromNodeId) &&
                knownNodeIds.Contains(edge.ToNodeId)),
            "earn_money dependency edge references an unknown node.");
        Require(result.Distinct().Count() == result.Length,
            "earn_money dependency edges are not unique.");
        return result;
    }

    private static RevenueActionNodeDefinition ReadActionNode(JsonElement node)
    {
        var nodeKind = ReadRequiredString(node, "kind");
        var policyChoice = string.Equals(
            nodeKind,
            "policy_option_transition",
            StringComparison.Ordinal);
        var deterministicSupport = string.Equals(
            nodeKind,
            "deterministic_transition",
            StringComparison.Ordinal);
        Require(policyChoice || deterministicSupport,
            "Revenue option node has unsupported kind: " + nodeKind);
        var candidateKinds = ReadStringArray(node, "candidate_kinds");
        Require(candidateKinds.Length > 0,
            "Revenue option node candidate_kinds are missing.");
        return new RevenueActionNodeDefinition(
            ReadRequiredString(node, "id"),
            nodeKind,
            ReadRequiredString(node, "option_id"),
            policyChoice,
            deterministicSupport,
            candidateKinds,
            ReadStringArray(node, "required_field_paths"));
    }

    private static string ReadRequiredString(
        JsonElement value,
        string propertyName)
    {
        var result = ReadOptionalString(value, propertyName);
        Require(!string.IsNullOrWhiteSpace(result),
            "Required dependency property is missing: " + propertyName);
        return result;
    }

    private static string ReadOptionalString(
        JsonElement value,
        string propertyName) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string[] ReadStringArray(
        JsonElement value,
        string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        var result = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        Require(result.Length == property.GetArrayLength(),
            "Dependency string array contains a non-string value: " +
            propertyName);
        return result;
    }
}
