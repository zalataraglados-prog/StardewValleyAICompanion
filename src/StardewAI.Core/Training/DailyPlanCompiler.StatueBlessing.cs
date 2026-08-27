using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> StatueBlessingSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_statue_blessing", 0),
                Kind = "claim_statue_blessing",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "farming_mastery_unlocked=true",
                    "has_been_blessed_today=false",
                    "exact_daily_blessing_identity_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "small_model_emits_only_the_parameterless_claim_goal",
                    "compiler_rebinds_daily_blessing_statue_and_stand_from_fresh_snapshot",
                    "native_Object_CheckForActionOnBlessedStatue_only",
                    "never_directly_apply_or_remove_a_production_buff"
                },
                FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
