using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static bool ProjectedOutputContainsItem(
        EventCandidate candidate,
        string parameterName,
        string requiredItemId)
    {
        var qualifiedRequired = QualifyQuestObjectId(requiredItemId);
        return ReadProjectedOutputs(candidate, parameterName)
            .Any(output => string.Equals(
                output.QualifiedItemId,
                qualifiedRequired,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool ProjectedOutputContextTagsMatch(
        EventCandidate candidate,
        string parameterName,
        string[] acceptableContextTagSets)
    {
        return ReadProjectedOutputs(candidate, parameterName)
            .Any(output => QuestContextTagMatcher.Matches(
                output.ContextTags,
                acceptableContextTagSets));
    }

    private static ProjectedQuestOutput[] ReadProjectedOutputs(
        EventCandidate candidate,
        string parameterName)
    {
        try
        {
            using var document = JsonDocument.Parse(
                ReadParameter(candidate.Parameters, parameterName));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ProjectedQuestOutput>();
            }

            return document.RootElement.EnumerateArray()
                .Where(output => output.ValueKind == JsonValueKind.Object)
                .Select(output => new ProjectedQuestOutput(
                    ReadProjectedOutputString(output, "qualified_item_id", "qualifiedItemId"),
                    ReadProjectedOutputStringArray(output, "context_tags", "contextTags")))
                .Where(output => !string.IsNullOrWhiteSpace(output.QualifiedItemId))
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<ProjectedQuestOutput>();
        }
    }

    private static string ReadProjectedOutputString(
        JsonElement output,
        string snakeName,
        string camelName)
    {
        return TryGetProjectedOutputProperty(output, snakeName, camelName, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static string[] ReadProjectedOutputStringArray(
        JsonElement output,
        string snakeName,
        string camelName)
    {
        return TryGetProjectedOutputProperty(output, snakeName, camelName, out var value) &&
            value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                    .Where(tag => tag.ValueKind == JsonValueKind.String)
                    .Select(tag => tag.GetString() ?? string.Empty)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray()
                : Array.Empty<string>();
    }

    private static bool TryGetProjectedOutputProperty(
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
