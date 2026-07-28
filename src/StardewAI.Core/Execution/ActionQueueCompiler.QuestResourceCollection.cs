using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateAttachedResourceCollectionQuestPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (!string.Equals(
                    ReadParameter(action, "quest_next_action"),
                    "collect_resources",
                    StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var candidateId = ReadParameter(action, "quest_candidate_id") ?? string.Empty;
            var family = ReadParameter(action, "quest_family") ?? string.Empty;
            var questId = ReadParameter(action, "quest_id") ?? string.Empty;
            var runtimeType = ReadParameter(action, "quest_runtime_type") ?? string.Empty;
            ValidateQuestIdentityAgainstSnapshot(
                snapshot,
                family,
                candidateId,
                questId,
                string.Empty,
                runtimeType,
                ReadIntParameter(action, "quest_objective_index"),
                ReadIntParameter(action, "quest_expected_current_count"),
                ReadIntParameter(action, "quest_expected_target_count"),
                reasons);

            if (!string.Equals(family, "ordinary_quest", StringComparison.Ordinal) ||
                !string.Equals(runtimeType, "ResourceCollectionQuest", StringComparison.Ordinal))
            {
                reasons.Add("quest_resource_identity_invalid");
                return reasons.ToArray();
            }

            var quest = ReadOrdinaryQuest(snapshot, questId, runtimeType);
            var requiredItemId = quest?.PerTypeFields?.ItemId ?? string.Empty;
            var requestedItemId = ReadParameter(action, "quest_required_item_id") ?? string.Empty;
            if (quest is null ||
                string.IsNullOrWhiteSpace(requiredItemId) ||
                !string.Equals(requestedItemId, requiredItemId, StringComparison.Ordinal))
            {
                reasons.Add("quest_resource_required_item_drifted");
                return reasons.ToArray();
            }

            var qualifiedRequired = requiredItemId.StartsWith("(", StringComparison.Ordinal)
                ? requiredItemId
                : "(O)" + requiredItemId;
            var targetStep = string.Equals(
                ReadParameter(action, "quest_acquisition_target_step"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var sourceStep = string.Equals(
                ReadParameter(action, "quest_acquisition_source_step"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (targetStep && sourceStep)
            {
                reasons.Add("quest_resource_step_cannot_be_source_and_receipt");
                return reasons.ToArray();
            }

            if (targetStep)
            {
                if (action.OptionId is not (
                        "executor.pickup_debris" or
                        "executor.collect_spawned_object" or
                        "executor.collect_machine_output") ||
                    !string.Equals(
                        ReadParameter(action, "qualified_item_id"),
                        qualifiedRequired,
                        StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("quest_resource_receipt_target_drifted");
                }
                return reasons.ToArray();
            }

            if (sourceStep)
            {
                if (action.OptionId == "executor.mine_stone")
                {
                    var expectedDrops = (ReadParameter(action, "expected_drop_qualified_item_ids") ?? string.Empty)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!expectedDrops.Contains(qualifiedRequired, StringComparer.OrdinalIgnoreCase))
                    {
                        reasons.Add("quest_resource_mining_source_drop_drifted");
                    }
                }
                else if (action.OptionId == "executor.clear_obstacle")
                {
                    var primaryMatches = string.Equals(
                        ReadParameter(action, "clear_output_qualified_item_id"),
                        qualifiedRequired,
                        StringComparison.OrdinalIgnoreCase) &&
                        (ReadIntParameter(action, "clear_output_quantity_min") ?? 0) > 0;
                    var bonusMatches = string.Equals(
                        ReadParameter(action, "clear_bonus_output_qualified_item_id"),
                        qualifiedRequired,
                        StringComparison.OrdinalIgnoreCase) &&
                        (ReadIntParameter(action, "clear_bonus_output_quantity_min") ?? 0) > 0;
                    if (!primaryMatches && !bonusMatches)
                    {
                        reasons.Add("quest_resource_clearance_source_drop_drifted");
                    }
                }
                else
                {
                    reasons.Add("quest_resource_source_primitive_invalid");
                }
                return reasons.ToArray();
            }

            if (action.OptionId is not (
                    "executor.combat_monster" or
                    "executor.shoot_monster" or
                    "executor.place_bomb" or
                    "executor.consume_food"))
            {
                reasons.Add("quest_resource_step_has_no_source_or_receipt_role");
            }
            return reasons.ToArray();
        }
    }
}
