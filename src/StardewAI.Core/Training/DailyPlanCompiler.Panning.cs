using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PanOreSpotSteps(PolicyEventCandidatePrediction candidate)
    {
        var stand = ParseCoordinate(candidate.ExpectedEffect, "panning_stand_tile=");
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue || string.IsNullOrWhiteSpace(candidate.LocationId))
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "pan_ore_spot", 0),
                Kind = "pan_ore_spot",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "panning_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_pan_lifecycle_only", "exact_reward_multiset", "no_direct_inventory_skill_or_ore_point_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
