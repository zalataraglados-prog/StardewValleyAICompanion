using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CraneGameSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_crane_game", 0),
                Kind = "play_crane_game",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_command=true",
                    "crane_game_fresh_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_CraneGame_action_and_yes_response_only",
                    "native_right_and_down_input_only",
                    "live_prize_physics_rebound_each_attempt",
                    "native_ItemGrabMenu_reward_transfer_only",
                    "no_direct_rng_money_prize_position_state_or_inventory_mutation"
                },
                FailurePolicy = new[] { "force_native_reward_conservation_cleanup_then_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
