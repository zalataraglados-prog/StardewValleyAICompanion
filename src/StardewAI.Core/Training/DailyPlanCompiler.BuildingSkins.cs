using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ChangeBuildingSkinSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "building_identity")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "target_skin_key")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "appearance_reason")))
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "change_building_skin", 0),
                Kind = "change_building_skin",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_live_building_skin_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_Robin_CarpenterMenu_and_BuildingSkinMenu_only", "exact_shortest_click_sequence", "acknowledge_paint_colors_reset" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
