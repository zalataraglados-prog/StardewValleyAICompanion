using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ClaimStarterGiftSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_starter_gift", 0),
                Kind = "claim_starter_gift",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_owned_farmhouse_starter_gift_still_ready=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_GameLocation.checkAction_only", "exact_starter_gift_chest_and_contents_rebound", "owned_farmhouse_only", "no_direct_chest_inventory_or_quest_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
