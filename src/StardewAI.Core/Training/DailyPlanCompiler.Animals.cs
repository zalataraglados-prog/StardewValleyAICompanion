using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CollectAnimalProductSteps(PolicyEventCandidatePrediction candidate)
    {
        var stand = ParseCoordinate(candidate.ExpectedEffect, "animal_harvest_stand_tile=");
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue ||
            string.IsNullOrWhiteSpace(candidate.LocationId) || string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "collect_animal_product", 0),
                Kind = "collect_animal_product",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "animal_harvest_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_animal_tool_lifecycle_only", "exact_animal_identity", "no_direct_animal_or_inventory_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
