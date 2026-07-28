using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidateAttachedFishingQuestPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.catch_fish" ||
            ReadParameter(action, "quest_next_action") is not ("fish_for_item" or "catch_fish"))
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var family = ReadParameter(action, "quest_family") ?? string.Empty;
        var questId = ReadParameter(action, "quest_id") ?? string.Empty;
        var questKey = ReadParameter(action, "quest_key") ?? string.Empty;
        var runtimeType = ReadParameter(action, "quest_runtime_type") ?? string.Empty;
        var objectiveIndex = ReadIntParameter(action, "quest_objective_index");
        ValidateQuestIdentityAgainstSnapshot(
            snapshot,
            family,
            ReadParameter(action, "quest_candidate_id") ?? string.Empty,
            questId,
            questKey,
            runtimeType,
            objectiveIndex,
            ReadIntParameter(action, "quest_expected_current_count"),
            ReadIntParameter(action, "quest_expected_target_count"),
            reasons);

        var distributionJson = ReadParameter(action, "outcome_distribution_json") ??
            string.Empty;
        if (string.Equals(family, "ordinary_quest", StringComparison.Ordinal) &&
            string.Equals(runtimeType, "FishingQuest", StringComparison.Ordinal))
        {
            var quest = ReadOrdinaryQuest(snapshot, questId, runtimeType);
            var requiredItemId = quest?.PerTypeFields?.ItemId ?? string.Empty;
            var qualifiedRequired = requiredItemId.StartsWith("(", StringComparison.Ordinal)
                ? requiredItemId
                : "(O)" + requiredItemId;
            if (quest is null ||
                string.IsNullOrWhiteSpace(requiredItemId) ||
                !ProjectedOutputContainsItem(distributionJson, qualifiedRequired))
            {
                reasons.Add("fishing_quest_outcome_distribution_drifted");
            }
            return reasons.ToArray();
        }

        if (!string.Equals(family, "special_order", StringComparison.Ordinal) ||
            !objectiveIndex.HasValue)
        {
            reasons.Add("special_order_fish_identity_invalid");
            return reasons.ToArray();
        }
        var objective = ReadSpecialOrderCollectObjective(
            snapshot,
            questKey,
            objectiveIndex.Value);
        var tagSets = objective?.PerTypeFields?.AcceptableContextTagSets ??
            Array.Empty<string>();
        if (objective is null ||
            !string.Equals(objective.RuntimeType, "FishObjective", StringComparison.Ordinal) ||
            !ProjectedOutputContextTagsMatch(distributionJson, tagSets))
        {
            reasons.Add("special_order_fish_outcome_distribution_drifted");
        }
        return reasons.ToArray();
    }
}
