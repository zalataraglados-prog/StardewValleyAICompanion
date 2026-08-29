using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FairFishingGameSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_fair_fishing_game", 0),
                Kind = "play_fair_fishing_game",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "fair_fishing_projection_still_matches=true",
                    "remaining_automatic_star_token_demand>0"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "one_native_50g_100_second_session_only",
                    "native_Event_dialogue_and_FishingGame_inputs_only",
                    "reuse_shared_movement_and_predictive_legal_bobber_input",
                    "no_direct_money_score_fish_timer_reward_or_inventory_mutation"
                },
                FailurePolicy = new[] { "release_all_inputs_escape_native_minigame_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
