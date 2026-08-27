using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> SlimeBallCollectionSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "collect_slime_ball", 0),
                Kind = "collect_slime_ball",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_natural_fragility_2_slime_ball_still_present=true",
                    "empty_toolbar_slot_available=true",
                    "destructive_object_trap_preamble=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "candidate_selects_one_exact_slime_ball",
                    "compiler_rebinds_seed_expected_outputs_empty_slot_and_adjacent_stand_from_fresh_snapshot",
                    "one_native_GameLocation_checkAction_only",
                    "never_directly_remove_object_or_create_output_in_production",
                    "native_debris_collection_is_delegated_to_shared_executor.pickup_debris"
                },
                FailurePolicy = new[] { "stop_restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
