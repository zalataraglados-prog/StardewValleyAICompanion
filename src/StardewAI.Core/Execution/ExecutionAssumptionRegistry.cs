using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Execution
{
    public sealed class ExecutionAssumption
    {
        public string DomainId { get; set; } = string.Empty;

        public string Profile { get; set; } = "perfect_human_player";

        public string[] AppliesToOptions { get; set; } = Array.Empty<string>();

        public string[] HardConstraints { get; set; } = Array.Empty<string>();

        public string[] CalibrationFactors { get; set; } = Array.Empty<string>();

        public string[] PreferencePenaltyExclusions { get; set; } = Array.Empty<string>();

        public string[] DecompiledAnchors { get; set; } = Array.Empty<string>();
    }

    public sealed class ExecutionAssumptionRegistry
    {
        private readonly ExecutionAssumption[] assumptions =
        {
            Assumption(
                "mining_and_combat",
                new[] { "exploration.visit_location" },
                new[] { "time_budget", "health_floor", "energy_floor", "inventory_capacity", "route_to_exit_or_elevator" },
                new[] { "mine_level", "mine_random", "ladder_discovery", "monster_mix", "ore_nodes", "loot" },
                new[] { "missed_swings", "bad_dodging", "slow_reaction", "poor_path_micro" },
                new[] { "MineShaft.mineLevel", "MineShaft.mineRandom", "MineShaft.findLadder", "Monster" }),
            Assumption(
                "volcano_dungeon",
                new[] { "exploration.visit_location" },
                new[] { "time_budget", "health_floor", "energy_floor", "route_to_exit", "water_or_bridge_requirements" },
                new[] { "generated_level", "monster_mix", "resource_nodes", "forge_access" },
                new[] { "bad_dodging", "poor_path_micro", "missed_weapon_timing" },
                new[] { "VolcanoDungeon", "Monster", "GameLocation.warps" }),
            Assumption(
                "fishing",
                new[] { "exploration.visit_location", "quest.advance", "fishing.catch_fish", "executor.catch_fish" },
                new[] { "time_budget", "energy_floor", "fishable_tile", "rod_and_bait_tackle" },
                new[] { "bite_time", "fish_selection", "fish_difficulty", "treasure_chance", "weather_season_time" },
                new[] { "missed_bite", "bad_bobber_control", "failed_perfect_catch_due_to_inputs" },
                new[] { "FishingRod.minFishingBiteTime", "FishingRod.maxFishingBiteTime", "FishingRod.baseChanceForTreasure", "FishingGame" }),
            Assumption(
                "crab_pot_collection",
                new[] { "fishing.collect_crab_pots", "executor.collect_crab_pot" },
                new[] { "ready_output", "inventory_capacity", "adjacent_reachable_tile", "menu_clear" },
                new[] { "route_length", "book_double_roll", "caught_fish_size" },
                Array.Empty<string>(),
                new[] { "CrabPot.checkForAction", "Farmer.caughtFish", "Farmer.gainExperience" }),
            Assumption(
                "fish_pond_service",
                new[] { "fishing.service_fish_ponds", "executor.collect_fish_pond_output", "executor.complete_fish_pond_request" },
                new[] { "exact_pond_identity", "native_branch_priority", "inventory_or_toolbar_capacity", "reachable_pond_edge_tile", "menu_clear" },
                new[] { "route_length", "output_receipt_callbacks", "request_mutex_completion" },
                Array.Empty<string>(),
                new[] { "FishPond.doAction", "FishPond.ResolveNeeds", "Farmer.gainExperience" }),
            Assumption(
                "panning",
                new[] { "foraging.pan_ore_spot", "executor.pan_ore_spot" },
                new[] { "active_ore_pan_point", "exact_pan_tool", "inventory_capacity", "reachable_shore_tile", "menu_clear" },
                new[] { "reward_multiset", "mining_experience", "foraging_experience", "post_use_ore_point_respawn" },
                Array.Empty<string>(),
                new[] { "Pan.beginUsing", "Pan.getPanItems", "Pan.DoFunction", "GameLocation.performOrePanTenMinuteUpdate" }),
            Assumption(
                "ginger_harvest",
                new[] { "foraging.harvest_ginger", "executor.harvest_ginger" },
                new[] { "exact_ginger_crop", "hoe_available", "energy_floor", "adjacent_reachable_tile", "menu_clear" },
                new[] { "route_length", "native_tool_animation_ticks" },
                Array.Empty<string>(),
                new[] { "Crop.hitWithHoe", "HoeDirt.performToolAction", "Hoe.DoFunction", "Farmer.gainExperience" }),
            Assumption(
                "bush_harvest",
                new[] { "foraging.harvest_bushes", "executor.harvest_bush" },
                new[] { "exact_vanilla_bush", "native_ready_and_bloom_state", "perimeter_interaction_tile", "menu_clear" },
                new[] { "route_length", "native_shake_and_unique_mutex_ticks" },
                Array.Empty<string>(),
                new[] { "GameLocation.checkAction", "Bush.performUseAction", "Bush.shake", "FarmerTeam.MarkCollectedNut", "Farmer.gainExperience" }),
            Assumption(
                "fruit_tree_harvest",
                new[] { "foraging.harvest_fruit_tree", "executor.harvest_fruit_tree" },
                new[] { "exact_vanilla_fruit_tree", "nonempty_live_fruit_list", "native_shake_idle", "adjacent_interaction_tile", "menu_clear" },
                new[] { "route_length", "native_shake_and_debris_settlement_ticks" },
                Array.Empty<string>(),
                new[] { "GameLocation.checkAction", "FruitTree.performUseAction", "FruitTree.shake", "FruitTree.GetQuality" }),
            Assumption(
                "wild_tree_product_harvest",
                new[] { "foraging.harvest_tree_product", "executor.harvest_tree_product" },
                new[] { "exact_vanilla_tree", "locked_base_wild_tree_data", "mature_seed_ready_untapped", "native_shake_idle", "empty_toolbar_slot", "adjacent_interaction_tile", "menu_clear" },
                new[] { "route_length", "native_shake_and_debris_settlement_ticks", "complete_stochastic_output_domain" },
                Array.Empty<string>(),
                new[] { "GameLocation.checkAction", "Tree.performUseAction", "Tree.shake", "Utility.tryRollMysteryBox", "Utility.trySpawnRareObject", "Data/WildTrees" }),
            Assumption(
                "garbage_can_rummage",
                new[] { "foraging.rummage_garbage", "executor.rummage_garbage" },
                new[] { "exact_map_Garbage_action", "locked_Data_GarbageCans", "unchecked_today", "deterministic_prediction", "safe_or_no_npc_witness", "empty_toolbar_slot", "adjacent_interaction_tile", "menu_clear" },
                new[] { "route_length", "native_animation_and_debris_settlement_ticks" },
                Array.Empty<string>(),
                new[] { "GameLocation.checkAction", "GameLocation.performAction", "GameLocation.CheckGarbage", "GameLocation.TryGetGarbageItem", "Data/GarbageCans" }),
            Assumption(
                "green_rain_resource_clump",
                new[] { "foraging.clear_green_rain_bushes", "executor.break_current_location_resource_clump" },
                new[] { "exact_vanilla_resource_clump_44_or_46", "axe_available", "perimeter_stand_tile", "menu_clear" },
                new[] { "route_length", "native_axe_animation_ticks", "bounded_secret_note_global_rng" },
                Array.Empty<string>(),
                new[] { "Axe.DoFunction", "GameLocation.performToolAction", "ResourceClump.performToolAction", "ResourceClump.destroy", "Farmer.gainExperience" }),
            Assumption(
                "navigation",
                new[] { "exploration.visit_location", "social.gift_npc", "economy.buy_supplies", "quest.advance" },
                new[] { "passability_verified", "warp_verified", "destination_available", "time_budget" },
                new[] { "route_length", "door_or_warp_rules", "temporary_obstacles", "npc_blockage" },
                new[] { "walking_into_walls", "slow_path_following", "wrong_turns" },
                new[] { "PathFindController", "GameLocation.isCollidingPosition", "GameLocation.warps" }),
            Assumption(
                "crop_farming",
                new[] { "farm.maintain_crops" },
                new[] { "energy_floor", "tool_available", "tile_reachable", "inventory_capacity" },
                new[] { "crop_quality", "extra_harvest", "mixed_seed_crop", "fertilizer_effect" },
                new[] { "missed_tile", "wrong_tool_timing", "slow_watering_micro" },
                new[] { "Crop.phaseDays", "Crop.harvest", "HoeDirt", "Farmer.Stamina" }),
            Assumption(
                "forestry_and_resource_clumps",
                new[] { "exploration.visit_location", "farm.maintain_crops" },
                new[] { "energy_floor", "tool_available", "tile_reachable", "fall_zone_or_debris_capacity" },
                new[] { "drop_tables", "tree_growth", "seed_drop", "resource_clump_health" },
                new[] { "missed_tile", "bad_axe_timing", "poor_pickaxe_timing" },
                new[] { "Tree", "ResourceClump", "GameLocation.resourceClumps" }),
            Assumption(
                "machines_and_processing",
                new[] { "farm.process_machines" },
                new[] { "input_item_verified", "output_slot_available", "inventory_capacity", "machine_ready_state" },
                new[] { "machine_timer", "output_item", "quality_rules" },
                new[] { "misclick_machine", "slow_menu_or_interact_micro" },
                new[] { "MachineDataUtility" }),
            Assumption(
                "animals",
                new[] { "farm.collect_animal_products", "executor.collect_animal_product", "animals.purchase", "executor.choose_animal_purchase_response", "executor.purchase_animal", "animals.manage_animal", "executor.manage_animal", "farm.care_for_pets", "executor.pet_interact", "executor.fill_pet_bowl" },
                new[] { "animal_location_verified", "tool_or_item_available", "inventory_capacity", "time_budget", "pet_daily_grant_state", "pet_bowl_assignment" },
                new[] { "produce_quality", "friendship_mood", "incubator_or_birth_timing", "pet_gift_trigger_and_runtime_observed_selection" },
                new[] { "missed_pet", "poor_milking_shearing_micro", "failed_moving_pet_replan", "missed_pet_bowl_action_tile" },
                new[] { "FarmAnimal", "MilkPail.DoFunction", "Shears.DoFunction", "Pet.checkAction", "Pet.dayUpdate", "PetBowl.performToolAction" }),
            Assumption(
                "shops_and_menus",
                new[] { "economy.buy_supplies", "economy.sell_items" },
                new[] { "menu_identity_verified", "stock_verified", "price_verified", "budget_reserve", "protected_items_verified" },
                new[] { "stock_rules", "dialogue_variation", "limited_stock" },
                new[] { "misclick_purchase", "wrong_scroll", "slow_menu_navigation" },
                new[] { "ShopMenu.forSale", "ShopMenu.itemPriceAndStock", "ShopMenu.safetyTimer", "ShopBuilder.GetShopStock" }),
            Assumption(
                "social_and_quests",
                new[] { "social.gift_npc", "quest.advance" },
                new[] { "npc_identity_verified", "schedule_or_location_verified", "gift_item_verified", "quest_step_verified", "time_window" },
                new[] { "npc_route_changes", "dialogue_variation", "quest_reward_randomness" },
                new[] { "wrong_npc_click", "missed_dialogue_advance", "slow_route_following" },
                new[] { "NPC", "Quest", "PathFindController" }),
            Assumption(
                "festivals_and_minigames",
                new[] { "quest.advance", "exploration.visit_location" },
                new[] { "event_state_verified", "rules_modeled", "time_window", "entry_and_exit_verified" },
                new[] { "event_random_seed", "minigame_reward_rules", "contest_scoring" },
                new[] { "bad_minigame_inputs", "missed_dialogue_or_menu_inputs" },
                new[] { "Event", "DesertFestival", "FishingGame" }),
            Assumption(
                "inventory_chests_and_irreversible_actions",
                new[] { "economy.sell_items", "quest.advance", "farm.process_machines" },
                new[] { "item_identity_verified", "protected_item_policy", "target_container_verified", "state_hash_match" },
                new[] { "stack_merge_rules", "inventory_space", "container_layout" },
                new[] { "misclick_item", "wrong_slot_drag", "slow_menu_navigation" },
                new[] { "ShopMenu", "Farmer.Items", "Item" })
        };

        public IReadOnlyCollection<ExecutionAssumption> All => assumptions;

        public ExecutionAssumption GetRequired(string domainId)
        {
            var match = assumptions.FirstOrDefault(item => item.DomainId == domainId);
            if (match is null)
            {
                throw new KeyNotFoundException("No execution assumption registered for domain: " + domainId);
            }

            return match;
        }

        private static ExecutionAssumption Assumption(
            string domainId,
            string[] options,
            string[] hardConstraints,
            string[] calibrationFactors,
            string[] exclusions,
            string[] anchors)
        {
            return new ExecutionAssumption
            {
                DomainId = domainId,
                AppliesToOptions = options,
                HardConstraints = hardConstraints,
                CalibrationFactors = calibrationFactors,
                PreferencePenaltyExclusions = exclusions,
                DecompiledAnchors = anchors
            };
        }
    }
}
