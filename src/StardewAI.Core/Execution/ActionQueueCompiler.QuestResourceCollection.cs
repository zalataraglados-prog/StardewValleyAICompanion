using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
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
                else if (action.OptionId is "executor.harvest_bush" or "executor.harvest_ginger" or
                    "executor.harvest_fruit_tree" or "executor.harvest_tree_product" or "executor.rummage_garbage")
                {
                    if (!string.Equals(
                        ReadParameter(action, "qualified_item_id"),
                        qualifiedRequired,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add("quest_resource_foraging_source_drop_drifted");
                    }
                }
                else if (action.OptionId == "executor.harvest_crop")
                {
                    var targetX = ReadIntParameter(action, "target_tile_x");
                    var targetY = ReadIntParameter(action, "target_tile_y");
                    var crop = targetX.HasValue && targetY.HasValue
                        ? HarvestCropAt(snapshot, targetX.Value, targetY.Value)
                        : null;
                    if (crop is null ||
                        !string.Equals(
                            ReadString(crop.Value, "harvest_method"),
                            "Scythe",
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(
                            ReadString(crop.Value, "harvest_item_qualified_id"),
                            qualifiedRequired,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add("quest_resource_scythe_crop_source_drifted");
                    }
                }
                else if (action.OptionId == "executor.harvest_giant_crop")
                {
                    var targetX = ReadIntParameter(action, "target_tile_x");
                    var targetY = ReadIntParameter(action, "target_tile_y");
                    var giantCrop = targetX.HasValue && targetY.HasValue
                        ? GiantCropResourceClumpAt(snapshot, targetX.Value, targetY.Value)
                        : null;
                    if (!giantCrop.HasValue ||
                        !ProjectedOutputContainsItem(
                            ReadString(giantCrop.Value, "giant_crop_guaranteed_outputs_json"),
                            qualifiedRequired))
                    {
                        reasons.Add("quest_resource_giant_crop_source_drifted");
                    }
                }
                else if (action.OptionId == "executor.break_current_location_resource_clump")
                {
                    var anchorX = ReadIntParameter(action, "resource_clump_tile_x");
                    var anchorY = ReadIntParameter(action, "resource_clump_tile_y");
                    var clumps = ReadStateFieldValue(snapshot, "current_location", "resource_clumps");
                    var clump = anchorX.HasValue && anchorY.HasValue &&
                        clumps.HasValue && clumps.Value.ValueKind == System.Text.Json.JsonValueKind.Array
                            ? clumps.Value.EnumerateArray().FirstOrDefault(row =>
                                ReadInt(row, "tile_x") == anchorX.Value &&
                                ReadInt(row, "tile_y") == anchorY.Value)
                            : default;
                    if (clump.ValueKind != System.Text.Json.JsonValueKind.Object ||
                        !ProjectedOutputContainsItem(
                            ReadString(clump, "expected_core_output_items_json"),
                            qualifiedRequired))
                    {
                        reasons.Add("quest_resource_current_location_clump_source_drifted");
                    }
                }
                else if (action.OptionId == "executor.catch_fish")
                {
                    if (!ProjectedOutputContainsItem(
                            ReadParameter(action, "outcome_distribution_json") ?? string.Empty,
                            qualifiedRequired))
                    {
                        reasons.Add("quest_resource_fishing_source_distribution_drifted");
                    }
                }
                else if (action.OptionId is
                    "executor.combat_monster" or
                    "executor.shoot_monster")
                {
                    if (!MonsterDropSourceMatches(action, snapshot, qualifiedRequired))
                    {
                        reasons.Add("quest_resource_monster_drop_source_drifted");
                    }
                }
                else if (action.OptionId == "executor.load_machine_input")
                {
                    if (!TaskMachineInputPredictedOutputMatches(
                            action,
                            snapshot,
                            qualifiedRequired,
                            Array.Empty<string>()))
                    {
                        reasons.Add("quest_resource_machine_input_source_drifted");
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
