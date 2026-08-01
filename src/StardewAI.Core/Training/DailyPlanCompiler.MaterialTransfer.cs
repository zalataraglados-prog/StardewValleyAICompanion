using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> TransferInventoryItemSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var standX = CandidateInt(candidate, "stand_tile_x");
        var standY = CandidateInt(candidate, "stand_tile_y");
        var targetX = CandidateInt(candidate, "target_tile_x");
        var targetY = CandidateInt(candidate, "target_tile_y");
        foreach (var name in MaterialTransferIntentBinder.RequiredParameterNames)
        {
            if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, name)))
            {
                return Array.Empty<SmallModelPlanStep>();
            }
        }
        if (!standX.HasValue || !standY.HasValue ||
            !targetX.HasValue || !targetY.HasValue)
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        var locationId = string.IsNullOrWhiteSpace(candidate.LocationId)
            ? CandidateParameter(candidate, "location_id")
            : candidate.LocationId;
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        var transferParameters = new List<SmallModelActionParameter>();
        foreach (var name in MaterialTransferIntentBinder.RequiredParameterNames)
        {
            transferParameters.Add(Parameter(name, CandidateParameter(candidate, name)));
        }
        transferParameters.Add(Parameter("stand_tile_x", standX.Value.ToString()));
        transferParameters.Add(Parameter("stand_tile_y", standY.Value.ToString()));

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "move_to_transfer_chest", 0),
                Kind = "move_to_tile",
                TargetLocation = locationId,
                TargetTileX = standX,
                TargetTileY = standY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                ExpectedEffects = new[] { "player.tile=" + standX + "," + standY },
                SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" }
            },
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "transfer_inventory_item", 1),
                Kind = "transfer_material",
                TargetLocation = locationId,
                TargetTileX = targetX,
                TargetTileY = targetY,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "material_transfer_projection_status=projected",
                    "player_adjacent_to_transfer_chest=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "intent_from_explicit_candidate_parameters",
                    "projection_recomputed_by_action_queue_compiler",
                    "native_chest_menu_transfer_only",
                    "no_direct_inventory_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = transferParameters.ToArray()
            }
        };
    }
}
