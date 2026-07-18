using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FarmhouseUpgradeSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, candidate.Kind, 0),
                Kind = candidate.Kind,
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "marriage_house_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_Carpenter_action_and_dialogue_responses_only",
                    "purchase_exactly_one_verified_farmhouse_upgrade",
                    "verify_money_material_and_construction_countdown",
                    "no_direct_money_inventory_house_or_construction_mutation"
                },
                FailurePolicy = new[] { "close_carpenter_dialogue_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
