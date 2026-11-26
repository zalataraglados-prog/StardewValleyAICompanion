using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static string[]? GlobalDailyPrimaryCandidates(LiveTrainingOptions options, JsonObject? resolution,
        bool ownedComposition, bool exclusiveRecovery, bool priorityInterrupt, bool activeContinuation, bool scopedRequest)
    {
        if (!options.ContinueGlobalDailyPlanAfterCropTeacherRun || !options.UseJointDailyCalendar ||
            ownedComposition || exclusiveRecovery || priorityInterrupt || activeContinuation || scopedRequest ||
            ReadString(resolution, "status") != "resolved") return null;
        return resolution?["bound_candidate_ids"]?.AsArray()
            .Select(node => node?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)
            .Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
    }

    private static string WithGlobalDailyPrimaryCandidates(string request, string[]? primary)
    {
        if (primary is null) return request;
        var root = JsonNode.Parse(request)?.AsObject() ?? throw new InvalidDataException("Daily compile request is empty.");
        root["primary_candidate_ids"] = JsonSerializer.SerializeToNode(primary, JsonOptions);
        root["use_joint_calendar"] = true;
        return root.ToJsonString(JsonOptions);
    }

    private static void VerifyGlobalDailyPrimaryResponse(JsonObject plan, string[]? primary)
    {
        if (primary is null) return;
        // Older backends ignore unknown request properties. Never dispatch a broad
        // candidate pool unless the compiler confirms this requested constraint.
        if (plan["primary_candidate_ids"] is not JsonArray applied ||
            !applied.Select(node => node?.GetValue<string>()).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(primary.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("daily_plan_primary_goal_constraint_not_applied: use a matching backend/compiler before dispatch.");
    }

    private static bool GlobalDailyPrimaryTimeExhausted(
        string stateHash, string[]? primary, JsonObject plan, JsonObject queue)
    {
        if (primary is not { Length: > 0 } || string.IsNullOrWhiteSpace(stateHash) ||
            ReadString(plan, "state_hash") != stateHash || ReadString(queue, "state_hash") != stateHash ||
            plan["steps"] is not JsonArray { Count: 0 } ||
            ReadString(queue, "status") != "blocked" || queue["items"] is not JsonArray { Count: 0 } ||
            queue["compiler_diagnostics"] is not JsonArray { Count: 1 } diagnostics ||
            diagnostics[0]?.ToString() != "empty_action_list" ||
            plan["candidate_audit"] is not JsonArray audit)
            return false;

        return primary.All(id =>
        {
            var rows = audit.OfType<JsonObject>().Where(row => ReadString(row, "candidate_id") == id).ToArray();
            return rows is [var row] && QueueReplanFilter.IsExclusiveTimeBudgetSkip(row);
        });
    }
}
