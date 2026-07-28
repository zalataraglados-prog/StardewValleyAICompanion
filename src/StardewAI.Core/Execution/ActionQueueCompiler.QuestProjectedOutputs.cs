using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static bool ProjectedOutputContainsItem(
        string outputsJson,
        string qualifiedItemId)
    {
        return ReadProjectedQuestOutputs(outputsJson)
            .Any(output => string.Equals(
                output.QualifiedItemId,
                qualifiedItemId,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool ProjectedOutputContextTagsMatch(
        string outputsJson,
        string[] acceptableContextTagSets)
    {
        return ReadProjectedQuestOutputs(outputsJson)
            .Any(output => QuestContextTagMatcher.Matches(
                output.ContextTags,
                acceptableContextTagSets));
    }

    private static ProjectedQuestOutput[] ReadProjectedQuestOutputs(string outputsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(outputsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ProjectedQuestOutput>();
            }

            return document.RootElement.EnumerateArray()
                .Where(output => output.ValueKind == JsonValueKind.Object)
                .Select(output => new ProjectedQuestOutput(
                    ReadProjectedQuestOutputString(output, "qualified_item_id", "qualifiedItemId"),
                    ReadProjectedQuestOutputTags(output)))
                .Where(output => !string.IsNullOrWhiteSpace(output.QualifiedItemId))
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<ProjectedQuestOutput>();
        }
    }

    private static string ReadProjectedQuestOutputString(
        JsonElement output,
        string snakeName,
        string camelName)
    {
        if (!TryGetProjectedQuestOutputProperty(
                output,
                snakeName,
                camelName,
                out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        return value.GetString() ?? string.Empty;
    }

    private static string[] ReadProjectedQuestOutputTags(JsonElement output)
    {
        if (!TryGetProjectedQuestOutputProperty(
                output,
                "context_tags",
                "contextTags",
                out var tags) ||
            tags.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        return tags.EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.String)
            .Select(tag => tag.GetString() ?? string.Empty)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray();
    }

    private static bool TryGetProjectedQuestOutputProperty(
        JsonElement output,
        string snakeName,
        string camelName,
        out JsonElement value)
    {
        return output.TryGetProperty(snakeName, out value) ||
            output.TryGetProperty(camelName, out value) ||
            output.TryGetProperty(
                char.ToUpperInvariant(camelName[0]) + camelName.Substring(1),
                out value);
    }

    private sealed record ProjectedQuestOutput(
        string QualifiedItemId,
        string[] ContextTags);
}
