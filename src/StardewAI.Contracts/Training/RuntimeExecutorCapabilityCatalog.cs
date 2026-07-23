using System;
using System.Collections.Generic;

namespace StardewAI.Contracts.Training
{
    public static class RuntimeExecutorCapabilityCatalog
    {
        private static readonly HashSet<string> SupportedOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "farm.maintain_crops",
            "executor.move_to_tile",
            "executor.traverse_connector",
            "executor.face_direction",
            "executor.interact",
            "executor.buy_shop_item",
            "executor.sell_shop_item",
            "executor.choose_dialogue_response",
            "executor.sleep",
            "executor.wait_ticks",
            "executor.clear_obstacle",
            "executor.break_farm_resource_clump",
            "executor.break_current_location_resource_clump",
            "executor.till_soil",
            "executor.plant_seed",
            "executor.harvest_crop",
            "executor.harvest_giant_crop",
            "executor.pickup_debris",
            "executor.collect_spawned_object",
            "executor.harvest_ginger",
            "executor.harvest_bush",
            "executor.claim_mine_reward_chest",
            "executor.collect_crab_pot",
            "executor.collect_fish_pond_output",
            "executor.complete_fish_pond_request",
            "executor.collect_animal_product",
            "executor.pet_interact",
            "executor.fill_pet_bowl",
            "executor.donate_museum_item",
            "executor.donate_community_center_item",
            "executor.purchase_joja_membership",
            "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade",
            "executor.pan_ore_spot",
            "executor.collect_machine_output",
            "executor.load_machine_input",
            "executor.craft_machine_item",
            "executor.read_book",
            "executor.catch_fish",
            "executor.cool_volcano_lava",
            "executor.break_volcano_stone",
            "executor.break_volcano_container",
            "executor.combat_volcano_monster",
            "executor.mine_stone",
            "executor.break_container",
            "executor.break_resource_clump",
            "executor.combat_monster",
            "executor.shoot_monster",
            "executor.place_bomb",
            "executor.consume_food",
            "executor.descend_ladder",
            "executor.descend_shaft",
            "executor.exit_mine",
            "executor.social_interact",
            "executor.select_safe_item_slot",
            "executor.close_menu",
            "executor.ship_inventory_item_to_bin"
        };

        public static IReadOnlyCollection<string> OptionIds => SupportedOptions;

        public static bool IsSupported(string optionId)
        {
            return !string.IsNullOrWhiteSpace(optionId) && SupportedOptions.Contains(optionId);
        }
    }
}
