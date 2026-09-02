using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed record SelectedQueueCandidateLock(
    int QueueIndex,
    string CandidateId,
    string OptionId,
    JsonObject RankedCandidate,
    JsonObject? ObjectiveContinuation);

public sealed class SelectedQueueDecisionLease
{
    private readonly IReadOnlyDictionary<int, SelectedQueueCandidateLock> byIndex;

    private SelectedQueueDecisionLease(SelectedQueueCandidateLock[] candidates)
    {
        Candidates = candidates;
        byIndex = candidates.ToDictionary(candidate => candidate.QueueIndex);
    }

    public IReadOnlyList<SelectedQueueCandidateLock> Candidates { get; }

    public SelectedQueueCandidateLock CandidateAt(int queueIndex)
    {
        return byIndex.TryGetValue(queueIndex, out var candidate)
            ? candidate
            : throw new InvalidOperationException(
                "selected queue decision is missing candidate index " + queueIndex);
    }

    public static SelectedQueueDecisionLease Load(
        string compiledQueuePath,
        string rankingPath)
    {
        if (string.IsNullOrWhiteSpace(compiledQueuePath) ||
            string.IsNullOrWhiteSpace(rankingPath))
        {
            throw new InvalidOperationException(
                "selected queue decision artifact paths are required");
        }

        var queue = JsonNode.Parse(File.ReadAllText(compiledQueuePath))?.AsObject()
            ?? throw new InvalidOperationException(
                "selected queue decision compiled queue is invalid");
        var ranking = JsonNode.Parse(File.ReadAllText(rankingPath))?.AsObject()
            ?? throw new InvalidOperationException(
                "selected queue decision ranking is invalid");
        return Create(queue, ranking);
    }

    public static SelectedQueueDecisionLease Create(
        JsonObject compiledQueue,
        JsonObject ranking)
    {
        var rankedCandidates = (ranking["ranked_event_candidates"] as JsonArray)?
            .Select(node => node as JsonObject)
            .Where(candidate => candidate is not null)
            .Cast<JsonObject>()
            .ToArray() ?? Array.Empty<JsonObject>();
        var rankedById = rankedCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(
                ReadString(candidate, "candidate_id")))
            .GroupBy(candidate => ReadString(candidate, "candidate_id"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var selected = ((compiledQueue["items"] as JsonArray)?
                .Select(node => node as JsonObject)
                .Where(item => item is not null)
                .Cast<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            .Select(item => new
            {
                Item = item,
                QueueIndex = QueueReplanFilter.ReadAcceptedCandidateIndex(item),
                CandidateId = EffectiveDecisionArtifactTracker.ReadRawQueueItemCandidateId(item)
            })
            .Where(row => row.QueueIndex >= 0 && !string.IsNullOrWhiteSpace(row.CandidateId))
            .GroupBy(row => row.QueueIndex)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var candidateIds = group
                    .Select(row => row.CandidateId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (candidateIds.Length != 1)
                {
                    throw new InvalidOperationException(
                        "selected queue index maps to multiple high-level candidates");
                }

                var candidateId = candidateIds[0];
                if (!rankedById.TryGetValue(candidateId, out var matches) ||
                    matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        "selected queue candidate is absent or ambiguous in original ranking: " +
                        candidateId);
                }

                var firstItem = group.First().Item;
                return new SelectedQueueCandidateLock(
                    group.Key,
                    candidateId,
                    ReadString(matches[0], "option_id"),
                    Clone(matches[0]),
                    CloneNullable(QueueReplanFilter.ReadObjectiveContinuation(firstItem)));
            })
            .ToArray();

        if (selected.Length == 0)
        {
            throw new InvalidOperationException(
                "selected queue decision contains no accepted high-level candidates");
        }

        for (var index = 0; index < selected.Length; index++)
        {
            if (selected[index].QueueIndex != index)
            {
                throw new InvalidOperationException(
                    "selected queue candidate order is not contiguous");
            }
        }

        return new SelectedQueueDecisionLease(selected);
    }

    private static JsonObject Clone(JsonObject value) =>
        JsonNode.Parse(value.ToJsonString())!.AsObject();

    private static JsonObject? CloneNullable(JsonObject? value) =>
        value is null ? null : Clone(value);

    private static string ReadString(JsonObject value, string propertyName)
    {
        return value[propertyName] is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var result)
                ? result
                : string.Empty;
    }
}

public static class SelectedQueueCandidateMatcher
{
    public static JsonArray FilterMaterializedCandidates(
        JsonArray materializedCandidates,
        SelectedQueueCandidateLock selected)
    {
        if (selected.ObjectiveContinuation is not null)
        {
            return QueueReplanFilter.FilterRankedCandidates(
                materializedCandidates,
                selected.ObjectiveContinuation);
        }

        var exact = materializedCandidates
            .Select(node => node as JsonObject)
            .Where(candidate => candidate is not null &&
                string.Equals(
                    ReadString(candidate, "candidate_id"),
                    selected.CandidateId,
                    StringComparison.Ordinal))
            .Select(candidate => JsonNode.Parse(candidate!.ToJsonString()))
            .ToArray();
        return new JsonArray(exact);
    }

    private static string ReadString(JsonObject? value, string propertyName)
    {
        return value?[propertyName] is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var result)
                ? result
                : string.Empty;
    }
}
