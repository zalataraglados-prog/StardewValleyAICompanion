using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> SlotsSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_slots", 0),
                Kind = "play_slots",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "missing_casino_rarecrow_currency_demand=true",
                    "slots_probability_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_ClubSlots_action_only",
                    "native_10_or_100_spin_button_only",
                    "one_native_spin_then_done",
                    "shared_rng_live_feedback_only",
                    "no_direct_rng_reel_coin_result_or_stat_mutation"
                },
                FailurePolicy = new[] { "close_slots_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
