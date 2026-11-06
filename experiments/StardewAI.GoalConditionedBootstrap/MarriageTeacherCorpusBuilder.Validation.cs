using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageTeacherCorpusBuilder
{
    private static void ValidateAuthority(GoalKnowledge knowledge)
    {
        Require(knowledge.GoalId == GoalMethodTeacherCoverageGoalIds.Authoritative && knowledge.TargetScore == 21 &&
            knowledge.CriterionCount == 19 && knowledge.Criteria.Length == 19 &&
            knowledge.Criteria.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() == 19 && knowledge.Criteria.Sum(row => row.Points) == 21,
            "Marriage corpus requires the complete authoritative goal denominator.");
        var criterion = knowledge.Criteria.SingleOrDefault(row => row.Id == MarriageTeacherCorpus.CriterionId);
        Require(criterion is not null && criterion.Points == 1 && criterion.Operation == "and" &&
            criterion.Target == "married_or_roommate=true;farmhouse_upgrade_level>=2" &&
            criterion.StatePaths.SequenceEqual(new[] { "player.married_or_roommate", "player.farmhouse_upgrade_level" }),
            "Marriage corpus authority differs from the native joint scoring rule.");
    }

    private static void ValidateRuns(MarriageTeacherCorpusRun[] runs)
    {
        var decisions = new HashSet<(string Save, string Player, string State, string Candidate)>();
        var trajectories = new HashSet<(string Run, string Trajectory)>();
        var queues = new HashSet<(string Run, string Queue)>();
        var items = new HashSet<(string Run, string Queue, string Item)>();
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var nativeHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            Require(run.NativeOutcomeVerified && nativeHashes.Add(run.NativeReceiptSha256) &&
                run.DatasetPartition == PolicyTrajectoryDatasetBuilder.PartitionFor(run.OriginSplitKey) &&
                run.PreparationDecisions is { Length: > 0 } && run.QueueChain.ValueKind == JsonValueKind.Array,
                "Marriage corpus repeats a native source or has no complete original decision partition.");
            var chain = run.QueueChain.EnumerateArray().ToArray();
            Require(chain.Length > run.PreparationDecisions.Length && chain.Length <= 64 &&
                run.PreparationDecisions.Select(row => row.PreparationIndex).SequenceEqual(Enumerable.Range(0, run.PreparationDecisions.Length)) &&
                Text(chain[0], "before_state_hash") == run.BeforeStateHash &&
                run.TotalDayBefore == chain[0].GetProperty("total_day_before").GetInt32() &&
                run.TotalDayAfter == chain[^1].GetProperty("total_day_after").GetInt32(),
                "Marriage corpus omits a preparation or its complete joint control chain.");
            for (var index = 0; index < chain.Length; index++)
            {
                var queue = chain[index];
                var id = Text(queue, "queue_id");
                var queueItems = Strings(queue, "queue_item_ids");
                var options = Strings(queue, "primitive_options");
                var requests = Strings(queue, "request_nonces");
                Require(queue.GetProperty("index").GetInt32() == index && queues.Add((run.RunId, id)) &&
                    queueItems.Length > 0 && options.Length == queueItems.Length && requests.Length == queueItems.Length &&
                    options.All(value => !string.IsNullOrWhiteSpace(value)) &&
                    queueItems.All(item => !string.IsNullOrWhiteSpace(item) && items.Add((run.RunId, id, item))) &&
                    requests.All(nonce => !string.IsNullOrWhiteSpace(nonce) && nonces.Add(nonce)) &&
                    Text(queue, "role") == (index < run.PreparationDecisions.Length
                        ? "independent_gift_preparation" : "mechanical_control_not_preference_label"),
                    "Marriage corpus repeats or loses a queue, primitive, nonce or control role.");
                if (index >= run.PreparationDecisions.Length) continue;
                var row = run.PreparationDecisions[index];
                Require(row.DecisionCandidateCount > 1 && row.StrictPairCount > 0 &&
                    decisions.Add((run.SaveId, run.PlayerId, row.BeforeStateHash, row.SelectedCandidateId)) &&
                    trajectories.Add((run.RunId, row.TrajectoryId)) && row.QueueId == id &&
                    row.BeforeStateHash == Text(queue, "before_state_hash") && row.AfterStateHash == Text(queue, "after_state_hash") &&
                    row.SelectedCandidateId == Text(queue, "candidate_id") && row.QueueItemIds.SequenceEqual(queueItems) &&
                    row.PrimitiveOptionIds.SequenceEqual(options) && row.RequestNonces.SequenceEqual(requests),
                    "Marriage corpus preparation differs from its original decision, comparison or queue.");
            }
            var transition = run.JointCriterionTransition;
            Require(Text(transition, "option_id") == "executor.sleep" &&
                transition.GetProperty("total_day_after").GetInt32() == run.TotalDayAfter &&
                transition.GetProperty("total_day_before").GetInt32() + 1 == run.TotalDayAfter &&
                chain.Skip(run.PreparationDecisions.Length).Any(queue => TransitionInQueue(transition, queue)),
                "Marriage corpus requires the recorded native joint transition, not a gift or unfinished wedding.");
        }
    }

    private static bool TransitionInQueue(JsonElement transition, JsonElement queue)
    {
        if (Text(queue, "queue_id") != Text(transition, "queue_id")) return false;
        var index = Array.IndexOf(Strings(queue, "queue_item_ids"), Text(transition, "queue_item_id"));
        return index >= 0 && Strings(queue, "request_nonces")[index] == Text(transition, "request_nonce") &&
            Strings(queue, "primitive_options")[index] == Text(transition, "option_id");
    }
}
