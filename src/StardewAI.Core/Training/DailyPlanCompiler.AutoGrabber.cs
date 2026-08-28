using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> AutoGrabberCollectionSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "collect_auto_grabber_contents", 0),
                Kind = "collect_auto_grabber_contents",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_base_auto_grabber_still_present=true",
                    "native_held_chest_nonempty=true",
                    "at_least_one_projected_stack_fits_inventory=true",
                    "active_menu=none"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "candidate_selects_one_exact_auto_grabber",
                    "compiler_rebinds_contents_inventory_safe_slot_and_adjacent_stand_from_fresh_snapshot",
                    "shared_native_object_interaction_movement_only",
                    "native_ItemGrabMenu_left_clicks_only",
                    "never_directly_mutate_held_chest_or_player_inventory_in_production",
                    "leave_nonfitting_stacks_unchanged"
                },
                FailurePolicy = new[] { "close_owned_menu_restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
