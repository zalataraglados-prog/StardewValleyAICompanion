using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public static class MiningFloorStepCompiler
    {
        public static string ExecutionOptionId(MiningFloorStepPlan plan)
        {
            return plan.StepKind switch
            {
                MiningFloorStepKinds.MineStone => "executor.mine_stone",
                MiningFloorStepKinds.BreakContainer => "executor.break_container",
                MiningFloorStepKinds.BreakResourceClump => "executor.break_resource_clump",
                MiningFloorStepKinds.CombatMonster => "executor.combat_monster",
                MiningFloorStepKinds.ShootMonster => "executor.shoot_monster",
                MiningFloorStepKinds.PlaceBomb => "executor.place_bomb",
                MiningFloorStepKinds.PickupDebris => "executor.pickup_debris",
                MiningFloorStepKinds.ConsumeFood => "executor.consume_food",
                MiningFloorStepKinds.DescendLadder => "executor.descend_ladder",
                MiningFloorStepKinds.DescendShaft => "executor.descend_shaft",
                MiningFloorStepKinds.ExitMine => "executor.exit_mine",
                MiningFloorStepKinds.MoveToGoldenScytheAltar => "executor.move_to_tile",
                MiningFloorStepKinds.ClaimGoldenScythe => "executor.interact",
                MiningFloorStepKinds.MoveToSkullKeyChest => "executor.move_to_tile",
                MiningFloorStepKinds.ClaimSkullKey => "executor.interact",
                MiningFloorStepKinds.ClaimRewardChest => "executor.claim_mine_reward_chest",
                _ => string.Empty
            };
        }

        public static SmallModelActionParameter[] BuildExecutionParameters(MiningFloorStepPlan plan)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("execution_option_id", ExecutionOptionId(plan)),
                Parameter("mining_step_kind", plan.StepKind),
                Parameter("mining_step_reason", plan.Reason),
                Parameter("estimated_movement_tiles", plan.EstimatedMovementTiles.ToString()),
                Parameter("estimated_tool_swings", plan.EstimatedToolSwings.ToString()),
                Parameter("safety_window_status", plan.SafetyWindowStatus)
            };
            Add(parameters, "target_tile_x", plan.TargetTileX);
            Add(parameters, "target_tile_y", plan.TargetTileY);
            Add(parameters, "stand_tile_x", plan.StandTileX);
            Add(parameters, "stand_tile_y", plan.StandTileY);
            Add(parameters, "max_movement_tiles", plan.EstimatedMovementTiles > 0 ? Math.Max(8, plan.EstimatedMovementTiles + 8) : (int?)null);
            Add(parameters, "max_tool_swings", plan.EstimatedToolSwings > 0 ? Math.Max(1, plan.EstimatedToolSwings + 2) : (int?)null);
            Add(parameters, "debris_index", plan.DebrisIndex);
            Add(parameters, "slot_index", plan.FoodSlotIndex);
            Add(parameters, "restore_slot_index", plan.RestoreSlotIndex);
            Add(parameters, "tool_slot_index", plan.ToolSlotIndex);
            Add(parameters, "required_tool_kind", plan.RequiredToolKind);
            Add(parameters, "resource_clump_tile_x", plan.ResourceClumpTileX);
            Add(parameters, "resource_clump_tile_y", plan.ResourceClumpTileY);
            Add(parameters, "resource_clump_width", plan.ResourceClumpWidth);
            Add(parameters, "resource_clump_height", plan.ResourceClumpHeight);
            Add(parameters, "resource_clump_parent_sheet_index", plan.ResourceClumpParentSheetIndex);
            Add(parameters, "expected_mine_level_delta", plan.ExpectedMineLevelDelta);
            Add(parameters, "expected_mine_level_after", plan.ExpectedMineLevelAfter);
            Add(parameters, "expected_health_cost", plan.ExpectedHealthCost);
            Add(parameters, "expected_health_after", plan.ExpectedHealthAfter);
            Add(parameters, "expected_target_location", plan.ExpectedTargetLocation);
            Add(parameters, "expected_arrival_tile_x", plan.ExpectedArrivalTileX);
            Add(parameters, "expected_arrival_tile_y", plan.ExpectedArrivalTileY);
            Add(parameters, "qualified_item_id", plan.TargetQualifiedItemId);
            Add(parameters, "quantity", plan.TargetQuantity);
            Add(parameters, "expected_output_quality", plan.TargetQuality);
            Add(parameters, "reward_branch", plan.RewardBranch);
            Add(parameters, "expected_output_items_json", plan.ExpectedOutputItemsJson);
            Add(parameters, "native_gain_experience_call_amount", plan.NativeGainExperienceCallAmount);
            Add(parameters, "expected_stardrop_max_stamina_delta", plan.ExpectedStardropMaxStaminaDelta);
            Add(parameters, "expected_drop_qualified_item_ids", string.Join(",", plan.ExpectedDropQualifiedItemIds));
            Add(parameters, "source_match_status", plan.SourceMatchStatus);
            Add(parameters, "target_drop_chance_preview", plan.TargetDropChancePreview);
            Add(parameters, "target_drop_probability_status", plan.TargetDropProbabilityStatus);
            Add(parameters, "target_expected_quantity_per_kill", plan.TargetExpectedQuantityPerKill);
            Add(parameters, "target_drop_efficiency_score", plan.TargetDropEfficiencyScore);
            Add(parameters, "target_runtime_identity", plan.TargetRuntimeIdentity);
            Add(parameters, "target_runtime_type", plan.TargetRuntimeType);
            Add(parameters, "target_name", plan.TargetName);
            Add(parameters, "required_weapon_enchantment_runtime_type", plan.RequiredWeaponEnchantmentRuntimeType);
            Add(parameters, "combat_weapon_slot_index", plan.CombatWeaponSlotIndex);
            Add(parameters, "combat_method", plan.CombatMethod);
            Add(parameters, "combat_terminal_state", plan.CombatTerminalState);
            Add(parameters, "skill_experience_skill_id", plan.SkillExperienceSkillId);
            Add(parameters, "expected_skill_experience", plan.ExpectedSkillExperience);
            Add(parameters, "skill_experience_on_success_min", plan.SkillExperienceMinimum);
            Add(parameters, "skill_experience_on_success_max", plan.SkillExperienceMaximum);
            Add(parameters, "skill_experience_condition", plan.SkillExperienceCondition);
            Add(parameters, "skill_experience_projection_status", plan.SkillExperienceProjectionStatus);
            Add(parameters, "secondary_skill_experience_skill_id", plan.SecondarySkillExperienceSkillId);
            Add(parameters, "secondary_skill_experience_on_success_min", plan.SecondarySkillExperienceMinimum);
            Add(parameters, "secondary_skill_experience_on_success_max", plan.SecondarySkillExperienceMaximum);
            Add(parameters, "secondary_skill_experience_condition", plan.SecondarySkillExperienceCondition);
            Add(parameters, "secondary_skill_experience_projection_status", plan.SecondarySkillExperienceProjectionStatus);
            Add(parameters, "slingshot_slot_index", plan.SlingshotSlotIndex);
            Add(parameters, "slingshot_ammo_qualified_item_id", plan.SlingshotAmmoQualifiedItemId);
            Add(parameters, "bomb_slot_index", plan.BombSlotIndex);
            Add(parameters, "bomb_qualified_item_id", plan.BombQualifiedItemId);
            Add(parameters, "bomb_radius_tiles", plan.BombRadiusTiles);
            Add(parameters, "escape_tile_x", plan.EscapeTileX);
            Add(parameters, "escape_tile_y", plan.EscapeTileY);
            Add(parameters, "expected_bomb_object_hits", plan.ExpectedBombObjectHits);
            Add(parameters, "expected_bomb_monster_hits", plan.ExpectedBombMonsterHits);
            Add(parameters, "expected_combat_attacks", plan.ExpectedCombatAttacks);
            Add(parameters, "expected_combat_duration_ms", plan.ExpectedCombatDurationMs);
            Add(parameters, "estimated_target_cost_ms", plan.EstimatedTargetCostMs);
            Add(parameters, "combat_duration_status", plan.CombatDurationStatus);
            if (plan.StepKind == MiningFloorStepKinds.ClaimGoldenScythe)
            {
                parameters.Add(Parameter("interaction_kind", "map_action"));
                parameters.Add(Parameter("expected_action_type", "GoldenScythe"));
            }
            else if (plan.StepKind == MiningFloorStepKinds.ClaimSkullKey)
            {
                parameters.Add(Parameter("interaction_kind", "overlay_object"));
                parameters.Add(Parameter("expected_action_type", "SkullKeyChest"));
                parameters.Add(Parameter("required_postcondition", "player.has_skull_key=true"));
            }
            else if (plan.StepKind == MiningFloorStepKinds.ClaimRewardChest)
            {
                parameters.Add(Parameter("interaction_kind", "overlay_object"));
                parameters.Add(Parameter("expected_action_type", "MineRewardChest"));
                parameters.Add(Parameter("expected_skill_id", "luck"));
                parameters.Add(Parameter("expected_skill_experience_delta", "0"));
                parameters.Add(Parameter("is_stardrop", (plan.TargetQualifiedItemId == "(O)434").ToString().ToLowerInvariant()));
                parameters.Add(Parameter("native_contract", "one_reward_open_then_wait_dumpContents_then_empty_chest_cleanup_checkAction"));
            }
            return parameters.ToArray();
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, int? value)
        {
            if (value.HasValue)
            {
                parameters.Add(Parameter(name, value.Value.ToString()));
            }
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, double? value)
        {
            if (value.HasValue)
            {
                parameters.Add(Parameter(name, value.Value.ToString("R", CultureInfo.InvariantCulture)));
            }
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }
    }}
