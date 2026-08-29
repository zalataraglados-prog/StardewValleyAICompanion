using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FairWheelSpinSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "spin_fair_wheel", 0),
                Kind = "spin_fair_wheel",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "fair_wheel_projection_still_matches=true",
                    "remaining_automatic_star_token_demand>=2",
                    "current_festival_score>=2"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "one_native_WheelSpinGame_session_only",
                    "green_response_and_zero_luck_kelly_7_of_15_wager_capped_by_remaining_demand",
                    "both_native_random_win_and_loss_are_valid_training_outputs",
                    "fresh_snapshot_replan_after_stochastic_settlement",
                    "no_direct_rng_rotation_velocity_wager_festival_score_result_text_or_menu_mutation"
                },
                FailurePolicy = new[] { "wait_for_native_exit_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
