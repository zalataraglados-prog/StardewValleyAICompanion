using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCandidateDenominatorBuilder
{
    internal sealed record RevenueDirectionDefinition(
        string Status,
        string[] Blockers,
        RevenueActionNodeDefinition[] Nodes);

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

        var actionNodes = nodes.EnumerateArray()
            .Where(node => node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("option_id", out var optionId) &&
                optionId.ValueKind == JsonValueKind.String)
            .Select(ReadActionNode)
            .ToArray();
        Require(actionNodes.Select(node => node.NodeId)
                .Distinct(StringComparer.Ordinal).Count() == actionNodes.Length,
            "earn_money dependency node IDs are not unique.");
        return new RevenueDirectionDefinition(
            ReadRequiredString(match, "status"),
            ReadStringArray(match, "blockers"),
            actionNodes);
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
