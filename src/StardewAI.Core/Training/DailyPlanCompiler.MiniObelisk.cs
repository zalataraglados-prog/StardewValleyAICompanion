using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MiniObeliskSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "use_mini_obelisk", 0),
                Kind = "use_mini_obelisk",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "source_is_exact_base_member_of_native_first_pair=true",
                    "native_destination_is_other_endpoint=true",
                    "native_landing_available=true",
                    "active_menu=none"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "compiler_rebinds_raw_native_pair_order_from_fresh_snapshot",
                    "compiler_rebinds_farther_destination_from_exact_interaction_stand",
                    "compiler_rebinds_first_down_left_right_up_native_landing",
                    "shared_native_object_interaction_movement_only",
                    "one_native_GameLocation_checkAction_only",
                    "never_directly_mutate_player_position_in_production",
                    "fresh_snapshot_required_after_native_teleport"
                },
                FailurePolicy = new[] { "restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
