using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static IReadOnlyDictionary<string, JsonObject> CompareHarvestQuests(SnapshotEnvelope before,
        SnapshotEnvelope after, string itemId, long units)
    {
        var comparison = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var priorActive = Field(before, "quests", "active_quests", required: false);
        var nextActive = Field(after, "quests", "active_quests", required: false);
        if (!Readable(priorActive) || !Readable(nextActive) ||
            JsonNode.DeepEquals(Value(priorActive!), Value(nextActive!)))
            return comparison;

        var prior = ReadHarvestQuestRows(Value(priorActive!));
        var next = ReadHarvestQuestRows(Value(nextActive!));
        var completed = new List<JsonObject>();
        var successors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, quest) in prior)
        {
            Require(next.ContainsKey(id), "Harvest removed an original quest.");
            var observed = next[id];
            if (JsonNode.DeepEquals(quest, observed)) continue;
            var definition = quest.Deserialize<QuestProgressRef>()!;
            Require(definition.RuntimeType == "ItemHarvestQuest" && !definition.Completed && !definition.Hidden &&
                    !definition.Destroy && definition.MoneyReward > 0 && definition.PerTypeFields.Available &&
                    definition.PerTypeFields.ItemId == itemId && definition.PerTypeFields.TargetCount > 0 &&
                    definition.PerTypeFields.TargetCount <= units,
                "Harvest changed a quest outside its verified item and quantity.");
            var expected = (JsonObject)quest.DeepClone();
            expected["completed"] = true;
            expected["per_type_fields"]!["target_count"] = 0;
            Require(JsonNode.DeepEquals(expected, observed), "Harvest quest changed more than its remaining count and completion.");
            completed.Add(observed);
            successors.UnionWith(definition.NextQuests.Where(successor => successor != "-1" && !prior.ContainsKey(successor)));
        }
        Require(completed.Count > 0 && successors.SetEquals(next.Keys.Except(prior.Keys, StringComparer.Ordinal)),
            "Harvest added a quest outside the completed quest's original successors.");
        foreach (var id in successors)
        {
            var quest = next[id].Deserialize<QuestProgressRef>()!;
            Require(!quest.Accepted && !quest.Completed && !quest.Hidden && !quest.Destroy && !quest.DailyQuest && quest.ShowNew,
                "Harvest successor is not a new ordinary native quest.");
        }

        var firstCompleted = Field(before, "quests", "completed_quests")!;
        var nextCompleted = Field(after, "quests", "completed_quests")!;
        var expectedCompleted = (JsonObject)Value(firstCompleted).DeepClone();
        expectedCompleted["retained_completed_quests"] = OrderedHarvestQuests(
            Array(expectedCompleted["retained_completed_quests"], "previous completed quests").Concat(completed), row => row);
        Require(JsonNode.DeepEquals(expectedCompleted, Value(nextCompleted)), "Harvest changed unrelated quest completion history.");

        var firstRewards = Field(before, "quests", "claimable_rewards")!;
        var nextRewards = Field(after, "quests", "claimable_rewards")!;
        var addedRewards = completed.Select(quest =>
        {
            var claim = JsonSerializer.SerializeToNode(new QuestRewardClaimRef
            {
                RewardFingerprint = QuestRewardClaimIdentity.Compute(quest.Deserialize<QuestProgressRef>()!),
                Claimable = true,
                Status = "ready"
            })!;
            claim["quest"] = quest.DeepClone();
            return claim;
        });
        var expectedRewards = OrderedHarvestQuests(
            Array(Value(firstRewards), "previous quest rewards").Concat(addedRewards), row => row["quest"]!);
        Require(JsonNode.DeepEquals(expectedRewards, Value(nextRewards)), "Harvest changed unrelated claimable quest rewards.");

        var firstCancellation = Field(before, "quests", "cancellation_candidates")!;
        var nextCancellation = Field(after, "quests", "cancellation_candidates")!;
        var cancellation = (JsonObject)Value(firstCancellation).DeepClone();
        VerifyHarvestCancellationProjection(cancellation, prior);
        cancellation["quest_log_count_before"] = next.Count;
        cancellation["candidates"] = BuildHarvestCancellationRows(cancellation, next);
        Require(JsonNode.DeepEquals(cancellation, Value(nextCancellation)), "Harvest changed unrelated quest cancellation state.");

        void KeepPrior(string field, JsonObject original, JsonObject observed)
        {
            observed["value"] = Value(original).DeepClone();
            comparison[field] = observed;
        }
        KeepPrior("active_quests", priorActive!, nextActive!);
        KeepPrior("completed_quests", firstCompleted, nextCompleted);
        KeepPrior("claimable_rewards", firstRewards, nextRewards);
        KeepPrior("cancellation_candidates", firstCancellation, nextCancellation);
        return comparison;
    }

    private static Dictionary<string, JsonObject> ReadHarvestQuestRows(JsonNode value) =>
        Array(value, "active harvest quests").Cast<JsonObject>()
            .ToDictionary(quest => quest["id"]!.GetValue<string>(), StringComparer.Ordinal);

    private static JsonArray OrderedHarvestQuests(IEnumerable<JsonNode?> rows, Func<JsonNode, JsonNode> quest)
        => new(rows.OrderBy(row => quest(row!)["id"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(row => row!.DeepClone()).ToArray());

    private static void VerifyHarvestCancellationProjection(JsonObject projection, Dictionary<string, JsonObject> quests)
    {
        Require(Int(projection, "quest_log_count_before") == quests.Count &&
                JsonNode.DeepEquals(BuildHarvestCancellationRows(projection, quests), projection["candidates"]),
            "Harvest quest cancellation previews do not match the original quest log.");
    }

    private static JsonArray BuildHarvestCancellationRows(JsonObject projection, Dictionary<string, JsonObject> quests)
    {
        var today = Int(projection, "current_total_days");
        var acceptedDaily = HarvestMirrorBoolean(projection, "accepted_daily_quest_before");
        return OrderedHarvestQuests(quests.Values.Select(raw =>
        {
            var quest = raw.Deserialize<QuestProgressRef>()!;
            var reasons = new List<string>();
            if (quest.Hidden) reasons.Add("quest_hidden_from_native_quest_log");
            if (!quest.Accepted) reasons.Add("quest_not_accepted");
            if (quest.Completed) reasons.Add("quest_already_completed");
            if (!quest.CanBeCancelled) reasons.Add("quest_native_cancellation_disabled");
            if (quest.Destroy) reasons.Add("quest_pending_native_removal");
            var resetsDaily = quest.DailyQuest && quest.DayQuestAccepted == today;
            var row = JsonSerializer.SerializeToNode(new QuestCancellationCandidateRef
            {
                CancellationFingerprint = QuestCancellationIdentity.Compute(quest),
                Eligible = reasons.Count == 0,
                NativeButtonVisible = !quest.Completed && quest.CanBeCancelled,
                ResetsAcceptedDailyQuest = resetsDaily,
                ExpectedAcceptedDailyQuestAfter = !resetsDaily && acceptedDaily,
                ExpectedQuestLogCountAfter = quests.Count - 1,
                Status = reasons.Count == 0 ? "ready" : "blocked",
                BlockedDiagnostics = reasons.ToArray()
            })!;
            row["quest"] = raw.DeepClone();
            return row;
        }), row => row["quest"]!);
    }
}
