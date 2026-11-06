using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageLocalDependencyViewBuilder
{
    private static void ValidateBindings(MarriageTeacherCorpus corpus, JsonArray routes)
    {
        Require(routes.Count == corpus.Runs.Length && routes.Select(route => Text(route!, "source_id")).Distinct(StringComparer.Ordinal).Count() == routes.Count,
            "Marriage local source-route identities are incomplete or repeated.");
        var catalog = GrandpaDirectionCatalog.Entries.Single(entry => entry.DirectionId == DirectionId);
        foreach (var route in routes)
        {
            var runs = corpus.Runs.Where(run => run.SourceId == Text(route!, "source_id")).ToArray();
            Require(runs.Length == 1, "Marriage local route belongs to another source.");
            var run = runs[0];
            var proposal = Proposal(run);
            Require(run.NativeOutcomeVerified && Text(route!, "option_id") == "social.advance_partnership" &&
                catalog.PermittedOptionIds.Contains(Text(route!, "option_id")) &&
                Text(route!, "selected_candidate_id") == proposal.GetProperty("candidate_id").GetString() &&
                Text(route!, "source_state_hash") == proposal.GetProperty("before_state_hash").GetString() &&
                Text(route!, "queue_id") == proposal.GetProperty("queue_id").GetString(),
                "Marriage direct route is not its original native proposal control; gifts remain dependencies.");
            var premise = route!["declared_native_prerequisites"]!;
            Require(Text(premise, "native_receipt_sha256") == run.NativeReceiptSha256 &&
                Text(premise, "before_state_hash") == run.BeforeStateHash && Text(premise, "after_state_hash") == run.AfterStateHash &&
                premise["native_wedding_total_day"]!.GetValue<int>() == run.TotalDayAfter,
                "Marriage declared prerequisites belong to another native source.");
            var preparations = route["preparation_bindings"]!.AsArray();
            Require(preparations.Count == run.PreparationDecisions.Length && preparations.Select(value => value!["preparation_index"]!.GetValue<int>())
                .SequenceEqual(run.PreparationDecisions.Select(value => value.PreparationIndex)),
                "Marriage local route omits or reorders a preparation dependency.");
            foreach (var (binding, index) in preparations.Select((value, index) => (value!, index)))
            {
                var decision = run.PreparationDecisions[index];
                Require(Text(binding, "option_id") == "social.gift_npc" && Text(binding, "dependency_node_id") == Prefix + "gift_partner" &&
                    Text(binding, "selected_candidate_id") == decision.SelectedCandidateId && Text(binding, "source_state_hash") == decision.BeforeStateHash &&
                    Text(binding, "queue_id") == decision.QueueId && decision.StrictPairCount > 0,
                    "Marriage preparation is not the exact original gift dependency and independent pair.");
            }
        }
    }
}
