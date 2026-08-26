using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ClaimPotOfGoldSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_pot_of_gold", 0),
                Kind = "claim_pot_of_gold",
                TargetLocation = "Forest",
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_live_spring_17_pot_of_gold_still_ready=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_GameLocation.checkAction_only", "no_inventory_capacity_gate_on_open", "no_direct_object_inventory_or_debris_mutation", "remaining_reward_debris_uses_shared_pickup_executor_after_fresh_snapshot" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
