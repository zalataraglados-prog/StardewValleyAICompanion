using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
    public static bool ShouldReleaseTimeBlockedSocialTalk(
        JsonObject? continuation, JsonObject? ranking, JsonObject queue)
    {
        if (ReadString(continuation, "kind") != "social" ||
            ReadString(continuation, "option_id") != "social.talk_npc" ||
            string.IsNullOrWhiteSpace(ReadString(continuation, "npc_name")) ||
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

        var matchingIds = rankedCandidates.OfType<JsonObject>()
            .Where(candidate => MatchesContinuation(candidate, continuation!))
            .Select(candidate => ReadString(candidate, "candidate_id"))
            .ToHashSet(StringComparer.Ordinal);
        matchingIds.Remove(string.Empty);
        // Release only the exact audited talk lock. An empty queue caused by any
        // other precondition must retain its original handling.
        return audit.All(node => node is JsonObject item &&
            matchingIds.Remove(ReadString(item, "candidate_id")) &&
            ReadString(item, "decision") == "skipped" &&
            ContainsOnlyReason(item["reasons"], "aggregate_time_budget_exceeded") &&
            TryReadInt(item, "candidate_minutes", out var minutes) &&
            TryReadInt(item, "remaining_minutes_before", out var remaining) &&
            remaining >= 0 && minutes > remaining);
    }

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
