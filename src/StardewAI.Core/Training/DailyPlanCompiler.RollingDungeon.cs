using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> RollingDungeonPrimitiveSteps(
            PolicyEventCandidatePrediction candidate)
        {
            var executionOptionId = CandidateParameter(candidate, "execution_option_id");
            var stepKind = executionOptionId switch
            {
                "executor.move_to_tile" => "move_to_tile",
                "executor.interact" => "interact",
                "executor.mine_stone" => "mine_stone",
                "executor.break_container" => "break_container",
                "executor.break_resource_clump" => "break_resource_clump",
                "executor.combat_monster" => "combat_monster",
                "executor.shoot_monster" => "shoot_monster",
                "executor.place_bomb" => "place_bomb",
                "executor.pickup_debris" => "pickup_debris",
                "executor.consume_food" => "consume_food",
                "executor.descend_ladder" => "descend_ladder",
                "executor.descend_shaft" => "descend_shaft",
                "executor.exit_mine" => "exit_mine",
                "executor.claim_mine_reward_chest" => "claim_mine_reward_chest",
                "executor.cool_volcano_lava" => "cool_volcano_lava",
                "executor.break_volcano_stone" => "break_volcano_stone",
                "executor.break_volcano_container" => "break_volcano_container",
                "executor.combat_volcano_monster" => "combat_volcano_monster",
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(stepKind))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var targetX = candidate.TileX ?? CandidateInt(candidate, "target_tile_x");
            var targetY = candidate.TileY ?? CandidateInt(candidate, "target_tile_y");
            var targetLocation = string.IsNullOrWhiteSpace(candidate.LocationId)
                ? CandidateParameter(candidate, "target_location")
                : candidate.LocationId;
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, stepKind, 0),
                    Kind = stepKind,
                    TargetLocation = targetLocation,
                    TargetTileX = targetX,
                    TargetTileY = targetY,
                    EstimatedMinutes = Math.Max(1, CandidateInt(candidate, "estimated_minutes") ?? 1),
                    Preconditions = new[]
                    {
                        "fresh_snapshot_state_hash_matches=true",
                        "rolling_dungeon_floor_step_still_matches_transparent_state=true"
                    },
                    ExpectedEffects = new[]
                    {
                        candidate.ExpectedEffect,
                        "fresh_snapshot_replan_required=true"
                    },
                    SafetyConstraints = new[]
                    {
                        "execute_only_compiler_selected_current_floor_primitive",
                        "runtime_revalidate_exact_target_identity_and_safety_window"
                    },
                    FailurePolicy = new[]
                    {
                        "stop_current_floor_primitive_refresh_snapshot_and_replan"
                    },
                    Parameters = candidate.Parameters
                }
            };
        }
    }
}
