using System.Collections.Generic;
using StardewAI.Contracts.Options;

namespace StardewAI.Core.OptionRegistry
{
    public sealed class OptionRegistry
    {
        private readonly Dictionary<string, OptionSpec> options = new Dictionary<string, OptionSpec>();

        public OptionRegistry()
        {
            Register(Option("farm.maintain_crops", "farm", "Maintain farm crops",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.energy", "time.season", "time.weather", "farm.crops" },
                new[] { "crop obligations inspected", "crop maintenance action steps produced" },
                new[] { "block_unavailable_required_state", "block_unverified_movement" }));

            Register(Option("farm.process_machines", "farm", "Process machines",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.inventory_capacity", "farm.machines" },
                new[] { "machine queue inspected", "machine action steps produced" },
                new[] { "never_sell_protected_items", "block_unavailable_required_state" }));

            Register(Option("economy.buy_supplies", "economy", "Buy supplies preview",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "time.time", "time.season", "player.money", "player.seed_inventory", "farm.crop_catalog", "locations.shops", "menus.active_menu" },
                new[] { "purchase list verified", "budget impact previewed" },
                new[] { "never_spend_below_emergency_reserve", "block_unknown_ui_clicks" }));

            Register(Option("economy.sell_items", "economy", "Sell items in active shop only",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.inventory", "menus.active_menu", "menus.sell_context" },
                new[] { "sell candidates previewed" },
                new[] { "never_sell_protected_items", "block_unknown_ui_clicks" }));

            Register(Option("economy.ship_items", "economy", "Ship items through shipping bin",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.inventory", "farm.shipping_bins" },
                new[] { "shipping candidates previewed" },
                new[] { "never_ship_protected_items", "block_no_completed_shipping_bin", "block_no_route_to_bin" }));

            Register(Option("social.talk_npc", "social", "Talk to NPC current-state plan",
                OptionBehaviorCategories.SocialStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.active_dialogue_events", "npcs.social_interaction", "npcs.friendships", "npcs.schedules", "menus.active_menu", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "talk target verified", "native social executor handoff envelope produced" },
                new[] { "block_unavailable_required_state", "block_incomplete_social_legality" }));

            Register(Option("social.gift_npc", "social", "Gift NPC current-state plan",
                OptionBehaviorCategories.SocialStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "time.year", "time.is_green_rain", "player.location_id", "player.tile_x", "player.tile_y", "player.spouse", "player.active_dialogue_events", "player.inventory", "npcs.social_interaction", "npcs.friendships", "npcs.schedules", "npcs.gift_tastes", "menus.active_menu", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "gift target and exact owned item verified", "native social executor handoff envelope produced" },
                new[] { "never_gift_protected_items", "block_unavailable_required_state", "block_incomplete_social_legality" }));

            Register(Option("quest.advance", "quest", "Advance quest preview",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "quests.active_quests", "quests.special_orders", "quests.completed_special_orders", "quests.accepted_special_order_types", "quests.mail_received", "player.inventory", "player.location_id", "time.time", "world_progress.community_center", "world_progress.achievements" },
                new[] { "quest candidate selected", "quest compiler envelope produced with live evidence" },
                new[] { "block_unavailable_required_state", "block_state_hash_mismatch", "quest_native_executor_not_implemented" }));

            Register(Option("strategy.grandpa_progress", "strategy", "Improve Grandpa evaluation score",
                OptionBehaviorCategories.LongTermStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[]
                {
                    "player.total_money_earned",
                    "player.level",
                    "world_progress.achievements",
                    "world_progress.community_center",
                    "npcs.friendships",
                    "quests.mail_received",
                    "farm.grandpa_score"
                },
                new[] { "grandpa evaluation direction selected", "score delta target estimated" },
                new[] { "block_unavailable_required_state", "block_state_hash_mismatch" }));

            Register(Option("exploration.visit_location", "exploration", "Visit location preview",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "locations.collision_grid", "locations.route_action_branch_coverage", "player.energy", "time.time" },
                new[] { "route previewed" },
                new[] { "block_unverified_movement", "block_unavailable_required_state" }));

            Register(Option("fishing.catch_fish", "fishing", "Catch fish from a transparent legal cast candidate",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory",
                    "menus.active_menu", "current_location.map", "locations.collision_grid",
                    "fishing.location_context", "fishing.fishable_tiles", "fishing.rod_inventory",
                    "fishing.rod_contexts", "fishing.active_cast_state"
                },
                new[] { "legal cast candidate selected", "catch attempt handed to the fishing executor" },
                new[] { "block_unresolved_fishing_context", "block_illegal_cast_geometry", "block_unobserved_catch_result" }));

            Register(Option("mining.reach_depth", "mining", "Reach mine depth from transparent current mine state",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "mining.current_mine", "mining.tiles", "mining.objects", "mining.monsters",
                    "mining.floor_objectives", "mining.player_resources"
                },
                new[] { "rolling-horizon current-floor action compiled", "after-state replanning continues until target depth" },
                new[] { "block_unavailable_required_state", "block_impossible_target_depth", "block_unsupported_current_floor_step" }));

            Register(Option("recovery.stabilize_day", "recovery", "Stabilize current day",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time", "player.energy", "menus.active_menu", "menus.sleep_prompt_context", "player.location_id", "player.tile_x", "player.tile_y" },
                new[] { "urgent risks inspected", "safe stopping plan produced" },
                new[] { "block_state_hash_mismatch", "block_mutation_in_observer_or_planner_mode" }));

            Register(Option("executor.move_to_tile", "movement", "Move to tile safely",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.map" },
                new[] { "collision-safe tile movement requested" },
                new[] { "block_unverified_movement", "block_direct_coordinate_teleport" }));

            Register(Option("executor.traverse_connector", "movement", "Traverse expected map connector",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.map", "current_location.warps", "locations.route_connectors" },
                new[] { "collision-safe connector traversal requested" },
                new[] { "block_unverified_connector", "block_unexpected_target_location", "block_direct_coordinate_teleport" }));

            Register(Option("executor.face_direction", "movement", "Face direction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.facing_direction" },
                new[] { "player facing direction changed" },
                new[] { "block_invalid_direction" }));

            Register(Option("executor.interact", "interaction", "Interact with adjacent transparent tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.facing_direction", "current_location.route_context", "menus.active_menu", "locations.route_action_branch_coverage" },
                new[] { "transparent adjacent interaction requested" },
                new[] { "block_unknown_interaction", "block_unsupported_action_branch", "block_menu_unsafe_interaction" }));

            Register(Option("executor.buy_shop_item", "economy", "Buy one safe shop item",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.money", "player.inventory", "menus.active_menu" },
                new[] { "safe shop item purchased" },
                new[] { "block_unmodeled_purchase_side_effects", "block_unknown_ui_clicks", "block_budget_mismatch" }));

            Register(Option("executor.choose_dialogue_response", "interaction", "Choose whitelisted dialogue response",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "menus.active_menu", "menus.menu_specific_state" },
                new[] { "whitelisted dialogue response selected" },
                new[] { "block_unknown_dialogue_response", "block_unmodeled_dialogue_side_effects" }));

            Register(Option("executor.sleep", "recovery", "Terminal sleep macro",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time", "player.location_id", "player.tile_x", "player.tile_y", "current_location.home_context", "menus.active_menu", "menus.sleep_prompt_context", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "terminal sleep touch-action macro compiled" },
                new[] { "block_sleep_not_terminal", "block_sleep_target_unverified", "block_sleep_prompt_unsafe" }));

            Register(Option("executor.close_menu", "recovery", "Close safe active menu",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "menus.active_menu", "menus.sleep_prompt_context" },
                new[] { "safe menu closed or verified absent" },
                new[] { "block_unknown_menu_close", "block_sleep_prompt_close", "block_menu_not_ready_to_close" }));

            Register(Option("executor.wait_ticks", "timing", "Wait for ticks",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time" },
                new[] { "execution waits without mutation" },
                new[] { "block_unbounded_wait" }));

            Register(Option("executor.mine_stone", "mining", "Mine one transparent breakable stone",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.objects", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "native pickaxe input removes the exact target stone", "combat threats interrupt and resume the tool action" },
                new[] { "block_unknown_stone", "block_missing_pickaxe", "block_unsafe_tool_window", "block_direct_object_mutation" }));

            Register(Option("executor.combat_monster", "mining", "Defeat one transparent live monster",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS pursuit reaches melee range", "native attack input defeats the exact runtime monster" },
                new[] { "block_unknown_runtime_identity", "block_missing_melee_weapon", "block_direct_monster_damage" }));

            Register(Option("executor.consume_food", "recovery", "Consume one transparent healing food",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.monsters", "mining.player_resources", "player.inventory", "player.health", "player.energy", "menus.active_menu" },
                new[] { "native Eat confirmation consumes one exact food", "health and energy recovery are observed", "previous toolbar slot is restored" },
                new[] { "block_unknown_food", "block_tool_or_menu_conflict", "block_direct_health_mutation" }));

            Register(Option("executor.descend_ladder", "mining", "Descend one transparent mine ladder",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "menus.active_menu" },
                new[] { "BFS reaches ladder interaction range", "native MineShaft ladder action loads the next floor" },
                new[] { "block_unknown_ladder", "block_unsafe_interaction_window", "block_direct_mine_level_mutation" }));

            Register(Option("executor.descend_shaft", "mining", "Descend one transparent Skull Cavern shaft",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "mining.player_resources", "menus.active_menu" },
                new[] { "BFS reaches shaft interaction range", "native Shaft_Jump confirmation applies the exact previewed fall and health cost" },
                new[] { "block_unknown_shaft", "block_health_reserve", "block_dialogue_mismatch", "block_direct_mine_level_mutation" }));

            Register(Option("executor.catch_fish", "fishing", "Execute one legal fishing attempt",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[]
                {
                    "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory",
                    "menus.active_menu", "current_location.map", "locations.collision_grid",
                    "fishing.location_context", "fishing.fishable_tiles", "fishing.rod_inventory",
                    "fishing.rod_contexts", "fishing.active_cast_state"
                },
                new[] { "legal fishing input lifecycle executed", "catch outcome observed" },
                new[] { "block_wrong_location_or_stand_tile", "block_invalid_rod_or_bobber_tile", "block_unobserved_catch_result" }));

            Register(Option("executor.ship_inventory_item_to_bin", "economy", "Put one inventory item into a completed shipping bin safely",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.inventory", "player.location_id", "player.tile_x", "player.tile_y", "farm.shipping_bins", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "inventory item deposited into shipping bin" },
                new[] { "never_ship_protected_items", "block_no_completed_shipping_bin", "block_unverified_movement", "block_unavailable_required_state" }));

            Register(Option("executor.social_interact", "social", "Execute one transparent social interaction with a current-state NPC",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "npcs.social_interaction", "npcs.friendships", "npcs.gift_tastes", "player.inventory", "menus.active_menu", "locations.collision_grid" },
                new[] { "social interaction executed with observed outcome" },
                new[] { "block_unverified_movement", "block_unavailable_required_state" }));

            Register(Option("executor.clear_obstacle", "tool", "Clear removable obstacle on target tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "current_location.objects", "current_location.terrain_features", "current_location.map", "menus.active_menu" },
                new[] { "removable obstacle cleared from target tile" },
                new[] { "block_wrong_tool", "block_unremovable_obstacle", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.plant_seed", "farm", "Plant one verified seed on one verified tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.seed_inventory", "current_location.planting_context", "menus.active_menu" },
                new[] { "seed consumed and crop appears on target tile" },
                new[] { "block_unverified_planting_tile", "block_unverified_seed", "block_menu_unsafe_item_use" }));

            Register(Option("executor.till_soil", "farm", "Till one eligible farm tile with the native hoe",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "current_location.map", "locations.collision_grid", "menus.active_menu" },
                new[] { "native Hoe creates HoeDirt on target tile" },
                new[] { "block_unverified_till_tile", "block_missing_hoe", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.harvest_crop", "farm", "Harvest one verified mature crop tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "farm.crops", "menus.active_menu" },
                new[] { "mature crop harvested from target tile" },
                new[] { "block_unverified_harvest_tile", "block_menu_unsafe_item_use", "block_inventory_full_or_unverified_yield" }));

            Register(Option("executor.harvest_giant_crop", "farm", "Harvest one verified giant crop resource clump",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "farm.resource_clumps", "menus.active_menu" },
                new[] { "giant crop resource clump removed and output debris created" },
                new[] { "block_unverified_giant_crop_clump", "block_missing_axe", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.pickup_debris", "farm", "Pick up one verified collectible debris chunk",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.inventory_capacity", "farm.debris", "menus.active_menu" },
                new[] { "collectible debris removed and inventory updated" },
                new[] { "block_unverified_debris", "block_inventory_full_or_unverified_item", "block_menu_unsafe_pickup" }));

            Register(Option("executor.collect_machine_output", "farm", "Collect one verified ready machine output",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.inventory_capacity", "farm.machines", "menus.active_menu" },
                new[] { "machine output removed and inventory updated" },
                new[] { "block_unverified_machine_output", "block_inventory_full_or_unverified_item", "block_menu_unsafe_interact" }));

            Register(Option("executor.load_machine_input", "farm", "Load one verified input into one machine",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "farm.machines", "menus.active_menu" },
                new[] { "machine input consumed and processing started" },
                new[] { "block_unverified_machine_input", "block_machine_busy", "block_menu_unsafe_interact" }));

            Register(Option("executor.select_safe_item_slot", "inventory", "Select safe toolbar slot",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.current_tool_index", "player.active_object_qualified_id", "player.safe_item_context" },
                new[] { "safe toolbar slot selected" },
                new[] { "block_safe_slot_unavailable", "block_toolbar_slot_out_of_range" }));
        }

        public OptionSpec GetRequired(string optionId)
        {
            if (!options.TryGetValue(optionId, out var spec))
            {
                throw new KeyNotFoundException("No registered OptionSpec for intent: " + optionId);
            }

            return spec;
        }

        public IReadOnlyCollection<OptionSpec> All => options.Values;

        private void Register(OptionSpec spec)
        {
            options[spec.OptionId] = spec;
        }

        private static OptionSpec Option(
            string id,
            string domain,
            string name,
            string behaviorCategory,
            string compilerResponsibility,
            string trainingRole,
            string[] requiredStateFactors,
            string[] expectedEffects,
            string[] safetyConstraints)
        {
            return new OptionSpec
            {
                OptionId = id,
                Domain = domain,
                Name = name,
                BehaviorCategory = behaviorCategory,
                CompilerResponsibility = compilerResponsibility,
                TrainingRole = trainingRole,
                RequiredStateFactors = requiredStateFactors,
                EstimatedEffects = expectedEffects,
                SafetyConstraints = safetyConstraints,
                IrreversibleEffects = new string[0],
                RiskLevel = "low",
                Recoverability = "recoverable"
            };
        }
    }
}
