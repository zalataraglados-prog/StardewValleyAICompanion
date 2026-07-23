using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileSteps(SmallModelAction action, SnapshotEnvelope snapshot, OptionSpec? option)
        {
            if (option is null)
            {
                return Array.Empty<CompiledActionStep>();
            }

            if (option.CompilerResponsibility != CompilerResponsibilities.FullActionExpansion)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return ActionStepCompilers.TryGetValue(action.OptionId, out var compiler)
                ? compiler(action, snapshot)
                : Array.Empty<CompiledActionStep>();
        }

        private static CompiledActionStep[] CompileCloseMenuStep(SnapshotEnvelope snapshot)
        {
            var type = ActiveMenuType(snapshot);
            return new[]
            {
                Step("close_menu", string.IsNullOrWhiteSpace(type) ? "active_menu:none" : "active_menu:" + type, "menus.active_menu.is_open=false", 10)
            };
        }

        private static CompiledActionStep[] CompileCatchFishStep(SmallModelAction action)
        {
            var location = ReadParameter(action, "location_id") ?? ReadParameter(action, "target_location") ?? string.Empty;
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var bobberX = ReadIntParameter(action, "bobber_tile_x");
            var bobberY = ReadIntParameter(action, "bobber_tile_y");
            var rodSlot = ReadIntParameter(action, "rod_slot_index");
            if (string.IsNullOrWhiteSpace(location) || !standX.HasValue || !standY.HasValue ||
                !bobberX.HasValue || !bobberY.HasValue || !rodSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "catch_fish",
                    location + ":stand(" + standX + "," + standY + "):bobber(" + bobberX + "," + bobberY + "):rod_slot=" + rodSlot,
                    "fishing_attempt_completed_with_observed_catch_or_precise_block_reason",
                     Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 30) * 60))
            };
        }

        private static CompiledActionStep[] CompileCoolVolcanoLavaStep(SmallModelAction action)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var wateringCanSlot = ReadIntParameter(action, "watering_can_slot_index");
            if (!targetX.HasValue || !targetY.HasValue || !wateringCanSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "cool_volcano_lava",
                    "target(" + targetX.Value + "," + targetY.Value + "):watering_can_slot=" + wateringCanSlot.Value,
                    "volcano.tiles.cooled_lava_tiles contains target",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static CompiledActionStep[] CompileVolcanoNativePrimitiveStep(SmallModelAction action)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var kind = action.OptionId switch
            {
                "executor.break_volcano_stone" => "break_volcano_stone",
                "executor.break_volcano_container" => "break_volcano_container",
                "executor.combat_volcano_monster" => "combat_volcano_monster",
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(kind))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    kind,
                    "target(" + targetX.Value + "," + targetY.Value + ")",
                    action.OptionId == "executor.combat_volcano_monster"
                        ? "volcano.monsters target absent_or_health_zero"
                        : "volcano.objects target absent",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static CompiledActionStep[] CompileSocialInteractStep(SmallModelAction action)
        {
            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var actionKind = ReadParameter(action, "social_action_kind") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(actionKind))
            {
                actionKind = actionKind == "talk" || actionKind == "gift" ? actionKind : string.Empty;
            }
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(actionKind) ||
                !targetX.HasValue || !targetY.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var target = "social:" + actionKind + ":" + npcName + ":tile(" + targetX + "," + targetY + ")";
            return new[]
            {
                Step(
                    "social_interact",
                    target,
                    "social_native_execution_attempted_with_observed_outcome",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static CompiledActionStep[] CompileQuestNpcInteractStep(SmallModelAction action)
        {
            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var interactionKind = ReadParameter(action, "quest_interaction_kind") ?? string.Empty;
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (string.IsNullOrWhiteSpace(npcName) ||
                interactionKind is not ("report" or "offer_item") ||
                !targetX.HasValue ||
                !targetY.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "quest_npc_interact",
                    "quest:" + interactionKind + ":" + npcName + ":tile(" + targetX + "," + targetY + ")",
                    "matching_live_quest_or_special_order_objective_advanced_by_native_npc_interaction",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static SocialPlanEnvelope? CompileSocialPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "social.talk_npc" && action.OptionId != "social.gift_npc")
            {
                return null;
            }

            var candidate = SocialCandidateBuilder.FindMatching(snapshot, action);
            var evidence = candidate?.Parameters ?? Array.Empty<SmallModelActionParameter>();
            return new SocialPlanEnvelope
            {
                ActionKind = action.OptionId == "social.talk_npc" ? "talk" : "gift",
                RequestedNpcName = ReadParameter(action, "npc_name") ?? ReadParameter(action, "target_npc") ?? string.Empty,
                RequestedSlotIndex = ReadIntParameter(action, "slot_index"),
                RequestedQualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty,
                LiveLegalityEvidence = evidence
                    .Where(parameter => parameter.Name is "npc_name" or "slot_index" or "qualified_item_id" or "item_quality" or "item_stack_before" or "gift_taste" or "friendship_row_exists_before" or "gift_updates_normal_limits" or "gift_side_effect_risk" or "expected_talked_to_today_before" or "social_legality_evidence")
                    .ToArray(),
                TimeRouteConstraints = evidence
                    .Where(parameter => parameter.Name is "target_location" or "npc_tile_x" or "npc_tile_y" or "stand_tile_x" or "stand_tile_y" or "route_distance_tiles" or "route_distance_ticks" or "native_interaction_planner_budget_ticks")
                    .Concat(new[]
                    {
                        Parameter("duration", "planner_budget_route_distance_plus_native_interaction_ticks"),
                        Parameter("future_schedule_windows", "unavailable_in_this_slice")
                    })
                    .ToArray(),
                ExpectedDeterministicOutcome = evidence
                    .Where(parameter => parameter.Name is "expected_friendship_delta" or "item_stack_before" or "expected_talked_to_today_before")
                    .Concat(new[]
                    {
                        Parameter("result_verified_at_runtime", "true"),
                        Parameter("compiled_primitive_path", "executor.social_interact")
                    })
                    .ToArray(),
                TrainingRecordingContract = SocialTrainingRecordingContract()
            };
        }

        private static string[] SocialTrainingRecordingContract()
        {
            return new[]
            {
                "item_before_after_and_decrement",
                "friendship_points_before_after_delta",
                "talked_and_gift_counters_before_after",
                "dialogue_menu_event_side_effects",
                "npc_and_player_location_tick_time",
                "accepted_rejected_or_blocked_category",
                "primitive_verification",
                "freshness_state_hash",
                "calibration_vs_policy_label"
            };
        }

        private static QuestPlanEnvelope? CompileQuestPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "quest.advance")
            {
                return null;
            }

            var envelope = new QuestPlanEnvelope
            {
                TimeEstimate = "unknown",
                EnergyCost = "unknown",
                ExecutorBlockReason = "quest_requires_typed_daily_candidate_binding"
            };

            var candidateId = ReadParameter(action, "candidate_id");
            var questId = ReadParameter(action, "quest_id");
            var questKey = ReadParameter(action, "quest_key");
            var runtimeType = ReadParameter(action, "candidate_runtime_type");
            var nextAction = ReadParameter(action, "candidate_next_action");
            var modelTargetNpc = ReadParameter(action, "required_target_npc");
            var modelTargetLocation = ReadParameter(action, "required_target_location");
            var modelItemId = ReadParameter(action, "required_item_id");
            var modelTargetCountStr = ReadParameter(action, "required_target_count");
            var modelCurrentCountStr = ReadParameter(action, "current_progress_count");

            var activeQuests = ReadStateFieldValue(snapshot, "quests", "active_quests");
            var specialOrders = ReadStateFieldValue(snapshot, "quests", "special_orders");

            var rawActiveQuests = activeQuests.HasValue && activeQuests.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<QuestProgressRef[]>(activeQuests.Value.GetRawText()) ?? Array.Empty<QuestProgressRef>()
                : Array.Empty<QuestProgressRef>();

            var rawSpecialOrders = specialOrders.HasValue && specialOrders.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(specialOrders.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();

            var ordinaryCandidates = QuestCandidateBuilder.BuildOrdinaryCandidates(rawActiveQuests);
            var orderCandidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(rawSpecialOrders);
            var allCandidates = ordinaryCandidates.Concat(orderCandidates).ToArray();

            var suppliedIdentities = new List<string>();
            if (!string.IsNullOrWhiteSpace(candidateId)) suppliedIdentities.Add("candidate_id=" + candidateId);
            if (!string.IsNullOrWhiteSpace(questId)) suppliedIdentities.Add("quest_id=" + questId);
            if (!string.IsNullOrWhiteSpace(questKey)) suppliedIdentities.Add("quest_key=" + questKey);

            if (suppliedIdentities.Count == 0)
            {
                envelope.ExecutorBlockReason = "quest_missing_identity";
                return envelope;
            }

            var matchedCandidates = allCandidates.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(candidateId))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.CandidateId, candidateId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(questId))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.QuestId, questId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(questKey))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.QuestKey, questKey, StringComparison.Ordinal));
            }

            var matchList = matchedCandidates.ToArray();

            if (matchList.Length == 0)
            {
                envelope.ExecutorBlockReason = "quest_candidate_not_found:" + string.Join(";", suppliedIdentities);
                return envelope;
            }

            if (matchList.Length > 1)
            {
                envelope.ExecutorBlockReason = "quest_candidate_ambiguous:" + string.Join(";", suppliedIdentities) + ";matches=" + string.Join(",", matchList.Select(c => c.CandidateId));
                return envelope;
            }

            var match = matchList[0];

            if (!string.IsNullOrWhiteSpace(runtimeType) && !string.Equals(match.RuntimeType, runtimeType, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_runtime_type_mismatch:model=" + runtimeType + ";live=" + match.RuntimeType;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(nextAction) && !string.Equals(match.NextActionCategory, nextAction, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_next_action_mismatch:model=" + nextAction + ";live=" + match.NextActionCategory;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelTargetNpc) && !string.Equals(match.RequiredTargetNpc, modelTargetNpc, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_target_npc_mismatch:model=" + modelTargetNpc + ";live=" + match.RequiredTargetNpc;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelTargetLocation) && !string.Equals(match.RequiredTargetLocation, modelTargetLocation, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_target_location_mismatch:model=" + modelTargetLocation + ";live=" + match.RequiredTargetLocation;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelItemId) && !string.Equals(match.RequiredItemId, modelItemId, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_item_id_mismatch:model=" + modelItemId + ";live=" + match.RequiredItemId;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelTargetCountStr))
            {
                if (!int.TryParse(modelTargetCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelTargetCount))
                {
                    envelope.ExecutorBlockReason = "quest_target_count_malformed:value=" + modelTargetCountStr;
                    return envelope;
                }
                if (match.RequiredTargetCount != modelTargetCount)
                {
                    envelope.ExecutorBlockReason = "quest_target_count_mismatch:model=" + modelTargetCount + ";live=" + match.RequiredTargetCount;
                    return envelope;
                }
            }

            if (!string.IsNullOrWhiteSpace(modelCurrentCountStr))
            {
                if (!int.TryParse(modelCurrentCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelCurrentCount))
                {
                    envelope.ExecutorBlockReason = "quest_current_count_malformed:value=" + modelCurrentCountStr;
                    return envelope;
                }
                if (match.CurrentProgressCount != modelCurrentCount)
                {
                    envelope.ExecutorBlockReason = "quest_current_count_mismatch:model=" + modelCurrentCount + ";live=" + match.CurrentProgressCount;
                    return envelope;
                }
            }

            var modelSelectedObjectiveIndexStr = ReadParameter(action, "selected_objective_index");
            if (!string.IsNullOrWhiteSpace(modelSelectedObjectiveIndexStr))
            {
                if (!int.TryParse(modelSelectedObjectiveIndexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelSelectedObjectiveIndex))
                {
                    envelope.ExecutorBlockReason = "quest_selected_objective_index_malformed:value=" + modelSelectedObjectiveIndexStr;
                    return envelope;
                }
                if (match.SelectedObjectiveIndex != modelSelectedObjectiveIndex)
                {
                    envelope.ExecutorBlockReason = "quest_selected_objective_index_mismatch:model=" + modelSelectedObjectiveIndex + ";live=" + match.SelectedObjectiveIndex;
                    return envelope;
                }
            }

            envelope.SelectedCandidateId = match.CandidateId;
            envelope.SelectedQuestId = match.QuestId;
            envelope.SelectedQuestKey = match.QuestKey;
            envelope.SelectedRuntimeType = match.RuntimeType;
            envelope.Family = match.Family;
            envelope.NextActionCategory = match.NextActionCategory;
            envelope.RequiredTargetNpc = match.RequiredTargetNpc;
            envelope.RequiredTargetLocation = match.RequiredTargetLocation;
            envelope.RequiredItemId = match.RequiredItemId;
            envelope.RequiredTargetCount = match.RequiredTargetCount;
            envelope.CurrentProgressCount = match.CurrentProgressCount;
            envelope.SelectedObjectiveIndex = match.SelectedObjectiveIndex;
            envelope.LiveEvidence = new QuestCompilerEvidence
            {
                Candidate = match,
                RawActiveQuests = rawActiveQuests,
                RawSpecialOrders = rawSpecialOrders
            };

            return envelope;
        }

    }
}
