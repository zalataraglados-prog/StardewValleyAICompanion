using System.Security.Cryptography;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record GoalCriterion(
    string Id,
    int Points,
    string[] StatePaths,
    string Operation,
    string Target);

public sealed record GoalKnowledge(
    string SchemaVersion,
    string GoalId,
    int TargetScore,
    int CriterionCount,
    string Sha256,
    GoalCriterion[] Criteria);

public static class KnowledgeIndex
{
    public static GoalKnowledge Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var schema = RequiredString(root, "schema_version");
        var issues = root.GetProperty("issues");
        if (issues.GetArrayLength() != 0)
            throw new InvalidDataException("Goal dependency index has blocking issues.");

        var summary = root.GetProperty("summary");
        var goal = root.GetProperty("grandpa_goal");
        var targetScore = goal.GetProperty("targetScore").GetInt32();
        var expectedCount = summary.GetProperty("grandpaCriterionCount").GetInt32();
        var expectedMaximum = summary.GetProperty("grandpaMaximumScore").GetInt32();
        var criteria = goal.GetProperty("criteria").EnumerateArray()
            .Select(value => new GoalCriterion(
                RequiredString(value, "id"),
                value.GetProperty("points").GetInt32(),
                value.GetProperty("statePaths").EnumerateArray()
                    .Select(item => item.GetString() ?? string.Empty).ToArray(),
                RequiredString(value, "operation"),
                RequiredString(value, "target")))
            .ToArray();

        if (targetScore != 21 || expectedMaximum != 21)
            throw new InvalidDataException("The isolated bootstrap only accepts the authoritative 21-point target.");
        if (criteria.Length != expectedCount || criteria.Sum(value => value.Points) != targetScore)
            throw new InvalidDataException("Grandpa criteria do not reconcile to the declared maximum score.");
        if (criteria.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != criteria.Length)
            throw new InvalidDataException("Grandpa criteria contain duplicate IDs.");

        return new GoalKnowledge(
            schema,
            RequiredString(goal, "goalId"),
            targetScore,
            criteria.Length,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant(),
            criteria);
    }

    private static string RequiredString(JsonElement value, string name)
    {
        var result = value.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException($"Knowledge field '{name}' is empty.")
            : result;
    }
}
