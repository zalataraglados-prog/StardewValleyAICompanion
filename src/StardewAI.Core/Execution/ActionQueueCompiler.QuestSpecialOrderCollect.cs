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
                    "executor.pickup_debris" or
                    "executor.harvest_bush" or
                    "executor.harvest_ginger" or
                    "executor.harvest_giant_crop" or
                    "executor.break_current_location_resource_clump" or
                    "executor.catch_fish" or
                    "executor.combat_monster" or
                    "executor.shoot_monster") ||
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
            var targetStep = string.Equals(
                ReadParameter(action, "quest_acquisition_target_step"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var sourceStep = string.Equals(
                ReadParameter(action, "quest_acquisition_source_step"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var sourceOption = action.OptionId is
                    "executor.harvest_bush" or
                    "executor.harvest_ginger" or
                    "executor.harvest_giant_crop" or
                    "executor.break_current_location_resource_clump" or
                    "executor.catch_fish" or
                    "executor.combat_monster" or
                    "executor.shoot_monster" ||
                action.OptionId == "executor.harvest_crop" &&
                string.Equals(
                    ReadParameter(action, "harvest_method"),
                    "Scythe",
                    StringComparison.OrdinalIgnoreCase);
            if (objective is null ||
                !string.Equals(objective.RuntimeType, "CollectObjective", StringComparison.Ordinal))
            {
                reasons.Add("special_order_collect_objective_drifted");
            }
            else if (sourceOption &&
                !SpecialOrderCollectSourceMatches(action, snapshot, tagSets))
            {
                reasons.Add("special_order_collect_source_context_tags_drifted");
            }
            else if (!sourceOption &&
                !SpecialOrderCollectActionTargetMatches(action, snapshot, tagSets))
            {
                reasons.Add("special_order_collect_item_context_tags_drifted");
            }
            if (targetStep == sourceStep ||
                sourceOption != sourceStep)
            {
                reasons.Add("special_order_collect_acquisition_role_invalid");
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

        private static bool SpecialOrderCollectSourceMatches(
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

            if (action.OptionId is
                "executor.combat_monster" or
                "executor.shoot_monster")
            {
                return MonsterDropSourceMatchesContextTags(
                    action,
                    snapshot,
                    tagSets);
            }
            if (action.OptionId == "executor.harvest_bush")
            {
                var features = ReadStateFieldValue(
                    snapshot,
                    "current_location",
                    "large_terrain_features");
                var bush = features.HasValue &&
                    features.Value.ValueKind == JsonValueKind.Array
                        ? features.Value.EnumerateArray().FirstOrDefault(feature =>
                            ReadBool(feature, "is_bush") == true &&
                            ReadInt(feature, "tile_x") == targetX.Value &&
                            ReadInt(feature, "tile_y") == targetY.Value)
                        : default;
                return bush.ValueKind == JsonValueKind.Object &&
                    QuestContextTagMatcher.Matches(
                        ReadQuestStringArray(bush, "bush_output_context_tags"),
                        tagSets);
            }
            if (action.OptionId == "executor.catch_fish")
            {
                return ProjectedOutputContextTagsMatch(
                    ReadParameter(action, "outcome_distribution_json") ?? string.Empty,
                    tagSets);
            }
            if (action.OptionId == "executor.harvest_crop")
            {
                var crop = HarvestCropAt(snapshot, targetX.Value, targetY.Value);
                return crop is not null &&
                    string.Equals(
                        ReadString(crop.Value, "harvest_method"),
                        "Scythe",
                        StringComparison.OrdinalIgnoreCase) &&
                    QuestContextTagMatcher.Matches(
                        ReadQuestStringArray(crop.Value, "harvest_context_tags"),
                        tagSets);
            }
            if (action.OptionId == "executor.harvest_giant_crop")
            {
                var giantCrop = GiantCropResourceClumpAt(snapshot, targetX.Value, targetY.Value);
                return giantCrop.HasValue &&
                    ProjectedOutputContextTagsMatch(
                        ReadString(giantCrop.Value, "giant_crop_guaranteed_outputs_json"),
                        tagSets);
            }
            if (action.OptionId == "executor.break_current_location_resource_clump")
            {
                var anchorX = ReadIntParameter(action, "resource_clump_tile_x");
                var anchorY = ReadIntParameter(action, "resource_clump_tile_y");
                var clumps = ReadStateFieldValue(snapshot, "current_location", "resource_clumps");
                var clump = anchorX.HasValue && anchorY.HasValue &&
                    clumps.HasValue && clumps.Value.ValueKind == JsonValueKind.Array
                        ? clumps.Value.EnumerateArray().FirstOrDefault(row =>
                            ReadInt(row, "tile_x") == anchorX.Value &&
                            ReadInt(row, "tile_y") == anchorY.Value)
                        : default;
                return clump.ValueKind == JsonValueKind.Object &&
                    ProjectedOutputContextTagsMatch(
                        ReadString(clump, "expected_core_output_context_tag_sets_json"),
                        tagSets);
            }

            var terrainFeatures = ReadStateFieldValue(
                snapshot,
                "current_location",
                "terrain_features");
            var ginger = terrainFeatures.HasValue &&
                terrainFeatures.Value.ValueKind == JsonValueKind.Array
                    ? terrainFeatures.Value.EnumerateArray().FirstOrDefault(feature =>
                        ReadBool(feature, "is_ginger") == true &&
                        ReadInt(feature, "tile_x") == targetX.Value &&
                        ReadInt(feature, "tile_y") == targetY.Value)
                    : default;
            return ginger.ValueKind == JsonValueKind.Object &&
                QuestContextTagMatcher.Matches(
                    ReadQuestStringArray(ginger, "ginger_output_context_tags"),
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
