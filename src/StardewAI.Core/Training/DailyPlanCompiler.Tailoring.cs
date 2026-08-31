using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> TailoringSteps(PolicyEventCandidatePrediction candidate)
    {
        var names = new[]
        {
            "tailoring_candidate_id", "tailoring_operation", "tailoring_purpose", "tailoring_recipe_id",
            "tailoring_source_id", "tailoring_source_kind", "location_id", "interaction_tile_x", "interaction_tile_y",
            "stand_tile_x", "stand_tile_y", "left_source_id", "left_state_json", "right_source_id", "right_state_json",
            "tailoring_spend_left_count", "tailoring_spend_right_count", "tailoring_output_contract_kind",
            "expected_output_state_json", "random_outcome_contract_json", "tailoring_tailored_counts_before_json",
            "tailoring_marks_tailored_item", "tailoring_native_contract", "max_movement_tiles"
        };
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "tailor_item", 0),
                Kind = "tailor_item",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = Math.Max(1, TicksToMinutes(candidate.EstimatedTicks)),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_live_tailoring_endpoint_inputs_recipe_and_output_domain_rebound=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_TailoringMenu_clicks_only",
                    "one_tailoring_operation_then_fresh_snapshot",
                    "dye_and_prismatic_character_customization_excluded",
                    "no_direct_inventory_tailoredItems_boot_stat_clothing_or_rng_mutation",
                    "runtime_verify_inputs_consumption_output_domain_tailored_history_and_leftover_collection"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = names.Select(name => Parameter(name, CandidateParameter(candidate, name))).ToArray()
            }
        };
    }
}
