using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PrairieKingSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_prairie_king", 0),
                Kind = "play_prairie_king",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 30,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "completedPrairieKingWithoutDying=0",
                    "prairie_king_fresh_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "ai_actor_only_timed_equivalent",
                    "native_Saloon_arcade_entry_and_optional_NewGame_response",
                    "native_AbigailGame_usePowerup_minus3_phase1_settlement_only",
                    "no_direct_stats_mail_achievement_or_inventory_mutation",
                    "native_perfect_proxy_is_post_training_player_command_only"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
