using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FairStrengthGameSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_fair_strength_game", 0),
                Kind = "play_fair_strength_game",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "fair_strength_projection_still_matches=true",
                    "remaining_automatic_star_token_demand=1"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "one_free_native_StrengthGame_session_only",
                    "exact_player_tile_x_29_and_buildings_540_entry",
                    "native_single_click_and_eight_frame_hammer_animation_only",
                    "maximum_power_reward_branch_only",
                    "no_direct_power_speed_timer_festival_score_dialogue_or_player_animation_mutation"
                },
                FailurePolicy = new[] { "close_native_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
