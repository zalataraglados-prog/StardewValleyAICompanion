using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static SmallModelAction PlanStepToAction(
            SmallModelPlanStep step,
            int stepIndex,
            int stepCount,
            bool activeMenuOpenBeforeStep,
            string activeMenuTypeBeforeStep)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("plan_step_kind", step.Kind),
                Parameter("target_location", step.TargetLocation),
                Parameter("compiler_context.plan_step_index", stepIndex.ToString()),
                Parameter("compiler_context.plan_step_count", stepCount.ToString()),
                Parameter("compiler_context.is_terminal_step", (stepIndex == stepCount - 1).ToString().ToLowerInvariant()),
                Parameter("compiler_context.active_menu_open_before_step", activeMenuOpenBeforeStep.ToString().ToLowerInvariant()),
                Parameter("compiler_context.active_menu_type_before_step", activeMenuTypeBeforeStep)
            };

            if (step.TargetTileX.HasValue)
            {
                parameters.Add(Parameter("target_tile_x", step.TargetTileX.Value.ToString()));
            }
            if (step.TargetTileY.HasValue)
            {
                parameters.Add(Parameter("target_tile_y", step.TargetTileY.Value.ToString()));
            }
            if (step.Direction.HasValue)
            {
                parameters.Add(Parameter("direction", step.Direction.Value.ToString()));
            }
            if (step.WaitTicks.HasValue)
            {
                parameters.Add(Parameter("wait_ticks", step.WaitTicks.Value.ToString()));
            }
            if (step.EstimatedMinutes.HasValue)
            {
                parameters.Add(Parameter("estimated_minutes", step.EstimatedMinutes.Value.ToString()));
            }
            parameters.AddRange(step.Preconditions.Select(value => Parameter("precondition", value)));
            parameters.AddRange(step.ExpectedEffects.Select(value => Parameter("expected_effect", value)));
            parameters.AddRange(step.SafetyConstraints.Select(value => Parameter("safety_constraint", value)));
            parameters.AddRange(step.FailurePolicy.Select(value => Parameter("failure_policy", value)));
            parameters.AddRange(step.Parameters);

            return new SmallModelAction
            {
                ActionId = string.IsNullOrWhiteSpace(step.StepId) ? "plan_step." + Guid.NewGuid().ToString("N") : step.StepId,
                OptionId = PlanStepOptionId(step.Kind),
                Rationale = "compiled from small_model_plan step",
                Parameters = parameters.ToArray()
            };
        }

        private static string PlanStepOptionId(string kind)
        {
            return kind switch
            {
                "move_to_tile" => "executor.move_to_tile",
                "traverse_connector" => "executor.traverse_connector",
                "face_direction" => "executor.face_direction",
                "interact" => "executor.interact",
                "accept_daily_quest" => "executor.accept_daily_quest",
                "accept_special_order" => "executor.accept_special_order",
                "claim_quest_reward" => "executor.claim_quest_reward",
                "sleep" => "executor.sleep",
                "wait_ticks" => "executor.wait_ticks",
                "close_menu" => "executor.close_menu",
                "buy_shop_item" => "executor.buy_shop_item",
                "sell_shop_item" => "executor.sell_shop_item",
                "choose_dialogue_response" => "executor.choose_dialogue_response",
                "choose_animal_purchase_response" => "executor.choose_animal_purchase_response",
                "purchase_animal" => "executor.purchase_animal",
                "water_crop" => "executor.water_crop",
                "apply_fertilizer" => "executor.apply_fertilizer",
                "clear_obstacle" => "executor.clear_obstacle",
                "break_farm_resource_clump" => "executor.break_farm_resource_clump",
                "break_current_location_resource_clump" => "executor.break_current_location_resource_clump",
                "till_soil" => "executor.till_soil",
                "plant_seed" => "executor.plant_seed",
                "harvest_crop" => "executor.harvest_crop",
                "harvest_giant_crop" => "executor.harvest_giant_crop",
                "pickup_debris" => "executor.pickup_debris",
                "collect_spawned_object" => "executor.collect_spawned_object",
                "harvest_ginger" => "executor.harvest_ginger",
                "harvest_bush" => "executor.harvest_bush",
                "claim_mine_reward_chest" => "executor.claim_mine_reward_chest",
                "collect_crab_pot" => "executor.collect_crab_pot",
                "collect_fish_pond_output" => "executor.collect_fish_pond_output",
                "complete_fish_pond_request" => "executor.complete_fish_pond_request",
                "collect_animal_product" => "executor.collect_animal_product",
                "pet_interact" => "executor.pet_interact",
                "fill_pet_bowl" => "executor.fill_pet_bowl",
                "donate_museum_item" => "executor.donate_museum_item",
                "donate_community_center_item" => "executor.donate_community_center_item",
                "purchase_joja_membership" => "executor.purchase_joja_membership",
                "purchase_joja_project" => "executor.purchase_joja_project",
                "purchase_farmhouse_upgrade" => "executor.purchase_farmhouse_upgrade",
                "purchase_farmhouse_expansion" => "executor.purchase_farmhouse_upgrade",
                "select_safe_item_slot" => "executor.select_safe_item_slot",
                "pan_ore_spot" => "executor.pan_ore_spot",
                "collect_machine_output" => "executor.collect_machine_output",
                "load_machine_input" => "executor.load_machine_input",
                "name_hatched_animal" => "executor.name_hatched_animal",
                "craft_machine_item" => "executor.craft_machine_item",
                "craft_storage_item" => "executor.craft_storage_item",
                "craft_quest_item" => "executor.craft_quest_item",
                "construct_quest_building" => "executor.construct_building",
                "construct_building" => "executor.construct_building",
                "place_machine_item" => "executor.place_machine",
                "remove_machine_item" => "executor.remove_machine",
                "place_storage_item" => "executor.place_storage",
                "read_book" => "executor.read_book",
                "catch_fish" => "executor.catch_fish",
                "mine_stone" => "executor.mine_stone",
                "break_container" => "executor.break_container",
                "break_resource_clump" => "executor.break_resource_clump",
                "combat_monster" => "executor.combat_monster",
                "shoot_monster" => "executor.shoot_monster",
                "place_bomb" => "executor.place_bomb",
                "place_staircase" => "executor.place_staircase",
                "consume_food" => "executor.consume_food",
                "descend_ladder" => "executor.descend_ladder",
                "descend_shaft" => "executor.descend_shaft",
                "exit_mine" => "executor.exit_mine",
                "cool_volcano_lava" => "executor.cool_volcano_lava",
                "break_volcano_stone" => "executor.break_volcano_stone",
                "break_volcano_container" => "executor.break_volcano_container",
                "combat_volcano_monster" => "executor.combat_volcano_monster",
                "social_interact" => "executor.social_interact",
                "quest_npc_interact" => "executor.quest_npc_interact",
                "quest_drop_box_donate" => "executor.quest_drop_box_donate",
                "play_junimo_kart" => "executor.play_junimo_kart",
                "ship_inventory_item_to_bin" => "executor.ship_inventory_item_to_bin",
                "transfer_material" => "executor.transfer_material",
                _ => "unknown.plan_step"
            };
        }

        private static bool StepOpensMenu(SmallModelPlanStep step)
        {
            return step.ExpectedEffects.Any(effect =>
                effect.Contains("menus.active_menu.is_open=true", StringComparison.OrdinalIgnoreCase) ||
                effect.Contains("menus.sleep_prompt_context.prompt_open=true", StringComparison.OrdinalIgnoreCase) ||
                effect.Contains("menu_open", StringComparison.OrdinalIgnoreCase));
        }

        private static bool StepClosesMenu(SmallModelPlanStep step)
        {
            return step.ExpectedEffects.Any(effect =>
                effect.Contains("menus.active_menu.is_open=false", StringComparison.OrdinalIgnoreCase));
        }

        private static string InferredOpenedMenuType(SmallModelPlanStep step)
        {
            if (step.ExpectedEffects.Any(effect =>
                    effect.Contains("SpecialOrdersBoard", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("special_order_board", StringComparison.OrdinalIgnoreCase)))
            {
                return "SpecialOrdersBoard";
            }

            if (step.ExpectedEffects.Any(effect =>
                    effect.Contains("Billboard", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("daily_quest_board", StringComparison.OrdinalIgnoreCase)))
            {
                return "Billboard";
            }

            if (step.ExpectedEffects.Any(effect =>
                    effect.Contains("DialogueBox", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("dialogue", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Blacksmith", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Carpenter", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Marnie", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_AdventureGuild", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_adventureGuild", StringComparison.OrdinalIgnoreCase)))
            {
                return "DialogueBox";
            }

            if (step.ExpectedEffects.Any(effect =>
                    effect.Contains("ShopMenu", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_OpenShop", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Buy", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_JojaShop", StringComparison.OrdinalIgnoreCase)))
            {
                return "ShopMenu";
            }

            return string.Empty;
        }

    }
}
