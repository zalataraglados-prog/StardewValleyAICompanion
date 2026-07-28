using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool ValidateQuestSlayTarget(
        TrainingExecutionRequest request,
        Monster target,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId))
        {
            return true;
        }
        if (!request.QuestExpectedCurrentCount.HasValue ||
            !request.QuestExpectedTargetCount.HasValue)
        {
            reason = "quest_slay_expected_progress_required";
            return false;
        }

        if (request.QuestFamily == "ordinary_quest")
        {
            var quest = Game1.player.questLog
                .OfType<SlayMonsterQuest>()
                .SingleOrDefault(row =>
                    string.Equals(row.id.Value, request.QuestId, StringComparison.Ordinal) &&
                    string.Equals(row.GetType().Name, request.QuestRuntimeType, StringComparison.Ordinal));
            if (quest is null)
            {
                reason = "quest_slay_live_identity_not_found";
                return false;
            }
            if (quest.numberKilled.Value != request.QuestExpectedCurrentCount.Value ||
                quest.numberToKill.Value != request.QuestExpectedTargetCount.Value)
            {
                reason = "quest_slay_progress_projection_drifted";
                return false;
            }
            if (request.QuestSlayTargetStep &&
                !QuestMonsterTargetRules.Matches(
                    target.Name,
                    new[] { quest.monsterName.Value ?? string.Empty },
                    matchAnySlimeName: string.Equals(quest.id.Value, "15", StringComparison.Ordinal)))
            {
                reason = "quest_slay_target_name_drifted";
                return false;
            }
            return true;
        }

        if (request.QuestFamily == "special_order")
        {
            var order = Game1.player.team.specialOrders.SingleOrDefault(row =>
                string.Equals(row.questKey.Value, request.QuestKey, StringComparison.Ordinal));
            if (order is null ||
                !request.QuestObjectiveIndex.HasValue ||
                request.QuestObjectiveIndex.Value < 0 ||
                request.QuestObjectiveIndex.Value >= order.objectives.Count ||
                order.objectives[request.QuestObjectiveIndex.Value] is not SlayObjective objective)
            {
                reason = "special_order_slay_live_objective_not_found";
                return false;
            }
            if (objective.GetCount() != request.QuestExpectedCurrentCount.Value ||
                objective.GetMaxCount() != request.QuestExpectedTargetCount.Value)
            {
                reason = "special_order_slay_progress_projection_drifted";
                return false;
            }
            if (objective.ignoreFarmMonsters.Value &&
                string.Equals(target.currentLocation?.Name, "Farm", StringComparison.Ordinal))
            {
                reason = "special_order_slay_ignores_farm_monster";
                return false;
            }
            if (request.QuestSlayTargetStep &&
                !QuestMonsterTargetRules.Matches(target.Name, objective.targetNames))
            {
                reason = "special_order_slay_target_name_drifted";
                return false;
            }
            return true;
        }

        reason = "quest_slay_family_invalid";
        return false;
    }

    private static void ApplyQuestSlayFeedback(
        TrainingExecutionResult result,
        TrainingExecutionRequest request,
        bool requireProgress)
    {
        if (string.IsNullOrWhiteSpace(request.QuestCandidateId))
        {
            return;
        }

        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestProgressBefore = request.QuestExpectedCurrentCount;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        result.QuestPresentBefore = true;
        result.QuestCompletedBefore = false;

        int? progressAfter = null;
        var presentAfter = false;
        var completedAfter = false;
        if (request.QuestFamily == "ordinary_quest")
        {
            var quest = Game1.player.questLog
                .OfType<SlayMonsterQuest>()
                .SingleOrDefault(row =>
                    string.Equals(row.id.Value, request.QuestId, StringComparison.Ordinal) &&
                    string.Equals(row.GetType().Name, request.QuestRuntimeType, StringComparison.Ordinal));
            if (quest is not null)
            {
                presentAfter = true;
                completedAfter = quest.completed.Value;
                progressAfter = quest.numberKilled.Value;
            }
        }
        else if (request.QuestFamily == "special_order")
        {
            var order = Game1.player.team.specialOrders.SingleOrDefault(row =>
                string.Equals(row.questKey.Value, request.QuestKey, StringComparison.Ordinal));
            if (order is not null)
            {
                presentAfter = true;
                completedAfter = order.questState.Value == SpecialOrderStatus.Complete;
                if (request.QuestObjectiveIndex.HasValue &&
                    request.QuestObjectiveIndex.Value >= 0 &&
                    request.QuestObjectiveIndex.Value < order.objectives.Count)
                {
                    progressAfter = order.objectives[request.QuestObjectiveIndex.Value].GetCount();
                }
            }
        }

        result.QuestProgressAfter = progressAfter;
        result.QuestPresentAfter = presentAfter;
        result.QuestCompletedAfter = completedAfter;
        var progressed = !presentAfter ||
            completedAfter ||
            progressAfter > request.QuestExpectedCurrentCount;
        if (progressAfter != request.QuestExpectedCurrentCount)
        {
            result.ChangedFacts = result.ChangedFacts
                .Concat(new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests." + request.QuestCandidateId + ".current_count",
                        Before = request.QuestExpectedCurrentCount?.ToString() ?? string.Empty,
                        After = progressAfter?.ToString() ?? string.Empty
                    }
                })
                .ToArray();
        }
        if (!requireProgress)
        {
            return;
        }
        if (progressed)
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Concat(new[] { "matching_quest_slay_progress_changed" })
                .ToArray();
            return;
        }

        result.Status = "blocked";
        result.FailureCategory = "observed_mismatch";
        result.TrainingImpactScope = "executor_calibration";
        result.PrimitiveVerificationStatus = "observed_mismatch";
        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Concat(new[] { "target_defeated_without_matching_quest_slay_progress" })
            .ToArray();
        result.BlockReasons = result.BlockReasons
            .Concat(new[] { "quest_slay_progress_not_observed" })
            .ToArray();
    }
}
