using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class GlobalDailySocialBudgetRecoveryTests
{
    [Fact]
    public void ReleasesR4TalkLockAndKeepsOtherRankedWorkAvailable()
    {
        var (objective, ranking, queue) = R4Queue();
        Assert.False(QueueReplanFilter.ShouldReleaseUnavailableContinuation(objective, ranking));
        Assert.True(QueueReplanFilter.ShouldReleaseTimeBlockedSocialTalk(objective, ranking, queue));

        var suppressed = new List<JsonObject>();
        Assert.True(QueueReplanFilter.AddSuppressedContinuation(suppressed, objective));
        var remaining = QueueReplanFilter.FilterSuppressedContinuations(
            ranking["ranked_event_candidates"]!.AsArray(), suppressed);
        Assert.Equal("social:talk:Robin:route:Town:45,70", Assert.Single(remaining)!["candidate_id"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("different_objective")]
    [InlineData("wrong_snapshot")]
    [InlineData("unknown_budget")]
    [InlineData("not_over_budget")]
    [InlineData("other_blocker")]
    [InlineData("item_was_compiled")]
    [InlineData("no_filter")]
    [InlineData("incomplete_audit")]
    [InlineData("unrelated_audit")]
    public void DoesNotReleaseWithoutExactTimeExhaustion(string difference)
    {
        var (objective, ranking, queue) = R4Queue();
        var audit = queue["candidate_audit"]![0]!;
        switch (difference)
        {
            case "different_objective": objective["option_id"] = "social.give_gift"; break;
            case "wrong_snapshot": queue["state_hash"] = "other"; break;
            case "unknown_budget": audit["remaining_minutes_before"] = null; break;
            case "not_over_budget": audit["remaining_minutes_before"] = 176; break;
            case "other_blocker": audit["reasons"] = new JsonArray("aggregate_energy_budget_exceeded"); break;
            case "item_was_compiled": queue["items"] = new JsonArray(new JsonObject()); break;
            case "no_filter": ranking["objective_continuation_filter"] = null; break;
            case "incomplete_audit": ranking["candidate_id_filter"]!["selected_candidate_count"] = 2; break;
            case "unrelated_audit": audit["candidate_id"] = "social:talk:Robin:route:Town:45,70"; break;
        }
        Assert.False(QueueReplanFilter.ShouldReleaseTimeBlockedSocialTalk(objective, ranking, queue));
    }

    private static (JsonObject Objective, JsonObject Ranking, JsonObject Queue) R4Queue()
    {
        var objective = new JsonObject
        {
            ["kind"] = "social", ["option_id"] = "social.talk_npc", ["npc_name"] = "Linus",
            ["target_location"] = "Mountain"
        };
        const string linus = "social:talk:Linus:route:Town:79,-1";
        JsonObject Route(string id, string npc, string location) => new()
        {
            ["candidate_id"] = id, ["kind"] = "route_connector_tile", ["option_id"] = "social.talk_npc",
            ["parameters"] = new JsonArray(
                new JsonObject { ["name"] = "continuation.npc_name", ["value"] = npc },
                new JsonObject { ["name"] = "continuation.target_location", ["value"] = location })
        };
        var ranking = new JsonObject
        {
            ["objective_continuation_filter"] = new JsonObject { ["active"] = true,
                ["objective"] = objective.DeepClone(), ["selected_candidate_count"] = 1 },
            ["candidate_id_filter"] = new JsonObject { ["selected_candidate_count"] = 1 },
            ["goal_resolution"] = new JsonObject { ["source_state_hash"] = "same-state" },
            ["ranked_event_candidates"] = new JsonArray(Route(linus, "Linus", "Mountain"),
                Route("social:talk:Robin:route:Town:45,70", "Robin", "ScienceHouse"))
        };
        var queue = new JsonObject
        {
            ["status"] = "blocked", ["state_hash"] = "same-state", ["items"] = new JsonArray(),
            ["compiler_diagnostics"] = new JsonArray("empty_action_list"),
            ["candidate_audit"] = new JsonArray(new JsonObject
            {
                ["decision"] = "skipped", ["candidate_id"] = linus,
                ["reasons"] = new JsonArray("aggregate_time_budget_exceeded"),
                ["candidate_minutes"] = 176, ["remaining_minutes_before"] = 120
            })
        };
        return (objective, ranking, queue);
    }
}
