using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CalicoJackSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_calico_jack", 0),
                Kind = "play_calico_jack",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "missing_casino_rarecrow_currency_demand=true",
                    "calico_jack_seed_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_ClubCards_or_BlackJack_action_only",
                    "native_CalicoJack_Play_response_only",
                    "shared_exact_seed_replay_decision_model_only",
                    "one_native_round_then_quit",
                    "no_direct_card_rng_coin_or_result_mutation"
                },
                FailurePolicy = new[] { "close_calico_jack_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
