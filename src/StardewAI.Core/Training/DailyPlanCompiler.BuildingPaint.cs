using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PaintBuildingRegionSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "building_identity")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "paint_region_id")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "paint_target_mode")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "appearance_reason")))
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "paint_building_region", 0),
                Kind = "paint_building_region",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_live_building_paint_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "shared_native_Robin_and_CarpenterMenu_lifecycle_only", "native_BuildingPaintMenu_controls_only", "mouse_reachable_exact_slider_values_only" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
