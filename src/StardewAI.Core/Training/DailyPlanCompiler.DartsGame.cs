using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> DartsGameSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_darts", 0),
                Kind = "play_darts",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "pirate_night_and_live_DartsGame_action=true",
                    "darts_game_fresh_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_DartsGame_action_and_yes_response_only",
                    "native_mouse_position_and_left_button_charge_release_only",
                    "native_Darts_score_flight_dialogue_and_limited_nut_drop_only",
                    "no_direct_score_dart_count_timer_rng_reward_or_progress_mutation"
                },
                FailurePolicy = new[] { "release_native_input_then_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
