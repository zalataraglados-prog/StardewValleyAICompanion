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
            if (action.OptionId is not (
                    "executor.harvest_crop" or
                    "executor.collect_machine_output" or
                    "executor.pickup_debris") ||
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
            if (objective is null ||
                !string.Equals(objective.RuntimeType, "CollectObjective", StringComparison.Ordinal))
            {
                reasons.Add("special_order_collect_objective_drifted");
            }
            else if (!SpecialOrderCollectActionTargetMatches(
                action,
                snapshot,
                tagSets))
            {
                reasons.Add("special_order_collect_item_context_tags_drifted");
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

        private static bool SpecialOrderCollectActionTargetMatches(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string[] tagSets)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return false;
            }

            if (action.OptionId == "executor.harvest_crop")
            {
                var crop = HarvestCropAt(snapshot, targetX.Value, targetY.Value);
                return crop is not null &&
                    string.Equals(
                        ReadString(crop.Value, "harvest_method"),
                        "Grab",
                        StringComparison.OrdinalIgnoreCase) &&
                    QuestContextTagMatcher.Matches(
                        ReadQuestStringArray(crop.Value, "harvest_context_tags"),
                        tagSets);
            }
            if (action.OptionId == "executor.pickup_debris")
            {
                var debris = DebrisAt(
                    snapshot,
                    ReadParameter(action, "target_location"),
                    targetX.Value,
                    targetY.Value,
                    ReadIntParameter(action, "debris_index"));
                return debris is not null &&
                    debris.Value.TryGetProperty("item", out var debrisItem) &&
                    debrisItem.ValueKind == JsonValueKind.Object &&
                    QuestContextTagMatcher.Matches(
                        ReadQuestStringArray(debrisItem, "context_tags"),
                        tagSets);
            }

            var machine = MachineAt(
                snapshot,
                ReadParameter(action, "target_location"),
                targetX.Value,
                targetY.Value);
            if (machine is null ||
                !machine.Value.TryGetProperty("held_item", out var heldItem) ||
                heldItem.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            return QuestContextTagMatcher.Matches(
                ReadQuestStringArray(heldItem, "context_tags"),
                tagSets);
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
