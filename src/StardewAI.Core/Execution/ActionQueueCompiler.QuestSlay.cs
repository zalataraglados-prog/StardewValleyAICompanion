using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static void ValidateAttachedSlayQuestPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            ICollection<string> reasons)
        {
            var candidateId = ReadParameter(action, "quest_candidate_id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                return;
            }
            if (!string.Equals(ReadParameter(action, "quest_next_action"), "slay_monsters", StringComparison.Ordinal))
            {
                return;
            }

            var family = ReadParameter(action, "quest_family") ?? string.Empty;
            var questId = ReadParameter(action, "quest_id") ?? string.Empty;
            var questKey = ReadParameter(action, "quest_key") ?? string.Empty;
            var runtimeType = ReadParameter(action, "quest_runtime_type") ?? string.Empty;
            var objectiveIndex = ReadIntParameter(action, "quest_objective_index");
            var expectedCurrent = ReadIntParameter(action, "quest_expected_current_count");
            var expectedTarget = ReadIntParameter(action, "quest_expected_target_count");
            ValidateQuestIdentityAgainstSnapshot(
                snapshot,
                family,
                candidateId,
                questId,
                questKey,
                runtimeType,
                objectiveIndex,
                expectedCurrent,
                expectedTarget,
                reasons);

            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            var expectedFamily = ReadParameter(action, "quest_target_location_family") ?? string.Empty;
            if (!currentMine.HasValue ||
                currentMine.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(currentMine.Value, "mine_kind"), expectedFamily, StringComparison.Ordinal))
            {
                reasons.Add("quest_slay_target_mine_family_drifted");
            }

            var targetStep = string.Equals(
                ReadParameter(action, "quest_slay_target_step"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (!targetStep)
            {
                return;
            }
            if (action.OptionId is not ("executor.combat_monster" or "executor.shoot_monster" or "executor.place_bomb"))
            {
                reasons.Add("quest_slay_target_step_requires_combat_primitive");
                return;
            }

            var targetName = ReadParameter(action, "target_name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetName))
            {
                reasons.Add("quest_slay_target_name_required");
                return;
            }

            if (family == "ordinary_quest")
            {
                var quest = ReadOrdinaryQuest(snapshot, questId, runtimeType);
                var fragment = quest?.PerTypeFields?.MonsterName ?? string.Empty;
                if (quest is null ||
                    !QuestMonsterTargetRules.Matches(
                        targetName,
                        new[] { fragment },
                        matchAnySlimeName: string.Equals(quest.Id, "15", StringComparison.Ordinal)))
                {
                    reasons.Add("quest_slay_target_name_drifted");
                }
                return;
            }

            if (family == "special_order")
            {
                var objective = ReadSpecialOrderSlayObjective(snapshot, questKey, objectiveIndex);
                if (objective is null ||
                    !QuestMonsterTargetRules.Matches(
                        targetName,
                        objective.PerTypeFields.TargetNames))
                {
                    reasons.Add("special_order_slay_target_name_drifted");
                }
            }
        }

        private static QuestProgressRef? ReadOrdinaryQuest(
            SnapshotEnvelope snapshot,
            string questId,
            string runtimeType)
        {
            var state = ReadStateFieldValue(snapshot, "quests", "active_quests");
            var rows = state.HasValue && state.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<QuestProgressRef[]>(state.Value.GetRawText()) ?? Array.Empty<QuestProgressRef>()
                : Array.Empty<QuestProgressRef>();
            return rows.SingleOrDefault(row =>
                string.Equals(row.Id, questId, StringComparison.Ordinal) &&
                string.Equals(row.RuntimeType, runtimeType, StringComparison.Ordinal));
        }

        private static SpecialOrderObjectiveProgressRef? ReadSpecialOrderSlayObjective(
            SnapshotEnvelope snapshot,
            string questKey,
            int? objectiveIndex)
        {
            if (!objectiveIndex.HasValue)
            {
                return null;
            }
            var state = ReadStateFieldValue(snapshot, "quests", "special_orders");
            var rows = state.HasValue && state.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(state.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();
            var order = rows.SingleOrDefault(row =>
                string.Equals(row.QuestKey, questKey, StringComparison.Ordinal));
            return order is not null &&
                objectiveIndex.Value >= 0 &&
                objectiveIndex.Value < order.Objectives.Length &&
                string.Equals(order.Objectives[objectiveIndex.Value].RuntimeType, "SlayObjective", StringComparison.Ordinal)
                    ? order.Objectives[objectiveIndex.Value]
                    : null;
        }
    }
}
