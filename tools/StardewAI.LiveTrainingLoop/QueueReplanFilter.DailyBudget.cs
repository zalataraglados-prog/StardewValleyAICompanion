using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
    public static bool ShouldReleaseTimeBlockedRouteContinuation(
        JsonObject? continuation, JsonObject? ranking, JsonObject queue)
    {
        if (continuation is null ||
            ReadString(queue, "status") != "blocked" ||
            queue["items"] is not JsonArray { Count: 0 } ||
            !ContainsOnlyReason(queue["compiler_diagnostics"], "empty_action_list") ||
            queue["candidate_audit"] is not JsonArray { Count: > 0 } audit ||
            ranking?["objective_continuation_filter"] is not JsonObject filter ||
            filter["active"] is not JsonValue active || !active.TryGetValue<bool>(out var isActive) || !isActive ||
            !JsonNode.DeepEquals(filter["objective"], continuation) ||
            ranking["candidate_id_filter"] is not JsonObject candidateFilter ||
            !TryReadInt(candidateFilter, "selected_candidate_count", out var count) || count != audit.Count ||
            ranking["goal_resolution"] is not JsonObject resolution ||
            ranking["ranked_event_candidates"] is not JsonArray rankedCandidates)
        {
            return false;
        }

        var stateHash = ReadString(queue, "state_hash");
        if (string.IsNullOrWhiteSpace(stateHash) || stateHash != ReadString(resolution, "source_state_hash"))
            return false;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Only defer travel from the ordinary planner, never a terminal action
        // or an untyped fallback that happens to use the same option ID.
        return audit.All(node =>
        {
            if (node is not JsonObject item || !IsExclusiveTimeBudgetSkip(item))
                return false;
            var id = ReadString(item, "candidate_id");
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                return false;
            var matches = rankedCandidates.OfType<JsonObject>()
                .Where(candidate => ReadString(candidate, "candidate_id") == id).ToArray();
            if (matches is not [var candidate] || ReadString(candidate, "kind") != "route_connector_tile" ||
                !MatchesContinuation(candidate, continuation))
                return false;
            var rebound = ReadObjectiveContinuation(new JsonObject
            {
                ["normalized_command"] = new JsonObject { ["parameters"] = candidate["parameters"]?.DeepClone() }
            });
            return rebound is not null && ReadString(rebound, "kind") == ReadString(continuation, "kind") &&
                ReadString(rebound, "option_id") == ReadString(continuation, "option_id") &&
                MatchesContinuation(candidate, rebound);
        });
    }

    internal static bool IsExclusiveTimeBudgetSkip(JsonObject item) =>
        ReadString(item, "decision") == "skipped" &&
        ContainsOnlyReason(item["reasons"], "aggregate_time_budget_exceeded") &&
        TryReadInt(item, "candidate_minutes", out var minutes) &&
        TryReadInt(item, "remaining_minutes_before", out var remaining) &&
        remaining >= 0 && minutes > remaining;

    private static bool ContainsOnlyReason(JsonNode? node, string reason) =>
        node is JsonArray { Count: 1 } values &&
        values[0] is JsonValue entry && entry.TryGetValue<string>(out var value) &&
        value == reason;

    private static bool TryReadInt(JsonObject obj, string name, out int value)
    {
        value = 0;
        return obj[name] is JsonValue node && node.TryGetValue(out value);
    }
}
