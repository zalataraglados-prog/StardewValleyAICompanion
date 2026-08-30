using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CalicoStatueSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "activate_calico_statue", 0),
                Kind = "activate_calico_statue",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "desert_festival_skull_cavern=true",
                    "current_floor_calico_statue_unactivated=true",
                    "accepted_projected_effect_identity_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "small_model_accepts_only_the_exact_projected_effect_id",
                    "compiler_replays_day_save_seed_and_rebinds_live_tile_from_fresh_snapshot",
                    "shared_bfs_then_one_native_MineShaft_checkAction_only",
                    "never_directly_write_rating_effects_rewards_health_stamina_buff_tile_or_rng"
                },
                FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
