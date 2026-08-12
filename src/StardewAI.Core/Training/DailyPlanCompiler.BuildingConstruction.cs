using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ConstructBuildingSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "construction_building_type")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "construction_reason")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "placement_location_id")))
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "construct_building", 0),
                Kind = "construct_building",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "purpose_bound_native_building_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_builder_dialogue_and_CarpenterMenu_only", "exact_blueprint_location_and_placement", "no_direct_money_inventory_or_building_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
