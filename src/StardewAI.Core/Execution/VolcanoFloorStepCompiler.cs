using System;
using System.Collections.Generic;
using System.Globalization;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Execution
{
    public static class VolcanoFloorStepCompiler
    {
        public static string ExecutionOptionId(VolcanoFloorStepPlan plan)
        {
            return plan.StepKind switch
            {
                VolcanoFloorStepKinds.PressDwarfSwitch => "executor.move_to_tile",
                VolcanoFloorStepKinds.WaitForDwarfGate => "executor.wait_ticks",
                VolcanoFloorStepKinds.TraverseForwardConnector => "executor.traverse_connector",
                VolcanoFloorStepKinds.CoolLavaTile => "executor.cool_volcano_lava",
                VolcanoFloorStepKinds.BreakStone => "executor.break_volcano_stone",
                VolcanoFloorStepKinds.BreakContainer => "executor.break_volcano_container",
                VolcanoFloorStepKinds.CombatMonster => "executor.combat_volcano_monster",
                _ => string.Empty
            };
        }

        public static SmallModelActionParameter[] BuildExecutionParameters(
            VolcanoFloorStepPlan plan)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("execution_option_id", ExecutionOptionId(plan)),
                Parameter("volcano_step_kind", plan.StepKind),
                Parameter("volcano_step_reason", plan.Reason),
                Parameter(
                    "estimated_movement_tiles",
                    plan.EstimatedMovementTiles.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "estimated_tool_uses",
                    plan.EstimatedToolUses.ToString(
                        CultureInfo.InvariantCulture))
            };
            Add(parameters, "target_tile_x", plan.TargetTileX);
            Add(parameters, "target_tile_y", plan.TargetTileY);
            Add(parameters, "stand_tile_x", plan.StandTileX);
            Add(parameters, "stand_tile_y", plan.StandTileY);
            Add(
                parameters,
                "max_movement_tiles",
                plan.StepKind == VolcanoFloorStepKinds.CombatMonster
                    ? CombatMovementBudget(plan)
                    : plan.EstimatedMovementTiles > 0
                        ? Math.Max(8, plan.EstimatedMovementTiles + 8)
                        : (int?)null);
            Add(
                parameters,
                "max_tool_swings",
                plan.EstimatedToolUses > 0
                    ? plan.EstimatedToolUses + 1
                    : (int?)null);
            Add(
                parameters,
                "expected_target_location",
                plan.ExpectedTargetLocation);
            Add(
                parameters,
                "expected_arrival_tile_x",
                plan.ExpectedArrivalTileX);
            Add(
                parameters,
                "expected_arrival_tile_y",
                plan.ExpectedArrivalTileY);
            Add(
                parameters,
                "watering_can_slot_index",
                plan.WateringCanSlotIndex);
            Add(parameters, "tool_slot_index", plan.ToolSlotIndex);
            Add(
                parameters,
                "combat_weapon_slot_index",
                plan.CombatWeaponSlotIndex);
            Add(
                parameters,
                "combat_intent",
                plan.CombatIntent);
            Add(parameters, "route_objective_id", plan.RouteObjectiveId);
            Add(
                parameters,
                "route_target_tile_x",
                plan.RouteTargetTileX);
            Add(
                parameters,
                "route_target_tile_y",
                plan.RouteTargetTileY);
            Add(
                parameters,
                "route_target_stand_tile_x",
                plan.RouteTargetStandTileX);
            Add(
                parameters,
                "route_target_stand_tile_y",
                plan.RouteTargetStandTileY);
            Add(
                parameters,
                "blocked_route_cell_x",
                plan.BlockedRouteCellX);
            Add(
                parameters,
                "blocked_route_cell_y",
                plan.BlockedRouteCellY);
            Add(
                parameters,
                "blocker_attribution_status",
                plan.BlockerAttributionStatus);
            Add(
                parameters,
                "expected_connectivity_gain",
                plan.ExpectedConnectivityGain);
            Add(
                parameters,
                "target_runtime_identity",
                plan.TargetRuntimeIdentity);
            Add(
                parameters,
                "target_runtime_type",
                plan.TargetRuntimeType);
            Add(parameters, "target_name", plan.TargetName);
            Add(
                parameters,
                "qualified_item_id",
                plan.TargetQualifiedItemId);
            if (plan.StepKind == VolcanoFloorStepKinds.WaitForDwarfGate)
            {
                parameters.Add(Parameter("wait_ticks", "120"));
            }
            if (plan.StepKind == VolcanoFloorStepKinds.PressDwarfSwitch)
            {
                parameters.Add(
                    Parameter("expected_touch_action", "DwarfSwitch"));
            }
            if (plan.StepKind ==
                VolcanoFloorStepKinds.TraverseForwardConnector)
            {
                parameters.Add(Parameter("connector_kind", "warp"));
            }
            return parameters.ToArray();
        }

        private static int CombatMovementBudget(
            VolcanoFloorStepPlan plan)
        {
            return StardewAI.Contracts.Training
                .TrainingCombatIntentRules.BoundMovementBudget(
                    plan.CombatIntent,
                    plan.EstimatedMovementTiles,
                    512);
        }

        private static void Add(
            List<SmallModelActionParameter> parameters,
            string name,
            int? value)
        {
            if (value.HasValue)
            {
                parameters.Add(
                    Parameter(
                        name,
                        value.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void Add(
            List<SmallModelActionParameter> parameters,
            string name,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static SmallModelActionParameter Parameter(
            string name,
            string value)
        {
            return new SmallModelActionParameter
            {
                Name = name,
                Value = value
            };
        }
    }
}
