using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

// Joint consumption keeps the original choice; fresh physical endpoints still come from the old planner.
public static class CropTeacherSharedCandidateSelection
{
    public static JsonArray FilterMaterializedCandidates(JsonArray materialized, SelectedQueueCandidateLock selected)
    {
        var matches = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(materialized, selected);
        if (matches.Count > 0 || selected.ObjectiveContinuation is not null ||
            Text(selected.RankedCandidate, "kind") != "route_connector_tile" ||
            !selected.OptionId.StartsWith("quest.", StringComparison.Ordinal)) return matches;
        var questId = Parameter(selected.RankedCandidate, "quest_candidate_id");
        if (string.IsNullOrWhiteSpace(questId)) return matches;
        // Legacy quest routes need no typed execution cursor. Reuse the canonical quest identity matcher,
        // without stamping a made-up continuation into the actual queue or claiming the parent quest done.
        return QueueReplanFilter.FilterRankedCandidates(materialized, new JsonObject
        {
            ["kind"] = "quest", ["option_id"] = selected.OptionId, ["quest_candidate_id"] = questId
        });
    }

    public static SelectedQueueCandidateLock WithAcceptedContinuation(SelectedQueueCandidateLock selected,
        IReadOnlyDictionary<string, JsonObject> continuations) =>
        continuations.TryGetValue(selected.CandidateId, out var continuation)
            ? selected with { ObjectiveContinuation = ValidateContinuation(selected, continuation) }
            : selected;

    public static JsonObject ValidateContinuation(SelectedQueueCandidateLock selected, JsonObject continuation)
    {
        if (selected.ObjectiveContinuation is null ||
            Text(continuation, "kind") != Text(selected.ObjectiveContinuation, "kind") ||
            Text(continuation, "option_id") != selected.OptionId ||
            QueueReplanFilter.FilterRankedCandidates(new JsonArray(selected.RankedCandidate.DeepClone()), continuation).Count != 1)
            throw new InvalidDataException("Accepted shared continuation belongs to another original selected objective.");
        return continuation.DeepClone().AsObject();
    }

    private static string Text(JsonObject row, string key) => row[key]?.GetValue<string>() ?? string.Empty;
    private static string Parameter(JsonObject row, string name) => (row["parameters"] as JsonArray)?
        .OfType<JsonObject>().FirstOrDefault(parameter => Text(parameter, "name") == name)?["value"]?.GetValue<string>() ?? string.Empty;
}
