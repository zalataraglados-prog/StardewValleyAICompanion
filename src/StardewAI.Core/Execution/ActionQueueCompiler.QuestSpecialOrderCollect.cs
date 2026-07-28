using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateAttachedSpecialOrderCollectPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_crop" ||
                !string.Equals(
                    ReadParameter(action, "quest_next_action"),
                    "collect_items",
                    StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var candidateId = ReadParameter(action, "quest_candidate_id") ?? string.Empty;
            var family = ReadParameter(action, "quest_family") ?? string.Empty;
            var questKey = ReadParameter(action, "quest_key") ?? string.Empty;
            var runtimeType = ReadParameter(action, "quest_runtime_type") ?? string.Empty;
            var objectiveIndex = ReadIntParameter(action, "quest_objective_index");
            ValidateQuestIdentityAgainstSnapshot(
                snapshot,
                family,
                candidateId,
                string.Empty,
                questKey,
                runtimeType,
                objectiveIndex,
                ReadIntParameter(action, "quest_expected_current_count"),
                ReadIntParameter(action, "quest_expected_target_count"),
                reasons);
            if (!string.Equals(family, "special_order", StringComparison.Ordinal) ||
                !objectiveIndex.HasValue)
            {
                reasons.Add("special_order_collect_identity_invalid");
                return reasons.ToArray();
            }

            var objective = ReadSpecialOrderCollectObjective(snapshot, questKey, objectiveIndex.Value);
            var tagSets = objective?.PerTypeFields?.AcceptableContextTagSets ?? Array.Empty<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var crop = targetX.HasValue && targetY.HasValue
                ? HarvestCropAt(snapshot, targetX.Value, targetY.Value)
                : null;
            if (objective is null ||
                !string.Equals(objective.RuntimeType, "CollectObjective", StringComparison.Ordinal) ||
                crop is null ||
                !string.Equals(ReadString(crop.Value, "harvest_method"), "Grab", StringComparison.OrdinalIgnoreCase) ||
                !QuestContextTagMatcher.Matches(
                    ReadQuestStringArray(crop.Value, "harvest_context_tags"),
                    tagSets))
            {
                reasons.Add("special_order_collect_crop_or_context_tags_drifted");
            }
            if (!string.Equals(
                    ReadParameter(action, "quest_acquisition_target_step"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("special_order_collect_receipt_step_required");
            }

            var projectedTagSets = ReadParameter(
                action,
                "quest_acceptable_context_tag_sets_json") ?? string.Empty;
            if (!string.Equals(
                    projectedTagSets,
                    JsonSerializer.Serialize(tagSets),
                    StringComparison.Ordinal))
            {
                reasons.Add("special_order_collect_context_tag_sets_parameter_drifted");
            }
            return reasons.ToArray();
        }

        private static SpecialOrderObjectiveProgressRef? ReadSpecialOrderCollectObjective(
            SnapshotEnvelope snapshot,
            string questKey,
            int objectiveIndex)
        {
            var state = ReadStateFieldValue(snapshot, "quests", "special_orders");
            var orders = state.HasValue && state.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(state.Value.GetRawText()) ??
                    Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();
            var order = orders.SingleOrDefault(candidate =>
                string.Equals(candidate.QuestKey, questKey, StringComparison.Ordinal));
            return order is not null &&
                objectiveIndex >= 0 &&
                objectiveIndex < order.Objectives.Length
                    ? order.Objectives[objectiveIndex]
                    : null;
        }

        private static string[] ReadQuestStringArray(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToArray()
                    : Array.Empty<string>();
        }
    }
}
