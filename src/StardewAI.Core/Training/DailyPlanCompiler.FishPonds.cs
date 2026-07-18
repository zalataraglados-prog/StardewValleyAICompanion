using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FishPondSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        var kind = candidate.Kind;
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, kind, 0),
                Kind = kind,
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "fish_pond_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_checkAction_only", "transparent_pond_edge_stand_tile", "no_direct_pond_inventory_or_skill_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
