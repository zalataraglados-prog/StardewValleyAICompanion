using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FairSlingshotGameSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_fair_slingshot_game", 0),
                Kind = "play_fair_slingshot_game",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "fair_slingshot_projection_still_matches=true",
                    "remaining_automatic_star_token_demand>0"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "one_native_50g_50_second_session_only",
                    "native_Event_dialogue_and_TargetGame_inputs_only",
                    "reuse_shared_movement_and_slingshot_aim_patch_with_predictive_intercept",
                    "no_direct_money_target_score_accuracy_reward_timer_or_inventory_mutation"
                },
                FailurePolicy = new[] { "release_native_slingshot_escape_minigame_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
