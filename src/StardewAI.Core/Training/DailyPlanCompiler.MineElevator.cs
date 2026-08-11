using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MineElevatorApproachSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "mine_elevator_approach", 0),
                Kind = "move_to_tile",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "ordinary_mine_elevator_endpoint_exact=true" },
                ExpectedEffects = new[] { "player_adjacent_to_exact_mine_elevator_action=true", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_collision_path_only", "do_not_warp", "ordinary_mines_only" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> OpenMineElevatorSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || CandidateInt(candidate, "target_depth") is null)
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "open_mine_elevator", 0),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "player_adjacent_to_exact_mine_elevator_action=true", "unlocked_checkpoint_projection_matches=true" },
                ExpectedEffects = new[] { "menus.active_menu.type=MineElevatorMenu", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "interaction_kind=map_action", "expected_action_type=MineElevator", "do_not_call_enterMine_directly" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> SelectMineElevatorFloorSteps(PolicyEventCandidatePrediction candidate)
    {
        if (CandidateInt(candidate, "target_depth") is null || string.IsNullOrWhiteSpace(CandidateParameter(candidate, "mine_elevator_menu_identity_sha256")))
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "select_mine_elevator_floor", 0),
                Kind = "close_menu",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[] { "menus.active_menu.type=MineElevatorMenu", "mine_elevator_menu_identity_matches=true", "target_checkpoint_selectable=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect, "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_MineElevatorMenu_receiveLeftClick_only", "reuse_executor.close_menu", "do_not_call_enterMine_or_warpFarmer_directly" },
                FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
