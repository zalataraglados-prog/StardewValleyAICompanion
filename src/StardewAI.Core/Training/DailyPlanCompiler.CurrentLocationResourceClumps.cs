using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep>
        ClearCurrentLocationResourceClumpSteps(
            PolicyEventCandidatePrediction candidate)
    {
        var anchorX = CandidateParameter(candidate, "resource_clump_tile_x");
        var anchorY = CandidateParameter(candidate, "resource_clump_tile_y");
        if (!int.TryParse(anchorX, out _) || !int.TryParse(anchorY, out _))
            return [];

        return
        [
            new SmallModelPlanStep
            {
                StepId = StepId(
                    candidate,
                    "break_current_location_resource_clump",
                    0),
                Kind = "break_current_location_resource_clump",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = Math.Max(
                    1,
                    TicksToMinutes(candidate.EstimatedTicks)),
                Preconditions =
                [
                    "candidate_id:" + candidate.CandidateId,
                    "exact_loaded_current_location_resource_clump_still_matches=true"
                ],
                ExpectedEffects = [candidate.ExpectedEffect],
                SafetyConstraints =
                [
                    "native_cross_frame_axe_lifecycle",
                    "transparent_perimeter_stand_tile",
                    "no_direct_clump_drop_experience_or_route_mutation"
                ],
                FailurePolicy = ["refresh_snapshot_and_replan"],
                Parameters = candidate.Parameters
            }
        ];
    }
}
