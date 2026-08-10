using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;

namespace StardewAI.Core.OptionRegistry
{
    public sealed class OptionRegistry
    {
        private readonly Dictionary<string, OptionSpec> options = new Dictionary<string, OptionSpec>();
        private readonly Dictionary<string, string> optionSources = new Dictionary<string, string>();

        public OptionRegistry()
        {
            Register(Option("farm.maintain_crops", "farm", "Maintain farm crops",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "time.season", "time.weather", "current_location.crops", "current_location.planting_context", "locations.collision_grid", "menus.active_menu" },
                new[] { "current loaded crop and soil obligations inspected", "one exact native crop-maintenance primitive produced" },
                new[] { "block_unavailable_required_state", "block_unverified_movement", "block_native_crop_or_soil_rule_drift" }));

            Register(Option("farm.process_machines", "farm", "Process machines",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.inventory_capacity", "farm.machines" },
                new[] { "machine queue and learned machine recipes inspected", "machine service or native crafting action steps produced" },
                new[] { "never_sell_protected_items", "block_unavailable_required_state" }));

            Register(Option("farm.collect_machine_outputs", "farm", "Collect one transparent ready machine output",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "farm.machines", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact current-location ready machine output selected", "native machine collection lifecycle handed to the mechanical executor" },
                new[] { "block_incubator_completion", "block_unready_machine_output", "block_inventory_full", "block_unverified_route", "block_projection_drift" }));

            Register(Option("farm.load_supported_machine_input", "farm", "Load one committed positive-value machine input",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "farm.machines", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact placement-bound machine support intent continued", "one unreserved positive-net input handed to the native machine loader" },
                new[] { "block_missing_machine_support_intent", "block_nonpositive_current_input", "block_reserved_input", "block_unverified_route", "block_prediction_or_ledger_drift" }));

            Register(Option("farm.establish_supported_machine_capacity", "farm", "Establish one committed positive-value machine capacity",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.inventory", "menus.active_menu" },
                new[] { "one bounded positive-value machine support intent selected or continued", "the current craft, placement, or first-load stage handed to its existing native executor" },
                new[] { "block_nonpositive_or_incomplete_machine_support", "block_invalid_or_drifted_machine_support_intent", "block_reserved_material", "block_unverified_route", "block_prediction_or_ledger_drift" }));

            Register(Option("farm.fulfill_machine_task_demand", "farm", "Fulfill one exact machine-backed collection task",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "farm.machines", "quests.active_quests", "quests.special_orders", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact active collection task bound to a ready output or deterministic machine input", "native machine load or collection lifecycle handed to the existing mechanical executor" },
                new[] { "block_nonexact_or_additional_consumption_prediction", "block_task_identity_or_progress_drift", "block_active_material_reservations_without_projection", "block_unverified_route", "block_output_or_context_tag_drift" }));

            Register(Option("farm.collect_animal_products", "farm", "Collect one transparent ready animal product",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "farm.animals", "locations.collision_grid", "menus.active_menu" },
                new[] { "one ready tool-harvest animal selected", "native Milk Pail or Shears lifecycle handed to the mechanical executor" },
                new[] { "block_unready_animal_product", "block_missing_harvest_tool", "block_inventory_full", "block_unverified_route", "block_projection_drift" }));

            Register(Option("farm.care_for_pets", "farm", "Perform one transparent pet-care obligation",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "player.inventory", "player.energy", "farm.pets", "farm.pet_bowls", "quests.mail_received", "locations.collision_grid", "menus.active_menu" },
                new[] { "one daily pet interaction or unwatered assigned bowl selected", "native Pet.checkAction or WateringCan lifecycle handed to the mechanical executor", "immediate and next-day friendship/mail settlement kept distinct" },
                new[] { "block_already_satisfied_pet_care", "block_custom_pet_check_action", "block_missing_safe_slot_or_watering_can", "block_unverified_route", "block_projection_drift", "block_deterministic_claim_for_global_rng_gift_selection" }));

            Register(Option("museum.donate_items", "museum", "Donate one transparent museum item",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.museum", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact undonated inventory item selected", "native MuseumMenu donation lifecycle handed to the mechanical executor", "collection and Rusty Key threshold progress recorded" },
                new[] { "block_non_donatable_or_already_donated_item", "block_museum_mutex", "block_missing_free_display_tile", "block_unverified_route", "block_projection_drift", "block_direct_museum_inventory_achievement_mail_or_event_mutation" }));

            Register(Option("community_center.donate_bundle_items", "community_center", "Donate one transparent Community Center bundle ingredient",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.community_center", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact native-selected bundle ingredient is selected", "route and complete BundleData projection are verified", "native JunimoNoteMenu donation lifecycle is handed to the mechanical executor" },
                new[] { "block_joja_locked_or_route_conflict", "block_incomplete_bundle_projection", "block_bundle_mutex_or_note_unavailable", "block_unverified_route", "block_projection_drift", "block_direct_bundle_inventory_reward_mail_or_route_mutation" }));

            Register(Option("joja.advance_development", "joja", "Purchase one transparent Joja membership or development project",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.joja_development", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact membership or project purchase is selected", "price and irreversible route state are verified", "first native Morris greeting is compiler-owned when required", "native Joja dialogue/menu lifecycle is handed to the mechanical executor" },
                new[] { "block_community_center_locked_or_route_conflict", "block_missing_membership_event", "block_pending_project_order", "block_insufficient_money", "block_unverified_route", "block_projection_drift", "block_direct_money_mail_event_quest_or_world_mutation" }));

            Register(Option("housing.advance_farmhouse", "housing", "Purchase the next transparent farmhouse upgrade",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.marriage_house", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact vanilla farmhouse upgrade is selected", "money and material costs are verified", "native Carpenter dialogue lifecycle is handed to the mechanical executor" },
                new[] { "block_active_construction", "block_robin_absent", "block_insufficient_money_or_materials", "block_unverified_route", "block_projection_drift", "block_direct_money_inventory_or_house_mutation" }));

            Register(Option("skills.read_books", "skills", "Read one transparent inventory book through its native branch",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.inventory", "player.book_candidates", "player.skills_detail", "menus.active_menu" },
                new[] { "one exact inventory book branch selected", "native book use and item consumption handed to the mechanical executor" },
                new[] { "block_native_book_use_gate", "block_incomplete_book_projection", "block_projection_drift", "block_direct_skill_stat_mail_or_recipe_mutation" }));

            Register(Option("economy.buy_supplies", "economy", "Buy one exact supply through a rolling shop route",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "time.time", "time.season", "player.money", "player.seed_inventory", "farm.crop_catalog", "locations.shops", "menus.active_menu" },
                new[] { "exact purchase target rebound", "one route or shop interaction stage compiled", "native purchase verified before objective completion" },
                new[] { "never_spend_below_emergency_reserve", "block_closed_or_unbound_shop", "block_unknown_ui_clicks" }));

            Register(Option("economy.sell_items", "economy", "Sell one exact safe inventory stack through a rolling shop route",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "time.time", "player.location_id", "player.inventory", "locations.shops", "locations.route_graph", "menus.active_menu" },
                new[] { "exact sale target rebound", "one route or shop interaction stage compiled", "native sale verified before objective completion" },
                new[] { "never_sell_protected_items", "block_unaccepted_or_unbound_shop", "block_unknown_ui_clicks" }));

            Register(Option("economy.ship_items", "economy", "Ship items through shipping bin",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.inventory", "farm.shipping_bins", "locations.route_graph", "locations.route_connectors", "world_progress.shipping_collection" },
                new[] { "exact shipping item and bin rebound", "one route, approach, or native deposit stage compiled", "immediate deposit and delayed day settlement recorded" },
                new[] { "never_ship_protected_items", "block_identity_price_or_bin_drift", "one_native_item_per_fresh_snapshot" }));

            Register(Option("inventory.transfer_item", "inventory", "Transfer one exact item quantity between the player inventory and an ordinary placed chest",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "farm.material_inventory_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "one explicit material-transfer intent is projected against the transparent inventory graph", "route and native executor.transfer_material handoff are produced" },
                new[] { "block_missing_transfer_intent", "block_locked_or_unauthorized_chest", "block_inventory_identity_or_quantity_drift", "block_destination_capacity", "block_unverified_route", "block_non_native_inventory_mutation" }));

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

            Register(Option("social.advance_partnership", "social", "Advance one transparent dating, marriage, or Krobus roommate transition",
                OptionBehaviorCategories.SocialStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.married_or_roommate", "player.engaged", "player.spouse", "player.farmhouse_upgrade_level", "npcs.social_interaction", "npcs.friendships", "npcs.schedules", "menus.active_menu", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "exact native partnership branch and relationship item verified", "native social executor handoff envelope produced" },
                new[] { "explicit_player_confirmation_required", "block_unavailable_required_state", "block_rejected_native_partnership_branch", "block_unverified_relationship_item" }));

            Register(Option("quest.advance", "quest", "Advance one transparent quest objective stage",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "quests.active_quests", "quests.special_orders", "quests.completed_special_orders", "quests.accepted_special_order_types", "quests.mail_received", "player.inventory", "player.location_id", "time.time", "world_progress.community_center", "world_progress.achievements" },
                new[] { "typed quest candidate selected", "bound objective stage compiled through the daily action queue" },
                new[] { "block_unavailable_required_state", "block_state_hash_mismatch", "block_unbound_quest_objective_kind" }));

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

            Register(Option("exploration.visit_location", "exploration", "Advance one exact rolling cross-location route step",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "locations.collision_grid", "locations.route_action_branch_coverage", "player.energy", "time.time" },
                new[] { "one connector traversed or one exact route obstacle cleared; fresh snapshot required" },
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

            Register(Option("fishing.collect_crab_pots", "fishing", "Collect one transparent ready crab pot",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "one ready crab pot selected", "native checkAction handed to the mechanical executor" },
                new[] { "block_unready_crab_pot", "block_inventory_full", "block_unverified_route", "block_incomplete_output_projection" }));

            Register(Option("fishing.service_fish_ponds", "fishing", "Collect one ready fish-pond output or complete one ready pond request",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.safe_item_context", "farm.buildings", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact fish-pond branch selected", "native FishPond.doAction handed to the mechanical executor" },
                new[] { "block_unready_fish_pond", "block_output_precedes_request", "block_inventory_or_toolbar", "block_unverified_route", "block_projection_drift" }));

            Register(Option("foraging.collect_spawned_objects", "foraging", "Collect one transparent spawned object candidate",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "one current spawned-object candidate selected", "native pickup handed to the mechanical executor" },
                new[] { "block_unknown_spawned_object", "block_inventory_full", "block_unverified_route", "block_direct_object_mutation" }));

            Register(Option("foraging.harvest_ginger", "foraging", "Hoe one transparent ginger forage crop",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "current_location.terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact ginger crop selected", "native Hoe lifecycle handed to the mechanical executor" },
                new[] { "block_no_ginger_crop", "block_missing_hoe", "block_insufficient_energy", "block_unverified_route", "block_projection_drift" }));

            Register(Option("foraging.harvest_bushes", "foraging", "Shake one transparent harvest-ready bush",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.skills_detail", "current_location.large_terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact berry, tea, or golden-walnut bush selected", "native checkAction and Bush.performUseAction handed to the mechanical executor" },
                new[] { "block_unready_bush", "block_custom_bush_runtime", "block_unverified_perimeter_route", "block_projection_drift", "block_direct_bush_or_reward_mutation" }));

            Register(Option("foraging.clear_green_rain_bushes", "foraging", "Clear one loaded Green Rain ResourceClump",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "current_location.resource_clumps", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact loaded vanilla Green Rain clump selected", "seeded core drops and bounded secret-note probability preserved", "native axe ResourceClump lifecycle handed to the mechanical executor" },
                new[] { "block_custom_or_non_green_rain_clump", "block_missing_axe", "block_unverified_route", "block_projection_drift" }));

            Register(Option("foraging.pan_ore_spot", "foraging", "Pan one transparent active ore spot",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.panning", "locations.collision_grid", "menus.active_menu" },
                new[] { "active ore-pan point and exact native reward multiset selected", "native Pan lifecycle handed to the mechanical executor" },
                new[] { "block_inactive_ore_pan_point", "block_missing_pan", "block_inventory_full", "block_unverified_route", "block_projection_drift" }));

            Register(Option("mining.reach_depth", "mining", "Reach mine depth from transparent current mine state",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "mining.current_mine", "mining.tiles", "mining.objects", "mining.resource_clumps",
                    "mining.monsters", "mining.floor_objectives", "mining.reward_chests", "mining.player_resources"
                },
                new[] { "rolling-horizon current-floor action compiled", "after-state replanning continues until target depth" },
                new[] { "block_unavailable_required_state", "block_impossible_target_depth", "block_unsupported_current_floor_step" }));

            Register(Option("mining.obtain_skull_key", "mining", "Reach ordinary mine floor 120 and claim the native Skull Key reward chest",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "player.has_skull_key", "mining.current_mine", "mining.tiles", "mining.objects", "mining.resource_clumps",
                    "mining.monsters", "mining.floor_objectives", "mining.reward_chests", "mining.player_resources"
                },
                new[] { "ordinary mine floor 120 reached", "native reward chest claimed", "player.has_skull_key observed true" },
                new[] { "block_wrong_mine_family", "block_missing_skull_key_chest", "block_unverified_skull_key_postcondition" }));

            Register(Option("mining.claim_reward_chests", "mining", "Claim one transparent ordinary-mine or Skull Cavern reward chest",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "mining.current_mine", "mining.reward_chests", "mining.tiles", "mining.player_resources", "player.inventory", "player.skills_detail", "menus.active_menu" },
                new[] { "one exact live reward chest selected", "one native reward-open starts the lid animation", "after dumpContents empties the chest a separate empty-chest cleanup removes it" },
                new[] { "block_skull_key_specialized_chain", "block_unknown_chest_family", "block_non_vanilla_chest_shape", "block_inventory_full", "block_projection_drift", "block_claim_click_before_dump" }));

            Register(Option("mining.acquire_golden_scythe", "mining", "Acquire the Golden Scythe from the Quarry Mine side branch",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "mining.current_mine", "mining.tiles", "mining.objects", "mining.resource_clumps",
                    "mining.monsters", "mining.floor_objectives", "mining.reward_chests", "mining.player_resources"
                },
                new[] { "rolling-horizon Quarry Mine action compiled", "native altar grants the Golden Scythe", "claimed altar performs the native return warp" },
                new[] { "block_not_quarry_mine_77377", "block_missing_golden_scythe_altar", "block_full_inventory", "block_unsupported_current_floor_step" }));

            Register(Option("volcano.reach_caldera", "volcano", "Reach the Caldera through the Volcano Dungeon",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "volcano.current_level", "volcano.tiles", "volcano.connectors", "volcano.gates",
                    "volcano.objects", "volcano.monsters", "volcano.player_resources"
                },
                new[] { "rolling-horizon current-level action compiled", "native forward warps advance through levels 0 to 9", "Caldera arrival is observed" },
                new[] { "block_unavailable_required_state", "block_unimplemented_volcano_primitive", "block_direct_warp_or_gate_mutation" }));

            Register(Option("recovery.stabilize_day", "recovery", "Stabilize current day",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[]
                {
                    "time.time", "player.energy", "menus.active_menu", "menus.sleep_prompt_context",
                    "player.location_id", "player.tile_x", "player.tile_y", "current_location.home_context",
                    "locations.collision_grid", "locations.route_action_branch_coverage"
                },
                new[] { "urgent risks inspected", "one transparent rolling-horizon route or terminal sleep step produced" },
                new[] { "block_state_hash_mismatch", "block_unverified_home_route", "block_mutation_in_observer_or_planner_mode" }));

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
                new[]
                {
                    "player.location_id", "player.tile_x", "player.tile_y",
                    "locations.collision_grid", "locations.route_action_branch_coverage", "locations.route_connectors"
                },
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

            Register(Option("executor.sell_shop_item", "economy", "Sell one exact inventory stack through the active shop",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.money", "player.inventory", "menus.active_menu", "menus.sell_context" },
                new[] { "native ShopMenu inventory click applied", "exact inventory and money deltas verified" },
                new[] { "never_sell_protected_items", "block_custom_on_sell", "block_non_money_shop", "block_candidate_or_price_drift" }));

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

            Register(Option("executor.claim_mine_reward_chest", "mining", "Claim one verified MineShaft reward chest through its native open animation",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.reward_chests", "mining.tiles", "player.inventory", "player.skills_detail", "menus.active_menu" },
                new[] { "BFS reaches one adjacent stand tile", "one native reward-open starts the lid animation", "only after native dumpContents empties the chest does an empty-chest cleanup interaction remove it" },
                new[] { "block_target_not_exact_reward_chest", "block_inventory_full", "block_menu_or_player_busy", "block_projection_drift", "block_claim_click_before_dump", "block_direct_reward_or_experience_mutation" }));

            Register(Option("executor.mine_stone", "mining", "Mine one transparent breakable stone",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.objects", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "native pickaxe input removes the exact target stone", "combat threats interrupt and resume the tool action" },
                new[] { "block_unknown_stone", "block_missing_pickaxe", "block_unsafe_tool_window", "block_direct_object_mutation" }));

            Register(Option("executor.break_container", "mining", "Break one transparent mine container",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.objects", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "native heavy-hitter input removes the exact container", "released contents remain normal game debris" },
                new[] { "block_unknown_container", "block_missing_heavy_hitter", "block_unsafe_tool_window", "block_direct_object_mutation" }));

            Register(Option("executor.break_resource_clump", "mining", "Remove one transparent MineShaft resource clump",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.resource_clumps", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS reaches one exact perimeter stand tile", "native axe or pickaxe lifecycle removes the exact multi-tile clump", "natural drops remain game debris" },
                new[] { "block_unknown_clump", "block_unsupported_clump_type", "block_missing_required_tool_or_upgrade", "block_unsafe_tool_window", "block_direct_resource_clump_mutation" }));

            Register(Option("executor.combat_monster", "mining", "Defeat one transparent live monster",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS pursuit reaches melee range", "native attack input defeats the exact runtime monster" },
                new[] { "block_unknown_runtime_identity", "block_missing_melee_weapon", "block_direct_monster_damage" }));

            Register(Option("executor.shoot_monster", "mining", "Defeat one transparent live monster with a loaded slingshot",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "clear projectile line is preserved", "native full-charge slingshot input defeats the exact runtime monster", "ammo consumption is observed" },
                new[] { "block_unknown_runtime_identity", "block_missing_loaded_slingshot", "block_projectile_path", "block_direct_monster_damage" }));

            Register(Option("executor.place_bomb", "mining", "Place one transparent bomb and escape its damage square",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.objects", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "native placement consumes the exact bomb", "WASD escape reaches the verified tile before detonation", "natural explosion changes the predicted cluster" },
                new[] { "block_missing_bomb", "block_unverified_fuse_escape", "block_protected_object_in_blast", "block_direct_explosion" }));

            Register(Option("executor.place_staircase", "mining", "Place one transparent staircase on a native-legal MineShaft tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "mining.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "native placement consumes the exact (BC)71 stack", "the projected direct tile becomes a live ladder", "fresh-snapshot replanning reuses executor.descend_ladder" },
                new[] { "block_missing_staircase", "block_native_floor_rule", "block_unknown_or_recursive_relocated_tile", "block_unsafe_interaction_window", "block_direct_ladder_creation" }));

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

            Register(Option("executor.exit_mine", "mining", "Leave the current mine through its native exit",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "mining.current_mine", "mining.tiles", "mining.monsters", "mining.player_resources", "menus.active_menu" },
                new[] { "BFS reaches the live mine exit", "native ExitMine_Leave reaches the exact decompiled destination" },
                new[] { "block_unknown_exit", "block_unreachable_exit", "block_dialogue_mismatch", "block_direct_warp" }));

            Register(Option("executor.cool_volcano_lava", "volcano", "Cool one transparent Volcano Dungeon tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "volcano.current_level", "volcano.tiles", "volcano.monsters", "volcano.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS reaches the verified adjacent stand tile", "native watering-can input adds the exact tile to cooledLavaTiles" },
                new[] { "block_not_loaded_volcano", "block_level_five_cooling", "block_missing_watering_can_or_water", "block_direct_lava_mutation" }));

            Register(Option("executor.break_volcano_stone", "volcano", "Break one transparent Volcano Dungeon stone",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "volcano.current_level", "volcano.tiles", "volcano.objects", "volcano.monsters", "volcano.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS reaches the verified adjacent stand tile", "native pickaxe lifecycle removes the exact stone" },
                new[] { "block_not_loaded_volcano", "block_unknown_stone", "block_missing_pickaxe", "block_unsafe_tool_window", "block_direct_object_mutation" }));

            Register(Option("executor.break_volcano_container", "volcano", "Break one transparent Volcano Dungeon container",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "volcano.current_level", "volcano.tiles", "volcano.objects", "volcano.monsters", "volcano.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS reaches the verified adjacent stand tile", "native heavy-hitter input removes the exact container", "released contents remain normal game debris" },
                new[] { "block_not_loaded_volcano", "block_unknown_container", "block_missing_heavy_hitter", "block_unsafe_tool_window", "block_direct_object_mutation" }));

            Register(Option("executor.combat_volcano_monster", "volcano", "Defeat one transparent Volcano Dungeon monster",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "volcano.current_level", "volcano.tiles", "volcano.monsters", "volcano.player_resources", "player.inventory", "menus.active_menu" },
                new[] { "BFS pursuit reaches melee range", "native melee input defeats the exact runtime monster" },
                new[] { "block_not_loaded_volcano", "block_unknown_runtime_identity", "block_missing_melee_weapon", "block_unverified_monster_semantics", "block_direct_monster_damage" }));

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

            Register(Option("executor.transfer_material", "inventory", "Transfer an exact item quantity between player inventory and one ordinary placed chest",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "farm.material_inventory_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "native chest lock and ItemGrabMenu lifecycle transfers the projected quantity", "source and destination stacks match the transparent projection" },
                new[] { "block_unavailable_material_graph", "block_locked_or_drifted_chest", "block_unverified_inventory_projection", "block_non_native_inventory_mutation" }));

            Register(Option("executor.social_interact", "social", "Execute one transparent social interaction with a current-state NPC",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "npcs.social_interaction", "npcs.friendships", "npcs.gift_tastes", "player.inventory", "menus.active_menu", "locations.collision_grid" },
                new[] { "social interaction executed with observed outcome" },
                new[] { "block_unverified_movement", "block_unavailable_required_state" }));

            Register(Option("executor.quest_npc_interact", "quest", "Advance one exact live quest objective through native NPC interaction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "npcs.social_interaction", "quests.active_quests", "quests.special_orders", "menus.active_menu", "locations.collision_grid" },
                new[] { "matching quest or special-order objective advanced through native NPC action" },
                new[] { "block_unverified_movement", "block_quest_identity_drift", "block_inventory_identity_drift", "block_unobserved_quest_progress" }));

            Register(Option("executor.quest_drop_box_donate", "quest", "Donate one exact inventory stack through a native special-order drop box",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "quests.special_orders", "current_location.map", "menus.active_menu", "locations.collision_grid" },
                new[] { "matching special-order donation advanced through native QuestContainerMenu" },
                new[] { "block_unverified_movement", "block_quest_identity_drift", "block_drop_box_action_drift", "block_inventory_identity_drift", "block_unobserved_quest_progress" }));

            Register(Option("executor.clear_obstacle", "tool", "Clear removable obstacle on target tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "current_location.objects", "current_location.terrain_features", "current_location.map", "menus.active_menu" },
                new[] { "removable obstacle cleared from target tile" },
                new[] { "block_wrong_tool", "block_unremovable_obstacle", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.break_farm_resource_clump", "farm", "Remove one transparent farm stump or hollow log",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "farm.resource_clumps", "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one exact perimeter stand tile", "native multi-frame axe lifecycle removes the exact resource clump", "natural drops remain game debris" },
                new[] { "block_unknown_clump", "block_missing_required_axe_upgrade", "block_unverified_perimeter", "block_menu_unsafe_tool_use", "block_direct_resource_clump_mutation" }));

            Register(Option("executor.break_current_location_resource_clump", "foraging", "Remove one verified loaded Green Rain ResourceClump",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "current_location.resource_clumps", "current_location.debris", "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one exact perimeter stand tile", "native multi-frame axe lifecycle removes the exact clump", "seeded core debris and Foraging XP are verified while optional secret-note output is recorded" },
                new[] { "block_target_identity_drift", "block_missing_axe", "block_unreachable_perimeter", "block_output_projection_drift", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.plant_seed", "farm", "Plant one verified seed on one verified tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.seed_inventory", "current_location.planting_context", "locations.collision_grid", "menus.active_menu" },
                new[] { "seed consumed and crop appears on target tile" },
                new[] { "block_unverified_planting_tile", "block_unverified_seed", "block_menu_unsafe_item_use" }));

            Register(Option("executor.water_crop", "farm", "Water one verified crop with the native watering can lifecycle",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "current_location.crops", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact live crop changes from needing water to watered through native tool input" },
                new[] { "block_unverified_crop_tile", "block_missing_or_empty_watering_can", "block_unverified_route", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.apply_fertilizer", "farm", "Apply one verified fertilizer item to one verified HoeDirt tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.planting_context", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact fertilizer stack decreases and the target HoeDirt records that fertilizer through native placement" },
                new[] { "block_unverified_fertilizer_rule", "block_inventory_identity_drift", "block_unverified_route", "block_menu_unsafe_item_use" }));

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
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.crops", "locations.collision_grid", "menus.active_menu" },
                new[] { "mature crop harvested from target tile" },
                new[] { "block_unverified_harvest_tile", "block_menu_unsafe_item_use", "block_inventory_full_or_unverified_yield" }));

            Register(Option("executor.harvest_giant_crop", "farm", "Harvest one verified giant crop resource clump",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.resource_clumps", "locations.collision_grid", "menus.active_menu" },
                new[] { "giant crop resource clump removed and output debris created" },
                new[] { "block_unverified_giant_crop_clump", "block_missing_axe", "block_menu_unsafe_tool_use" }));

            Register(Option("executor.pickup_debris", "farm", "Pick up one verified collectible debris chunk",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.inventory_capacity", "current_location.debris", "menus.active_menu" },
                new[] { "collectible debris removed and inventory updated" },
                new[] { "block_unverified_debris", "block_inventory_full_or_unverified_item", "block_menu_unsafe_pickup" }));

            Register(Option("executor.collect_spawned_object", "foraging", "Collect one verified spawned object through native checkAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one adjacent stand tile", "native checkAction collects the exact spawned object", "inventory and skill deltas are observed" },
                new[] { "block_unknown_spawned_object", "block_inventory_full", "block_menu_unsafe_pickup", "block_direct_object_mutation" }));

            Register(Option("executor.harvest_ginger", "foraging", "Harvest one verified ginger crop with the native Hoe lifecycle",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "current_location.terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one adjacent stand tile", "native Hoe hit removes only the ginger crop", "ginger debris, energy, and Foraging XP deltas are verified" },
                new[] { "block_target_not_exact_ginger", "block_missing_hoe", "block_insufficient_energy", "block_menu_unsafe_tool_use", "block_projection_drift", "block_direct_crop_or_debris_mutation" }));

            Register(Option("executor.harvest_bush", "foraging", "Harvest one verified bush through native checkAction and Bush shake",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.skills_detail", "current_location.large_terrain_features", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one transparent perimeter stand tile", "native checkAction shakes the exact Bush", "offset, output, XP, and golden-walnut tracker deltas are verified by branch" },
                new[] { "block_target_not_exact_bush", "block_unready_bush", "block_menu_unsafe_interact", "block_projection_drift", "block_direct_bush_debris_inventory_nut_or_skill_mutation" }));

            Register(Option("executor.collect_crab_pot", "fishing", "Collect one verified ready crab pot through native checkAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one adjacent stand tile", "native checkAction collects the exact held output", "inventory, bait, fish collection, and Fishing XP deltas are observed" },
                new[] { "block_unready_crab_pot", "block_inventory_full", "block_menu_unsafe_interact", "block_projection_drift" }));

            Register(Option("executor.collect_fish_pond_output", "fishing", "Collect one verified fish-pond output through native checkAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.safe_item_context", "farm.buildings", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one verified pond-edge stand tile", "native FishPond.doAction collects the exact output", "inventory and Fishing XP deltas are verified" },
                new[] { "block_unready_fish_pond_output", "block_inventory_full", "block_menu_unsafe_interact", "block_projection_drift", "block_direct_pond_inventory_or_skill_mutation" }));

            Register(Option("executor.complete_fish_pond_request", "fishing", "Complete one verified fish-pond population request through native checkAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "farm.buildings", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one verified pond-edge stand tile", "native FishPond.doAction consumes each request item", "population gate and Fishing XP deltas are verified" },
                new[] { "block_unready_fish_pond_request", "block_output_precedence", "block_request_item_toolbar_shortage", "block_drop_in_interception", "block_menu_unsafe_interact", "block_projection_drift", "block_direct_pond_inventory_or_skill_mutation" }));

            Register(Option("executor.collect_animal_product", "farm", "Collect one verified animal product with its native tool",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.energy", "player.inventory", "farm.animals", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one adjacent stand tile", "native Milk Pail or Shears lifecycle targets the exact animal", "produce, inventory, energy, friendship, and Farming XP deltas are verified" },
                new[] { "block_unready_animal_product", "block_missing_harvest_tool", "block_inventory_full", "block_menu_unsafe_tool_use", "block_projection_drift", "block_direct_animal_or_inventory_mutation" }));

            Register(Option("executor.pet_interact", "farm", "Pet one verified pet through native checkAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.safe_item_context", "farm.pets", "menus.active_menu" },
                new[] { "native daily pet interaction completes", "friendship, lastPetDay, timesPet, mail, and observed gift debris are recorded" },
                new[] { "block_custom_pet_check_action", "block_already_petted_or_granted", "block_unsafe_selected_item", "block_projection_drift", "block_direct_pet_or_mail_mutation" }));

            Register(Option("executor.fill_pet_bowl", "farm", "Fill one verified pet bowl through native WateringCan lifecycle",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.energy", "player.inventory", "farm.pet_bowls", "menus.active_menu" },
                new[] { "pet bowl watered state becomes true", "next-day Pet.dayUpdate friendship/mail settlement remains pending" },
                new[] { "block_unassigned_or_watered_bowl", "block_missing_or_empty_watering_can", "block_projection_drift", "block_direct_bowl_or_friendship_mutation" }));

            Register(Option("executor.donate_museum_item", "museum", "Donate one verified item through native MuseumMenu clicks",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.museum", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the Gunther counter", "native LibraryMuseum.OpenDonationMenu and MuseumMenu.receiveLeftClick donate exactly one item", "menu close settles museum rewards through native callbacks" },
                new[] { "block_museum_not_current", "block_museum_mutex", "block_inventory_or_display_tile_drift", "block_unverified_route", "block_direct_museum_inventory_achievement_mail_or_event_mutation" }));

            Register(Option("executor.donate_community_center_item", "community_center", "Donate one verified bundle ingredient through native Junimo Note clicks",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.community_center", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the exact Junimo note", "native CommunityCenter.checkBundle opens the area menu", "native JunimoNoteMenu clicks donate exactly one full ingredient stack" },
                new[] { "block_joja_locked_or_route_conflict", "block_bundle_projection_or_mutex_drift", "block_inventory_or_note_tile_drift", "block_unverified_route", "block_direct_bundle_inventory_reward_mail_or_route_mutation" }));

            Register(Option("executor.purchase_joja_membership", "joja", "Purchase verified Joja membership through native Morris dialogue",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.joja_development", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the JoinJoja counter", "native Morris confirmation deducts exactly 5000g", "JojaMember is queued for tomorrow" },
                new[] { "block_irreversible_route_drift", "block_event_greeting_or_money_drift", "block_unverified_route", "block_direct_money_mail_event_or_quest_mutation" }));

            Register(Option("executor.purchase_joja_project", "joja", "Purchase one verified Joja development project through native JojaCDMenu",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.joja_development", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the JoinJoja counter", "native JojaCDMenu clicks one exact project checkbox", "paired cc and joja completion mails are queued for tomorrow" },
                new[] { "block_membership_or_project_order_drift", "block_project_price_or_button_drift", "block_unverified_route", "block_direct_money_mail_event_or_world_mutation" }));

            Register(Option("executor.purchase_farmhouse_upgrade", "housing", "Purchase one verified farmhouse upgrade through native Carpenter dialogue",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.marriage_house", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches Robin's counter", "native Carpenter Upgrade and Yes responses deduct exact costs", "three-day construction countdown starts" },
                new[] { "block_active_construction", "block_robin_or_upgrade_tuple_drift", "block_unverified_route", "block_direct_money_inventory_or_house_mutation" }));

            Register(Option("executor.pan_ore_spot", "foraging", "Pan one verified active ore spot with the native Pan",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.panning", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one legal shore stand tile", "native Pan lifecycle consumes the active point", "exact output multiset, TimesPanned, Mining XP, Foraging XP, and post-use point state are verified" },
                new[] { "block_inactive_ore_pan_point", "block_missing_pan", "block_inventory_full", "block_menu_unsafe_tool_use", "block_projection_drift", "block_direct_inventory_or_skill_mutation" }));

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

            Register(Option("executor.name_hatched_animal", "farm", "Confirm one native incubator hatch naming menu",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "farm.machines", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "one exact animal added", "selected incubator egg cleared", "native naming menu closed" },
                new[] { "block_non_naming_menu", "block_animal_house_full", "block_native_incubator_selection_drift", "block_direct_animal_or_machine_mutation" }));

            Register(Option("executor.craft_machine_item", "farm", "Craft one verified learned machine through the native personal or Workbench CraftingPage",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.machine_crafting", "menus.active_menu" },
                new[] { "native CraftingPage consumes the rebound ingredient multiset", "Workbench sources acquire and release native workbench and container mutexes", "exact machine output enters player inventory", "native recipe count, quest callbacks, and achievement checks run" },
                new[] { "block_unknown_or_unlearned_recipe", "block_recipe_inventory_or_workbench_topology_drift", "block_unowned_or_locked_workbench_container", "block_output_capacity", "block_direct_inventory_recipe_stat_quest_or_achievement_mutation" }));

            Register(Option("executor.craft_storage_item", "farm", "Craft one verified ordinary storage item for a transparent bootstrap or exhausted-capacity demand",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.storage_crafting", "player.storage_placement", "farm.chests", "farm.material_inventory_graph", "menus.active_menu" },
                new[] { "native CraftingPage consumes the rebound ingredient multiset", "exact ordinary storage output enters player inventory", "native recipe count, quest callbacks, and achievement checks run", "the existing storage placement chain becomes eligible" },
                new[] { "block_unknown_or_unlearned_storage_recipe", "block_existing_inventory_storage_or_available_capacity", "block_recipe_inventory_or_workbench_topology_drift", "block_output_capacity", "block_direct_inventory_recipe_stat_quest_or_achievement_mutation" }));

            Register(Option("executor.craft_quest_item", "quest", "Craft one exact active CraftingQuest target through the shared native personal or Workbench CraftingPage",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.quest_crafting", "quests.active_quests", "menus.active_menu" },
                new[] { "native CraftingPage consumes the rebound ingredient multiset", "exact quest output enters player inventory", "the exact CraftingQuest completes through Quest.OnRecipeCrafted" },
                new[] { "block_non_CraftingQuest_or_completed_quest", "block_unknown_or_unlearned_target_recipe", "block_recipe_inventory_or_workbench_topology_drift", "block_material_reservation_or_output_capacity", "block_direct_inventory_recipe_stat_or_quest_mutation" }));

            Register(Option("executor.place_machine", "farm", "Place one verified inventory machine at one exact native-legal tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.machine_placement", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates the machine at the rebound tile", "one matching inventory item is consumed through native placement callbacks" },
                new[] { "block_inventory_machine_identity_drift", "block_location_or_tile_projection_drift", "block_unreachable_adjacent_stand", "block_material_reservation_drift", "block_native_placement_recheck" }));

            Register(Option("executor.remove_machine", "farm", "Remove one explicitly selected idle machine through the native pickaxe and debris path",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "farm.machines", "locations.collision_grid", "menus.active_menu" },
                new[] { "native tool callbacks remove the exact source machine", "one matching recoverable machine debris item is created for the existing pickup and placement chain" },
                new[] { "block_without_explicit_relocation_intent", "block_machine_identity_or_projection_drift", "block_processing_ready_or_attached_machine", "block_unverified_runtime_tool_override", "block_nonrecoverable_fragility", "block_native_post_state_mismatch" }));

            Register(Option("executor.place_storage", "farm", "Place one verified inventory storage item at one route-safe native-legal tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.storage_placement", "current_location.chests", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates one player chest at the rebound tile", "one matching inventory item is consumed through native placement callbacks", "existing route and storage access remain connected in the pre-dispatch projection" },
                new[] { "block_inventory_storage_identity_drift", "block_location_layout_or_projection_drift", "block_unreachable_or_route_disconnect_placement", "block_material_reservation_drift", "block_native_placement_recheck" }));

            Register(Option("executor.read_book", "skills", "Read one verified inventory book through native performUseAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.book_candidates", "player.skills_detail", "menus.active_menu" },
                new[] { "native book animation starts and one item is consumed", "skill XP, permanent level, new-level queue, mastery, stat, mail, recipe, and feedback deltas are verified", "native animation settling wait is scheduled" },
                new[] { "block_native_book_use_gate", "block_inventory_identity_drift", "block_projection_drift", "block_direct_skill_stat_mail_or_recipe_mutation" }));

            Register(Option("executor.select_safe_item_slot", "inventory", "Select safe toolbar slot",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.current_tool_index", "player.active_object_qualified_id", "player.safe_item_context" },
                new[] { "safe toolbar slot selected" },
                new[] { "block_safe_slot_unavailable", "block_toolbar_slot_out_of_range" }));

            ValidateRegistryCompleteness();
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

        private void Register(
            OptionSpec spec,
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            RegisterCore(spec, sourceFile, sourceLine);
        }

        internal void RegisterForValidation(OptionSpec spec, string sourceFile, int sourceLine)
        {
            RegisterCore(spec, sourceFile, sourceLine);
        }

        private void RegisterCore(OptionSpec spec, string sourceFile, int sourceLine)
        {
            OptionGovernanceCatalog.Validate(spec);
            var source = $"{Path.GetFileName(sourceFile)}:{sourceLine}";
            if (!options.TryAdd(spec.OptionId, spec))
            {
                var firstSource = optionSources.TryGetValue(spec.OptionId, out var registeredSource)
                    ? registeredSource
                    : "unknown";
                throw new InvalidOperationException(
                    $"Duplicate OptionSpec id '{spec.OptionId}'; first={firstSource}; conflicting={source}.");
            }

            optionSources.Add(spec.OptionId, source);
        }

        private void ValidateRegistryCompleteness()
        {
            var registeredIds = options.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var capabilityIds = OptionCapabilityRegistrySource.RegisteredIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (options.Count != OptionGovernanceCatalog.Count ||
                !registeredIds.SequenceEqual(capabilityIds, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Option registry baseline mismatch: " +
                    $"registry={options.Count};governance={OptionGovernanceCatalog.Count};" +
                    $"capability={capabilityIds.Length};" +
                    $"missing_from_registry={string.Join(",", capabilityIds.Except(registeredIds, StringComparer.Ordinal))};" +
                    $"missing_from_capability={string.Join(",", registeredIds.Except(capabilityIds, StringComparer.Ordinal))}.");
            }
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
            return OptionGovernanceCatalog.Apply(new OptionSpec
            {
                OptionId = id,
                Domain = domain,
                Name = name,
                BehaviorCategory = behaviorCategory,
                CompilerResponsibility = compilerResponsibility,
                TrainingRole = trainingRole,
                RequiredStateFactors = requiredStateFactors,
                EstimatedEffects = expectedEffects,
                SafetyConstraints = safetyConstraints
            });
        }
    }
}
