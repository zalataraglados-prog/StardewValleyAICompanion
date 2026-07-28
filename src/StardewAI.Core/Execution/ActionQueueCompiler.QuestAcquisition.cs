using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateAttachedItemHarvestQuestPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_crop" ||
                !string.Equals(ReadParameter(action, "quest_next_action"), "harvest_items", StringComparison.Ordinal))
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
                !string.Equals(runtimeType, "ItemHarvestQuest", StringComparison.Ordinal))
            {
                reasons.Add("quest_harvest_identity_invalid");
                return reasons.ToArray();
            }
            if (!string.Equals(
                    ReadParameter(action, "quest_acquisition_target_step"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("quest_harvest_target_step_required");
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var crop = targetX.HasValue && targetY.HasValue
                ? HarvestCropAt(snapshot, targetX.Value, targetY.Value)
                : null;
            if (!crop.HasValue ||
                !string.Equals(ReadString(crop.Value, "harvest_method"), "Grab", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("quest_harvest_requires_ready_grab_crop");
                return reasons.ToArray();
            }

            var quest = ReadOrdinaryQuest(snapshot, questId, runtimeType);
            var requiredItemId = quest?.PerTypeFields?.ItemId ?? string.Empty;
            var cropQualifiedId = ReadString(crop.Value, "harvest_item_qualified_id");
            var matches = requiredItemId.StartsWith("-", StringComparison.Ordinal)
                ? int.TryParse(requiredItemId, out var category) &&
                    ReadInt(crop.Value, "harvest_item_category") == category
                : ItemHarvestIdentityMatches(
                    ReadString(crop.Value, "harvest_item_id"),
                    cropQualifiedId,
                    requiredItemId);
            if (quest is null || string.IsNullOrWhiteSpace(requiredItemId) || !matches)
            {
                reasons.Add("quest_harvest_item_identity_drifted");
            }
            if (!string.Equals(
                    ReadParameter(action, "quest_required_item_id"),
                    requiredItemId,
                    StringComparison.Ordinal))
            {
                reasons.Add("quest_harvest_required_item_parameter_drifted");
            }

            return reasons.ToArray();
        }

        private static bool ItemHarvestIdentityMatches(
            string itemId,
            string qualifiedItemId,
            string requiredItemId)
        {
            var normalized = requiredItemId.StartsWith("(O)", StringComparison.Ordinal)
                ? requiredItemId[3..]
                : requiredItemId;
            return string.Equals(itemId, requiredItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(qualifiedItemId, requiredItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(qualifiedItemId, "(O)" + normalized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
