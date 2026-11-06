using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageTeacherCorpusTests
{
    // Protocol-port fixture; public Build additionally replays the complete original sources.
    private static MarriageTeacherCorpus LocalMarriageCorpus()
    {
        var run = Run("local");
        var chain = JsonNode.Parse(run.QueueChain.GetRawText())!.AsArray();
        foreach (var queue in chain) queue!["candidate_kind"] = "recovery_sleep_immediately";
        chain.Insert(2, new JsonObject
        {
            ["index"] = 2, ["role"] = "mechanical_control_not_preference_label", ["candidate_kind"] = "partnership_propose_marriage_current",
            ["candidate_id"] = "proposal-candidate", ["before_state_hash"] = "proposal-before", ["queue_id"] = "proposal-queue",
            ["queue_item_ids"] = new JsonArray("proposal-item"), ["request_nonces"] = new JsonArray("proposal-nonce"),
            ["primitive_options"] = new JsonArray("executor.social_interact")
        });
        for (var index = 0; index < chain.Count; index++) chain[index]!["index"] = index;
        return Corpus(run with { QueueChain = JsonSerializer.SerializeToElement(chain) });
    }

    private static JsonArray MarriageRoutes(MarriageTeacherCorpus corpus) => new(corpus.Runs.Select(run => (JsonNode)new JsonObject
    {
        ["source_id"] = run.SourceId, ["option_id"] = "social.advance_partnership", ["selected_candidate_id"] = "proposal-candidate",
        ["source_state_hash"] = "proposal-before", ["queue_id"] = "proposal-queue",
        ["declared_native_prerequisites"] = new JsonObject
        {
            ["native_receipt_sha256"] = run.NativeReceiptSha256, ["before_state_hash"] = run.BeforeStateHash,
            ["after_state_hash"] = run.AfterStateHash, ["native_wedding_total_day"] = run.TotalDayAfter,
            ["initial_house_level"] = 3, ["initial_house_pending_days"] = -1, ["final_house_level"] = 3, ["npc_name"] = "Abigail"
        },
        ["preparation_bindings"] = new JsonArray(run.PreparationDecisions.Select(decision => (JsonNode)new JsonObject
        {
            ["preparation_index"] = decision.PreparationIndex, ["option_id"] = "social.gift_npc", ["dependency_node_id"] = MarriagePrefix + "gift_partner",
            ["selected_candidate_id"] = decision.SelectedCandidateId, ["source_state_hash"] = decision.BeforeStateHash, ["queue_id"] = decision.QueueId
        }).ToArray())
    }).ToArray());
}
