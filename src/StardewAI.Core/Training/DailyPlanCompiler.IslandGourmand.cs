using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> GourmandRequestSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "advance_gourmand_request", 0),
                Kind = "advance_gourmand_request",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "island_gourmand_projection_still_matches=true",
                    "required_crop_still_ready=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_Gourmand_action_only",
                    "native_Gourmand_Yes_response_only_when_completing",
                    "no_direct_request_tracker_walnut_or_debris_mutation"
                },
                FailurePolicy = new[] { "close_gourmand_dialogue_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
