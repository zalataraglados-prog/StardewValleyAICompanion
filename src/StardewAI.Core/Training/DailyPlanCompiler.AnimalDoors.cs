using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> OpenAnimalDoorSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var targetX = candidate.TileX;
        var targetY = candidate.TileY;
        var standX = CandidateInt(candidate, "stand_tile_x");
        var standY = CandidateInt(candidate, "stand_tile_y");
        var routeDistance = CandidateInt(candidate, "route_distance_tiles");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue ||
            !standY.HasValue || !routeDistance.HasValue || routeDistance < 0 ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "building_type")))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        var steps = new List<SmallModelPlanStep>();
        if (routeDistance > 0)
        {
            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "move_to_animal_door", 0),
                Kind = "move_to_tile",
                TargetLocation = candidate.LocationId,
                TargetTileX = standX,
                TargetTileY = standY,
                EstimatedMinutes = Execution.GameClockBudgetPolicy.MovementTilesToGameMinutes(routeDistance.Value),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                ExpectedEffects = new[] { "player.tile=" + standX + "," + standY },
                SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler", "no_direct_coordinate_teleport" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[] { Parameter("max_movement_tiles", Math.Max(1, routeDistance.Value).ToString()) }
            });
        }
        steps.Add(new SmallModelPlanStep
        {
            StepId = StepId(candidate, "open_animal_door", steps.Count),
            Kind = "interact",
            TargetLocation = candidate.LocationId,
            TargetTileX = targetX,
            TargetTileY = targetY,
            EstimatedMinutes = 1,
            Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "target_tile_adjacent=true", "animal_door_open=false" },
            ExpectedEffects = new[] { candidate.ExpectedEffect },
            SafetyConstraints = new[] { "native_Building_doAction_right_click_only", "exact_building_and_door_identity", "no_direct_animalDoorOpen_mutation" },
            FailurePolicy = new[] { "refresh_snapshot_and_replan" },
            Parameters = candidate.Parameters
        });
        return steps;
    }
}
