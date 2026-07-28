using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static string? ValidateQuestFishingAttempt(
        TrainingExecutionRequest request,
        IEnumerable<string> possibleQualifiedItemIds)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId))
        {
            return null;
        }

        if (string.Equals(request.QuestFamily, "ordinary_quest", StringComparison.Ordinal) &&
            string.Equals(request.QuestRuntimeType, "FishingQuest", StringComparison.Ordinal))
        {
            var quest = Game1.player.questLog
                .OfType<FishingQuest>()
                .SingleOrDefault(candidate =>
                    string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
            if (quest is null || quest.completed.Value)
            {
                return "fishing_quest_live_identity_not_found";
            }
            if (quest.numberFished.Value != request.QuestExpectedCurrentCount ||
                quest.numberToFish.Value != request.QuestExpectedTargetCount)
            {
                return "fishing_quest_progress_projection_drifted";
            }

            var requiredItemId = quest.ItemId.Value ?? string.Empty;
            var qualifiedRequired = requiredItemId.StartsWith("(", StringComparison.Ordinal)
                ? requiredItemId
                : ItemRegistry.QualifyItemId(requiredItemId) ?? "(O)" + requiredItemId;
            return possibleQualifiedItemIds.Contains(
                qualifiedRequired,
                StringComparer.OrdinalIgnoreCase)
                    ? null
                    : "fishing_quest_outcome_distribution_drifted";
        }

        if (!string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal) ||
            !request.QuestObjectiveIndex.HasValue)
        {
            return null;
        }
        var order = Game1.player.team.specialOrders.SingleOrDefault(candidate =>
            string.Equals(candidate.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        if (order is null ||
            request.QuestObjectiveIndex.Value < 0 ||
            request.QuestObjectiveIndex.Value >= order.objectives.Count ||
            order.objectives[request.QuestObjectiveIndex.Value] is not FishObjective objective)
        {
            return null;
        }
        if (objective.GetCount() != request.QuestExpectedCurrentCount ||
            objective.GetMaxCount() != request.QuestExpectedTargetCount)
        {
            return "special_order_fish_progress_projection_drifted";
        }
        return possibleQualifiedItemIds.Any(itemId =>
            NativeFishObjectiveMatches(objective, ItemRegistry.Create(itemId)))
                ? null
                : "special_order_fish_outcome_distribution_drifted";
    }

    private static bool NativeFishObjectiveMatches(FishObjective objective, Item item)
    {
        var tags = item.GetContextTags();
        foreach (var acceptableSet in objective.acceptableContextTagSets)
        {
            var rejected = acceptableSet
                .Split(',')
                .Any(group => !ItemContextTagManager.DoAnyTagsMatch(group.Split('/'), tags));
            if (!rejected)
            {
                return true;
            }
        }
        return false;
    }

    private static void ApplyQuestFishingFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId))
        {
            return;
        }

        if (string.Equals(request.QuestFamily, "ordinary_quest", StringComparison.Ordinal) &&
            string.Equals(request.QuestRuntimeType, "FishingQuest", StringComparison.Ordinal))
        {
            var quest = Game1.player.questLog
                .OfType<FishingQuest>()
                .SingleOrDefault(candidate =>
                    string.Equals(candidate.id.Value, request.QuestId, StringComparison.Ordinal));
            ApplyFishingProgressObservation(
                result,
                request,
                quest?.numberFished.Value ?? request.QuestExpectedTargetCount ?? 0,
                quest is not null,
                quest?.completed.Value ?? true,
                "matching_fishing_quest_count_increased",
                "fishing_quest_attempt_completed_without_target_catch");
            return;
        }

        if (!string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal) ||
            !request.QuestObjectiveIndex.HasValue)
        {
            return;
        }
        var order = Game1.player.team.specialOrders.SingleOrDefault(candidate =>
            string.Equals(candidate.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        FishObjective? objective = null;
        if (order is not null &&
            request.QuestObjectiveIndex.Value >= 0 &&
            request.QuestObjectiveIndex.Value < order.objectives.Count)
        {
            objective = order.objectives[request.QuestObjectiveIndex.Value] as FishObjective;
        }
        if (objective is null)
        {
            return;
        }
        ApplyFishingProgressObservation(
            result,
            request,
            objective.GetCount(),
            order is not null,
            objective.IsComplete(),
            "matching_special_order_fish_count_increased",
            "special_order_fish_attempt_completed_without_target_catch");
    }

    private static void ApplyFishingProgressObservation(
        TrainingExecutionResult result,
        TrainingExecutionRequest request,
        int progressAfter,
        bool presentAfter,
        bool completedAfter,
        string progressedReason,
        string unchangedReason)
    {
        var progressBefore = request.QuestExpectedCurrentCount ?? 0;
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = progressBefore;
        result.QuestProgressAfter = progressAfter;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        result.QuestPresentBefore = true;
        result.QuestPresentAfter = presentAfter;
        result.QuestCompletedBefore = false;
        result.QuestCompletedAfter = completedAfter;

        if (progressAfter < progressBefore)
        {
            result.Status = "blocked";
            result.PrimitiveVerificationStatus = "observed_mismatch";
            result.BlockReasons = result.BlockReasons
                .Concat(new[] { "fishing_quest_progress_regressed" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return;
        }
        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[] { progressAfter > progressBefore ? progressedReason : unchangedReason })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
