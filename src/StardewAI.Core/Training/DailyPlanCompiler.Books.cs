using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> ReadInventoryBookSteps(PolicyEventCandidatePrediction candidate)
        {
            var parameterNames = new[]
            {
                "slot_index", "qualified_item_id", "item_id", "book_runtime_type", "book_category",
                "book_stack_before", "book_stack_after", "book_native_branch", "book_native_branch_status",
                "book_context_tags_native_order_json", "book_matched_experience_tag",
                "expected_skill_experience_deltas_json", "expected_mastery_experience_delta",
                "book_skill_level_deltas_json", "book_new_levels_before_json", "book_new_levels_after_json",
                "book_native_feedback_callbacks",
                "skill_experience_projection_status", "skill_experience_condition",
                "book_stat_key", "book_stat_before", "book_stat_after",
                "read_a_book_mail_before", "read_a_book_mail_after",
                "well_read_achievement_before", "well_read_achievement_after",
                "well_read_achievement_will_unlock", "well_read_hatter_mail_before", "well_read_hatter_mail_after",
                "well_read_dialogue_event_seen_before", "well_read_dialogue_event_seen_after", "well_read_ui_sound_platform_callbacks",
                "cooking_recipes_added_json", "cooking_recipes_added_count"
            };
            var parameters = new List<SmallModelActionParameter>();
            foreach (var name in parameterNames)
            {
                parameters.Add(Parameter(name, CandidateParameter(candidate, name)));
            }
            foreach (var name in new[] { "skill_experience_skill_id", "skill_experience_on_success_min", "skill_experience_on_success_max" })
            {
                var value = CandidateParameter(candidate, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "read_inventory_book", 0),
                    Kind = "read_book",
                    TargetLocation = candidate.LocationId,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "player.inventory_slot_contains_exact_book=true", "native_book_use_gate=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "native_Object.performUseAction_only", "consume_exactly_one_after_native_success", "verify_all_projected_skill_stat_mail_and_recipe_deltas" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "read_inventory_book_settle", 1),
                    Kind = "wait_ticks",
                    WaitTicks = 75,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "previous_native_book_read_applied=true" },
                    ExpectedEffects = new[] { "native_book_read_animation_completed", "player.can_move=true" },
                    SafetyConstraints = new[] { "wait_only_for_native_book_animation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }
    }
}
