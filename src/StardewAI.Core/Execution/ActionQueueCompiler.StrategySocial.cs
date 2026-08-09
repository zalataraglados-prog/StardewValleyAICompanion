using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Goals;
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
        private static (string[] BlockingReasons, CandidateDirection? ValidatedDirection) ValidateStrategyPlan(
            SmallModelAction action,
            OptionSpec? option,
            SnapshotEnvelope snapshot,
            string executionMode)
        {
            if (!IsStrategyPlanOption(option, action))
            {
                return (Array.Empty<string>(), null);
            }

            var directionId = ReadParameter(action, "direction_id");
            if (string.IsNullOrWhiteSpace(directionId))
            {
                var blockReason = ReadParameter(action, "block_reason");
                var reason = string.IsNullOrWhiteSpace(blockReason)
                    ? "strategy_direction_id_required"
                    : "strategy_direction_failed_closed:" + blockReason;
                return (new[] { reason }, null);
            }

            if (string.Equals(directionId, "auto_select_best_direction", StringComparison.Ordinal))
            {
                return (new[] { "strategy_auto_select_best_direction_rejected:direction_must_be_selected_by_snapshot_aware_policy" }, null);
            }

            var validationModel = ReadParameter(action, "requires_direction_selection");
            if (string.Equals(validationModel, "failed_no_eligible_candidate", StringComparison.Ordinal))
            {
                return (new[] { "strategy_no_eligible_candidate_available" }, null);
            }

            var goal = ReadParameter(action, "strategic_goal");
            if (goal is null)
            {
                return (new[] { "strategy_strategic_goal_missing:strategic_goal_parameter_required" }, null);
            }
            if (!string.Equals(goal, GrandpaEvaluationGoalDefinition.StrategicGoal, StringComparison.Ordinal))
            {
                return (new[] { "strategy_strategic_goal_invalid:strategic_goal_must_be_" + GrandpaEvaluationGoalDefinition.StrategicGoal + "_but_was_" + goal }, null);
            }

            var worldModel = new WorldModelProjector().Project(snapshot, goal, executionMode);
            var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
            var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);

            var currentCandidate = sample.CandidateDirections
                .FirstOrDefault(c => string.Equals(c.DirectionId, directionId, StringComparison.Ordinal));

            if (currentCandidate is null)
            {
                return (new[] { "strategy_direction_absent:direction_id_" + directionId + "_not_in_current_snapshot_candidate_set" }, null);
            }

            var reasons = new System.Collections.Generic.List<string>();

            if (!currentCandidate.Known)
            {
                reasons.Add("strategy_direction_not_known:direction_has_unknown_factors_in_current_snapshot");
            }

            if (currentCandidate.Blocked)
            {
                reasons.Add("strategy_direction_blocked:direction_is_blocked_in_current_snapshot");
            }

            if (currentCandidate.PotentialPoints <= 0)
            {
                reasons.Add("strategy_direction_zero_potential:direction_has_no_expected_grandpa_points_gain");
            }

            var modelDomain = ReadParameter(action, "direction_domain") ?? string.Empty;
            if (!string.Equals(modelDomain, currentCandidate.Domain, StringComparison.Ordinal))
            {
                reasons.Add("strategy_direction_domain_mismatch:model=" + modelDomain + ";live=" + currentCandidate.Domain);
            }

            var modelPotential = ReadIntParameter(action, "potential_points");
            if (!modelPotential.HasValue || modelPotential.Value != currentCandidate.PotentialPoints)
            {
                reasons.Add("strategy_potential_points_mismatch:model=" + (modelPotential?.ToString() ?? "null") + ";live=" + currentCandidate.PotentialPoints);
            }

            var modelPriority = ReadDoubleParameter(action, "priority_score");
            if (!modelPriority.HasValue || Math.Abs(modelPriority.Value - currentCandidate.PriorityScore) > 0.0001)
            {
                reasons.Add("strategy_priority_score_mismatch:model=" + (modelPriority?.ToString(CultureInfo.InvariantCulture) ?? "null") + ";live=" + currentCandidate.PriorityScore.ToString(CultureInfo.InvariantCulture));
            }

            var modelFeedbackKey = ReadParameter(action, "feedback_key") ?? string.Empty;
            if (!string.Equals(modelFeedbackKey, currentCandidate.FeedbackKey, StringComparison.Ordinal))
            {
                reasons.Add("strategy_feedback_key_mismatch:model=" + modelFeedbackKey + ";live=" + currentCandidate.FeedbackKey);
            }

            var expectedRequiredMinutes = GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(currentCandidate);
            var modelRequiredMinutes = ReadIntParameter(action, "required_minutes");
            if (!modelRequiredMinutes.HasValue || modelRequiredMinutes.Value != expectedRequiredMinutes)
            {
                reasons.Add("strategy_required_minutes_mismatch:model=" + (modelRequiredMinutes?.ToString() ?? "null") + ";live=" + expectedRequiredMinutes);
            }

            var modelOptionalMinutes = ReadIntParameter(action, "optional_minutes");
            if (!modelOptionalMinutes.HasValue)
            {
                reasons.Add("strategy_optional_minutes_missing:optional_minutes_parameter_required");
            }
            else if (modelOptionalMinutes.Value != 0)
            {
                reasons.Add("strategy_optional_minutes_must_be_zero:model=" + modelOptionalMinutes.Value);
            }

            var modelPreconditions = ReadParameter(action, "hard_preconditions");
            if (!string.IsNullOrWhiteSpace(modelPreconditions))
            {
                reasons.Add("strategy_hard_preconditions_not_verifiable:model_value_rejected");
            }

            var modelResourceBudget = ReadParameter(action, "resource_budget");
            if (!string.IsNullOrWhiteSpace(modelResourceBudget))
            {
                reasons.Add("strategy_resource_budget_not_verifiable:model_value_rejected");
            }

            var modelExecutorHandoff = ReadParameter(action, "executor_handoff_option");
            if (!string.IsNullOrWhiteSpace(modelExecutorHandoff))
            {
                reasons.Add("strategy_executor_handoff_not_verifiable:model_value_rejected");
            }

            if (reasons.Count > 0)
            {
                return (reasons.ToArray(), null);
            }

            return (Array.Empty<string>(), currentCandidate);
        }

        private static string[] ValidateSocialPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "social.talk_npc" &&
                action.OptionId != "social.gift_npc" &&
                action.OptionId != "social.advance_partnership")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string> { "social_requires_daily_plan_compilation" };
            var npcName = ReadParameter(action, "npc_name") ?? ReadParameter(action, "target_npc") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(npcName))
            {
                reasons.Add("social_npc_name_required");
            }

            if (action.OptionId == "social.gift_npc" || action.OptionId == "social.advance_partnership")
            {
                if (!ReadIntParameter(action, "slot_index").HasValue)
                {
                    reasons.Add("social_gift_slot_index_required");
                }
                if (string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")))
                {
                    reasons.Add("social_gift_qualified_item_id_required");
                }
                if (action.OptionId == "social.advance_partnership" &&
                    ReadParameter(action, "partnership_action_kind") is not ("bouquet" or "propose_marriage" or "propose_roommate"))
                {
                    reasons.Add("partnership_action_kind_required");
                }
            }

            if (!string.IsNullOrWhiteSpace(npcName))
            {
                var candidate = SocialCandidateBuilder.FindMatching(snapshot, action);
                if (candidate is null)
                {
                    reasons.Add("social_current_state_candidate_not_available");
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateSocialInteractPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.social_interact")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var actionKind = ReadParameter(action, "social_action_kind") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(npcName))
            {
                reasons.Add("social_npc_name_required");
            }
            if (actionKind is not ("talk" or "gift" or "bouquet" or "propose_marriage" or "propose_roommate"))
            {
                reasons.Add("social_action_kind_not_supported");
            }
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("social_target_tile_required");
            }
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            if (!standX.HasValue || !standY.HasValue)
            {
                reasons.Add("social_stand_tile_required");
            }
            if (targetX.HasValue && targetY.HasValue && standX.HasValue && standY.HasValue)
            {
                if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1)
                {
                    reasons.Add("social_stand_not_adjacent_to_npc");
                }
            }
            else
            {
                reasons.Add("social_candidate_stand_npc_evidence_missing");
            }
            if (actionKind != "talk")
            {
                if (!ReadIntParameter(action, "slot_index").HasValue)
                {
                    reasons.Add("social_gift_slot_index_required");
                }
                if (string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")))
                {
                    reasons.Add("social_gift_qualified_item_id_required");
                }
            }
            if (!string.IsNullOrWhiteSpace(npcName) && !string.IsNullOrWhiteSpace(actionKind) &&
                standX.HasValue && standY.HasValue)
            {
                var optionId = actionKind == "gift"
                    ? "social.gift_npc"
                    : actionKind == "talk"
                        ? "social.talk_npc"
                        : "social.advance_partnership";
                var probe = new SmallModelAction
                {
                    ActionId = "social.interact.probe",
                    OptionId = optionId,
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "npc_name", Value = npcName }
                    }
                };
                if (actionKind != "talk")
                {
                    var slotIndex = ReadIntParameter(action, "slot_index") ?? 0;
                    var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
                    probe.Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "npc_name", Value = npcName },
                        new SmallModelActionParameter { Name = "slot_index", Value = slotIndex.ToString() },
                        new SmallModelActionParameter { Name = "qualified_item_id", Value = qualifiedItemId },
                        new SmallModelActionParameter { Name = "partnership_action_kind", Value = actionKind == "gift" ? string.Empty : actionKind }
                    };
                }
                var candidate = SocialCandidateBuilder.FindMatching(snapshot, probe);
                if (candidate is null)
                {
                    reasons.Add("social_current_state_candidate_not_available_for_executor");
                }
                else
                {
                    if (standX.HasValue && standY.HasValue && targetX.HasValue && targetY.HasValue)
                    {
                        var candidateStandXStr = SocialCandidateBuilder.CandidateParameter(candidate, "stand_tile_x");
                        var candidateStandYStr = SocialCandidateBuilder.CandidateParameter(candidate, "stand_tile_y");
                        var candidateNpcXStr = SocialCandidateBuilder.CandidateParameter(candidate, "npc_tile_x");
                        var candidateNpcYStr = SocialCandidateBuilder.CandidateParameter(candidate, "npc_tile_y");
                        if (!int.TryParse(candidateStandXStr, out var candidateStandX) ||
                            !int.TryParse(candidateStandYStr, out var candidateStandY) ||
                            !int.TryParse(candidateNpcXStr, out var candidateNpcX) ||
                            !int.TryParse(candidateNpcYStr, out var candidateNpcY) ||
                            candidateStandX != standX.Value || candidateStandY != standY.Value ||
                            candidateNpcX != targetX.Value || candidateNpcY != targetY.Value)
                        {
                            reasons.Add("social_candidate_stand_npc_mismatch");
                        }
                    }
                }
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("social_interact_menu_must_be_clear");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool IsStrategyPlanOption(OptionSpec? option, SmallModelAction action)
        {
            return option is not null &&
                option.CompilerResponsibility == CompilerResponsibilities.PlanValidation &&
                action.OptionId == "strategy.grandpa_progress";
        }

        private static StrategyPlanStep[] CompileStrategyPlan(CandidateDirection validatedDirection)
        {
            var requiredMinutes = GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(validatedDirection);

            return new[]
            {
                new StrategyPlanStep
                {
                    StepId = "strategy_step." + Guid.NewGuid().ToString("N"),
                    DirectionId = validatedDirection.DirectionId,
                    Domain = validatedDirection.Domain,
                    PotentialPoints = validatedDirection.PotentialPoints,
                    PriorityScore = validatedDirection.PriorityScore,
                    FeedbackKey = validatedDirection.FeedbackKey,
                    RequiredMinutes = requiredMinutes,
                    OptionalMinutes = 0,
                    HardPreconditions = Array.Empty<string>(),
                    ResourceBudget = Array.Empty<string>(),
                    ExecutorHandoffOption = string.Empty
                }
            };
        }

    }
}
