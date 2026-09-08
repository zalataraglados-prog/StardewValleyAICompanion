using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CollectCrabPotSteps(PolicyEventCandidatePrediction candidate)
    {
        var stand = ParseCoordinate(candidate.ExpectedEffect, "crab_pot_stand_tile=");
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue ||
            string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "collect_crab_pot", 0),
                Kind = "collect_crab_pot",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "crab_pot_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_checkAction_only", "transparent_adjacent_stand_tile", "no_direct_crab_pot_or_inventory_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
                    .Concat(new[]
                    {
                        Parameter("stand_tile_x", stand.Value.X.ToString()),
                        Parameter("stand_tile_y", stand.Value.Y.ToString())
                    })
                    .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray()
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> LoadCrabPotBaitSteps(
        PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            return Array.Empty<SmallModelPlanStep>();

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "load_crab_pot_bait", 0),
                Kind = "load_crab_pot_bait",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "native_bait_probe_and_production_domain_still_match=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_checkAction_and_performObjectDropInAction_only",
                    "no_direct_crab_pot_bait_or_inventory_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> PlaceCrabPotSteps(
        PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            return Array.Empty<SmallModelPlanStep>();

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "place_crab_pot", 0),
                Kind = "place_crab_pot",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "native_water_tile_and_production_signature_still_match=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "existing_executor_place_crab_pot_only",
                    "no_direct_object_or_inventory_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
