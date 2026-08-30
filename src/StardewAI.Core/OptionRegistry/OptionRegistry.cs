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

            Register(Option("animals.withdraw_feed_hopper_hay", "animals", "Withdraw the exact native hay stack needed by one animal house",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "player.inventory", "current_location.objects", "menus.active_menu" },
                new[] { "one exact base Feed Hopper with unfed animals and one safe adjacent stand selected", "compiler rebinds silo hay, animal occupancy, trough occupancy and exact native withdrawal", "one native GameLocation.checkAction transfers hay from the root silo to player inventory" },
                new[] { "block_non_animal_house_or_drifted_feed_hopper", "block_no_unfed_animals", "block_silo_empty_or_trough_full", "block_inventory_rejects_exact_stack", "block_destructive_object_trap_preamble", "block_no_safe_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_direct_silo_or_inventory_mutation" }));

            Register(Option("animals.collect_auto_grabber_contents", "animals", "Collect every currently inventory-acceptable stack from one exact Auto-Grabber",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "player.inventory", "current_location.objects", "menus.active_menu" },
                new[] { "one exact base Auto-Grabber with nonempty native held Chest and one safe adjacent stand selected", "compiler rebinds the exact transferable and retained stack projections from a fresh snapshot", "native GameLocation.checkAction opens ItemGrabMenu and native menu clicks transfer only projected stacks" },
                new[] { "block_drifted_or_empty_auto_grabber", "block_inventory_rejects_all_stacks", "block_content_identity_or_quantity_drift", "block_no_safe_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_non_native_chest_or_inventory_mutation", "block_native_menu_or_lock_lifecycle_mismatch" }));

            Register(Option("movement.use_mini_obelisk", "movement", "Use one exact native Mini-Obelisk route endpoint",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "current_location.objects", "menus.active_menu" },
                new[] { "one source in the native first Mini-Obelisk pair and one exact safe stand selected", "compiler rebinds native pair order, farther destination and down-left-right-up landing from a fresh snapshot", "one native GameLocation.checkAction performs the delayed same-location teleport", "pair identity, exact landing and selected toolbar slot are verified" },
                new[] { "block_native_pair_missing_or_non_base", "block_source_not_in_native_first_pair", "block_no_safe_toolbar_slot", "block_no_safe_source_stand_or_native_landing", "block_menu_or_player_busy", "block_pair_destination_or_collision_projection_drift", "block_direct_player_position_mutation" }));

            Register(Option("farming.collect_slime_ball", "farming", "Collect one exact natural Slime Hutch Slime Ball through its native object action",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "player.inventory", "current_location.objects", "current_location.debris", "menus.active_menu" },
                new[] { "one exact natural fragility-2 Slime Ball and adjacent stand selected", "compiler rebinds the deterministic day seed and both predicted outputs", "one native GameLocation.checkAction removes the ball and creates debris", "shared debris pickup owns subsequent collection" },
                new[] { "block_non_natural_or_drifted_slime_ball", "block_destructive_object_trap_preamble", "block_no_empty_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_seed_or_output_projection_drift", "block_direct_object_removal_or_debris_creation" }));

            Register(Option("animals.purchase", "animals", "Purchase one exact animal into one exact compatible home",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "time.time", "player.location_id", "player.tile_x", "player.tile_y", "player.money", "farm.animal_purchase_catalog", "locations.route_graph", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "one exact unlocked animal and compatible nonfull home selected", "rolling route and native Marnie dialogue stages compiled", "native PurchaseAnimalsMenu adoption and exact money, home, owner, type and name receipts verified" },
                new[] { "block_closed_or_unbound_animal_shop", "block_missing_or_full_animal_home", "block_insufficient_money", "block_purchase_projection_drift", "block_direct_animal_adoption_or_money_mutation" }));

            Register(Option("animals.manage_animal", "animals", "Apply one explicit native management operation to one exact animal",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.Mixed,
                new[] { "time.time", "player.location_id", "player.tile_x", "player.tile_y", "player.money", "farm.animals", "locations.route_graph", "locations.collision_grid", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "one exact animal and rename, reproduction, move-home, or sell intent selected", "rolling route reaches the moving animal and native AnimalQueryMenu performs the operation", "exact animal identity, home, money, name, or reproduction receipt verified" },
                new[] { "block_missing_explicit_animal_management_intent_or_reason", "block_unavailable_animal_query", "block_duplicate_name_or_invalid_reproduction_target", "block_incompatible_or_full_home", "block_unconfirmed_sale", "block_projection_drift", "block_direct_animal_or_money_mutation" }));

            Register(Option("crafting.cook_recipe", "crafting", "Cook one explicitly selected learned recipe for one stated purpose",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "player.cooking", "locations.route_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact learned recipe, count, purpose and native kitchen or cookout source selected", "rolling route reaches the selected source", "shared native CraftingPage mechanics consume exact materials and seasoning and verify output, recipe count, quests and achievements" },
                new[] { "block_missing_explicit_recipe_count_or_purpose", "block_unknown_recipe_or_unavailable_source", "block_material_container_or_mutex_drift", "block_insufficient_materials_or_output_capacity", "block_direct_inventory_recipe_stat_quest_or_achievement_mutation" }));

            Register(Option("crafting.forge_item", "crafting", "Forge, enchant, combine, or unforge one exact live item pair for one stated purpose",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "player.forge", "locations.route_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact operation, live input source set, purpose and loaded native forge selected", "rolling route reaches the selected forge", "shared native ForgeMenu mechanics consume exact inputs and shards and verify deterministic output or complete random result domain" },
                new[] { "block_missing_explicit_operation_inputs_or_purpose", "block_unknown_input_or_unavailable_forge", "block_insufficient_shards_or_output_capacity", "block_projection_or_random_domain_drift", "block_direct_inventory_enchantment_ring_stat_or_achievement_mutation" }));

            Register(Option("buildings.construct", "buildings", "Construct one exact purpose-bound building through the native builder service",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.inventory", "player.building_construction_catalog", "locations.route_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact live native blueprint and buildable location selected", "compiler chooses an exact native-valid placement and builder route", "the shared native construction executor starts construction and verifies money, materials and building state" },
                new[] { "block_missing_explicit_building_type_location_or_reason", "block_build_condition_resource_or_placement_drift", "block_active_construction", "block_unverified_route", "block_direct_money_inventory_or_building_mutation" }));

            Register(Option("buildings.change_skin", "buildings", "Change one exact building to one explicitly selected native skin",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.building_skin_catalog", "locations.route_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact building, target skin and appearance reason selected", "compiler freezes the live native menu order and shortest mutation-safe click sequence", "native Robin and BuildingSkinMenu flow applies the skin and verifies paint reset" },
                new[] { "block_missing_explicit_building_skin_identity_or_reason", "block_permission_condition_or_menu_order_drift", "block_active_construction_or_upgrade", "block_unverified_route", "block_direct_skin_or_paint_mutation" }));

            Register(Option("buildings.paint", "buildings", "Paint one exact native building region to an explicitly selected color or default",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.building_paint_catalog", "locations.route_graph", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact building, paint region, mouse-reachable color or default and appearance reason selected", "compiler freezes the live native ranges, current values and Robin service", "shared native Carpenter target flow applies and verifies the exact region" },
                new[] { "block_missing_explicit_building_region_color_or_reason", "block_permission_condition_or_mouse_quantization_drift", "block_active_construction_or_upgrade", "block_unverified_route", "block_direct_paint_mutation" }));

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

            Register(Option("island.field_office_donate", "island", "Donate one transparent Island Field Office fossil",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.island_field_office", "world_progress.golden_walnuts", "locations.collision_grid", "locations.route_connectors", "menus.active_menu" },
                new[] { "one exact owned fossil and native display slot selected", "route continuation retains fossil and slot identity", "native FieldOfficeMenu donation and set reward settlement handed to the mechanical executor" },
                new[] { "block_locked_field_office", "block_field_office_mutex", "block_missing_professor_or_desk", "block_inventory_piece_or_reward_projection_drift", "block_unverified_route", "block_direct_piece_reward_nut_mail_or_finale_mutation" }));

            Register(Option("island.field_office_survey", "island", "Answer the next transparent Island Field Office survey",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.island_field_office", "world_progress.golden_walnuts", "current_location.debris", "locations.collision_grid", "locations.route_connectors", "menus.active_menu" },
                new[] { "the unique next survey and locked vanilla numeric answer selected", "route continuation retains survey identity", "native survey dialogues and plant/nut/debris/finale settlement handed to the mechanical executor" },
                new[] { "block_locked_or_completed_field_office", "block_failed_survey_today", "block_missing_professor_or_survey_endpoint", "block_unverified_route", "block_projection_drift", "block_direct_plant_failed_lock_nut_debris_mail_or_finale_mutation" }));

            Register(Option("festival.manage_grange_display", "festival", "Prepare the best available Stardew Valley Fair grange display and retrieve it after judging",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.grange_display", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact fresh-snapshot display removal or placement selected", "live sell prices quality categories and shared display state rebound", "native festival StorageContainer and grange mutex lifecycle handed to the mechanical executor" },
                new[] { "block_inactive_festival_or_judging_transition", "block_grange_mutex", "block_inventory_capacity", "block_unverified_route", "block_projection_drift", "block_direct_team_display_inventory_score_or_judging_mutation" }));

            Register(Option("festival.play_fishing_game", "festival", "Play one native Stardew Valley Fair fishing game while the unacquired Stardrop still needs star tokens",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.fair_fishing_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "one 50g 100-second native FishingGame session selected from a fresh fair snapshot", "automatic repetition is bounded by the unacquired 2000-token Stardrop after projected unclaimed grange tokens", "native festival dialogue and predictive legal-input session handed to the mechanical executor" },
                new[] { "block_inactive_or_changed_fair_event", "block_no_remaining_automatic_star_token_demand", "block_insufficient_money", "block_unverified_route", "block_projection_drift", "block_direct_money_score_fish_timer_reward_or_inventory_mutation" }));

            Register(Option("festival.play_slingshot_game", "festival", "Play one native Stardew Valley Fair slingshot game while the unacquired Stardrop still needs star tokens",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.fair_slingshot_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "one 50g 50-second native TargetGame session selected from a fresh fair snapshot", "automatic repetition is bounded by the same unacquired 2000-token Stardrop demand as the other Fair games", "native festival dialogue and predictive moving-target intercept input handed to the mechanical executor" },
                new[] { "block_inactive_or_changed_fair_event", "block_no_remaining_automatic_star_token_demand", "block_insufficient_money", "block_unverified_route", "block_projection_drift", "block_direct_money_target_score_accuracy_reward_timer_or_inventory_mutation" }));

            Register(Option("festival.play_strength_game", "festival", "Play one native Stardew Valley Fair strength game to close an exact one-star-token Stardrop shortfall",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.fair_strength_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "one free native StrengthGame session selected only for an exact one-token automatic Stardrop shortfall", "the live Buildings tile 540 endpoint and required player x=29 stand are rebound from a fresh snapshot", "native maximum-power timing input is handed to the mechanical executor" },
                new[] { "block_inactive_or_changed_fair_event", "block_remaining_automatic_star_token_demand_not_exactly_one", "block_unverified_route", "block_projection_drift", "block_direct_power_timer_score_reward_animation_or_inventory_mutation" }));

            Register(Option("festival.spin_wheel", "festival", "Make one bounded green wager on the native Stardew Valley Fair spinning wheel for the unacquired Stardrop token deficit",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.fair_wheel_spin", "player.luck_context", "locations.collision_grid", "menus.active_menu" },
                new[] { "one native stochastic WheelSpinGame selected from a fresh fair snapshot", "green zero-luck 22-of-30 constructor distribution and effective LuckLevel are transparent", "wager is the exact zero-luck Kelly fraction 7/15 of current star tokens capped by the remaining unacquired Stardrop demand" },
                new[] { "block_inactive_or_changed_fair_event", "block_remaining_demand_below_two", "block_fewer_than_two_wagerable_star_tokens", "block_unverified_route", "block_projection_drift", "block_direct_rng_rotation_wager_score_result_or_menu_mutation" }));

            Register(Option("minigame.play_calico_jack", "minigame", "Play one native CalicoJack round only while the missing Casino Rarecrow creates an exact Qi-coin deficit",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.club_coins", "player.has_club_card", "player.calico_jack", "locations.collision_grid", "menus.active_menu" },
                new[] { "one native low- or high-stakes round is selected from a fresh exact seed projection", "automatic demand exists only while (BC)126 is absent and the Deluxe Scarecrow dependency remains open", "the shared deterministic hidden-card and future-draw decision model chooses native hit or stand input", "the executor quits after one native settlement so every coin delta is auditable" },
                new[] { "block_no_rarecrow_currency_demand", "block_club_card_or_seed_coins_missing", "block_projected_loss_of_last_seed_coins", "block_unverified_route", "block_projection_or_rng_replay_drift", "block_direct_card_rng_coin_result_or_minigame_mutation" }));

            Register(Option("minigame.play_slots", "minigame", "Play one bounded native Slots spin while the missing Casino Rarecrow creates an exact Qi-coin deficit",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.club_coins", "player.has_club_card", "player.slots", "locations.collision_grid", "menus.active_menu" },
                new[] { "one native 10- or 100-coin spin is selected from a fresh demand and Luck projection", "the complete locked payout threshold distribution and expected value are transparent while shared RNG remains live feedback", "automatic demand exists only while (BC)126 is absent and the Deluxe Scarecrow dependency remains open", "the executor exits after one settlement so every stochastic coin delta is auditable" },
                new[] { "block_no_rarecrow_currency_demand", "block_club_card_or_seed_coins_missing", "block_unverified_route", "block_projection_drift", "block_unverified_native_random_settlement_or_cleanup", "block_direct_rng_reel_coin_result_or_stat_mutation" }));

            Register(Option("minigame.play_crane_game", "minigame", "Play one explicitly authorized native Movie Theater Crane Game session",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.inventory", "player.crane_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "the exact live Movie Theater machine occupancy, fee, movie prize rules and active physics are transparent", "one explicit command authorizes exactly 500g and the native three-attempt session", "the executor selects a reachable live prize afresh for each attempt and transfers all rewards through the native menu" },
                new[] { "block_without_explicit_player_command", "block_occupied_machine_or_insufficient_gold_or_reward_capacity", "block_unverified_route_or_projection_drift", "block_direct_rng_money_prize_position_state_or_inventory_mutation" }));

            Register(Option("minigame.play_darts", "minigame", "Win the next native Pirate Cove darts round while one of its three limited Golden Walnuts remains",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.darts_game", "world_progress.golden_walnuts", "locations.collision_grid", "menus.active_menu" },
                new[] { "one session exists only on a live non-raining even-day pirate night while the Darts limited drop count is below three", "the exact 20, 15 or 10 dart allowance is rebound from the team drop count", "native mouse aim and charge-release input completes the 301-point board in at most six throws", "native dialogue and FarmerTeam limited-nut machinery issue exactly the next reward" },
                new[] { "block_not_pirate_night_or_three_rewards_complete", "block_live_DartsGame_endpoint_or_route_unavailable", "block_projection_drift", "block_unverified_native_score_dialogue_reward_or_cleanup", "block_direct_score_dart_count_timer_rng_reward_or_progress_mutation" }));

            Register(Option("minigame.play_prairie_king", "minigame", "Schedule one AI-only timed-equivalent Prairie King completion while the no-death objective remains open",
                OptionBehaviorCategories.LongTermStrategic,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.prairie_king", "locations.collision_grid", "menus.active_menu" },
                new[] { "the AI actor enters the live Saloon arcade and spends the conservative equivalent session budget", "the native AbigailGame completion branch increments completion and no-death stats and applies mail and achievement side effects", "the candidate disappears upstream after no-death completion" },
                new[] { "block_no_death_completion_already_recorded", "block_non_ai_actor", "block_unverified_route_or_arcade_endpoint", "block_projection_drift", "block_direct_stats_mail_achievement_inventory_or_reward_mutation", "block_native_proxy_play_until_post_training_explicit_player_command" }));

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

            Register(Option("housing.renovate", "housing", "Apply one exact explicit farmhouse renovation through the native Robin renovation flow",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.marriage_house", "locations.route_graph", "locations.route_connectors", "locations.collision_grid", "menus.active_menu" },
                new[] { "player command selects one exact renovation ID and region with a reason and confirmation", "compiler rebinds all 18 live Data/HomeRenovations branches, native shop order, cost, requirements, actions, bounds and obstructions", "native Carpenter, HouseRenovations ShopMenu and RenovateMenu lifecycle is handed to the mechanical executor", "money, FirstPurchase marker, renovation action state, event, map update and return are verified" },
                new[] { "block_non_player_command_or_missing_confirmation", "block_destructive_branch_without_destructive_confirmation", "block_data_catalog_menu_order_or_projection_drift", "block_unsatisfied_requirement_or_crib_family_gate", "block_insufficient_money", "block_selected_region_obstructed", "block_unverified_route", "block_direct_money_mail_NetInt_map_furniture_menu_viewport_or_event_mutation" }));

            Register(Option("skills.read_books", "skills", "Read one transparent inventory book through its native branch",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.inventory", "player.book_candidates", "player.skills_detail", "menus.active_menu" },
                new[] { "one exact inventory book branch selected", "native book use and item consumption handed to the mechanical executor" },
                new[] { "block_native_book_use_gate", "block_incomplete_book_projection", "block_projection_drift", "block_direct_skill_stat_mail_or_recipe_mutation" }));

            Register(Option("skills.choose_profession", "skills", "Choose one exact profession offered by the active native level-up menu",
                OptionBehaviorCategories.LongTermStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.skills_detail", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "all exact offered profession identities projected", "one strategy-selected profession compiled to the shared native level-up completion executor", "profession, immediate perk, pending level and menu receipts verified" },
                new[] { "block_non_profession_level_up", "block_incomplete_level_up_projection", "block_profession_identity_drift", "block_direct_duplicate_level_up_executor" }));

            Register(Option("mail.process_letter", "mail", "Process the next exact mailbox letter through the native mailbox and LetterViewerMenu lifecycle",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.Mixed,
                new[] { "quests.mailbox_processing", "quests.mailbox", "quests.mail_received", "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "locations.route_graph", "locations.route_connectors", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "native-order mailbox identity rebound", "one rolling route, approach, open, or exact LetterViewer completion stage compiled", "mailbox removal, attachment, quest, special-order and menu receipts verified" },
                new[] { "block_mail_data_or_directive_parse_failure", "block_unowned_mailbox", "block_attachment_capacity_insufficient", "block_mail_identity_drift", "block_direct_mail_inventory_recipe_money_quest_or_special_order_mutation" }));

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

            Register(Option("quest.accept_daily", "quest", "Accept today's transparent help-wanted quest through the native board",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "quests.daily_quest_offer", "player.location_id", "player.tile_x", "player.tile_y", "locations.route_graph", "locations.route_connectors", "menus.active_menu" },
                new[] { "exact offer identity rebound", "one route or native board acceptance stage compiled", "native quest-log receipt verified" },
                new[] { "block_daily_quest_offer_missing", "block_offer_identity_drift", "block_unverified_board_endpoint", "block_direct_quest_state_mutation" }));

            Register(Option("quest.accept_special_order", "quest", "Select and accept one exact transparent special order through its native board",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "quests.special_order_boards", "quests.special_orders", "quests.accepted_special_order_types", "player.location_id", "player.tile_x", "player.tile_y", "locations.route_graph", "locations.route_connectors", "menus.active_menu" },
                new[] { "one exact board and offer identity selected", "one rolling route, board, dialogue, or acceptance stage compiled", "native team special-order receipt verified" },
                new[] { "block_board_locked_or_unloaded", "block_offer_identity_or_generation_seed_drift", "block_order_type_already_accepted", "block_unverified_board_endpoint", "block_direct_special_order_state_mutation" }));

            Register(Option("quest.claim_reward", "quest", "Claim one exact completed ordinary quest money reward through the native QuestLog",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "quests.claimable_rewards", "quests.active_quests", "player.money", "menus.active_menu" },
                new[] { "exact reward identity rebound", "native QuestLog selection and reward clicks compiled", "money and reward-consumption receipt verified" },
                new[] { "block_hidden_or_incomplete_quest", "block_reward_identity_drift", "block_money_drift", "block_direct_money_or_quest_state_mutation" }));

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
                    "player.inventory_capacity",
                    "menus.active_menu", "current_location.map", "locations.collision_grid",
                    "fishing.location_context", "fishing.fishable_tiles", "fishing.rod_inventory",
                    "fishing.rod_contexts", "fishing.active_cast_state"
                },
                new[] { "legal cast candidate selected", "catch attempt handed to the fishing executor" },
                new[] { "block_unresolved_fishing_context", "block_illegal_cast_geometry", "block_inventory_full", "block_unobserved_catch_result" }));

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

            Register(Option("fishing.manage_fish_pond", "fishing", "Manage one exact Fish Pond through its native query menu for an explicit player request",
                OptionBehaviorCategories.LongTermStrategic,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "farm.buildings", "menus.active_menu" },
                new[] { "player command names one exact pond and cycle_netting or empty_pond operation", "compiler rebinds live pond state, edge stand, safe slot, native menu contract and exact expected receipt", "runtime right-click opens the bound PondQueryMenu and clicks only its public native controls", "netting cycles modulo four or ClearPond returns exact fish debris and verifies all reset and preserved fields" },
                new[] { "block_not_explicitly_authorized", "block_management_operation_or_reason_missing", "block_empty_pond_confirmation_missing", "block_output_precedes_management", "block_no_exact_live_pond_or_adjacent_stand", "block_menu_or_player_busy", "block_pond_projection_drift", "block_direct_fish_pond_state_mutation" }));

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

            Register(Option("foraging.harvest_fruit_tree", "foraging", "Shake one transparent fruit-bearing FruitTree",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.terrain_features", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact mature fruit-bearing FruitTree selected", "all live fruit slots and lightning substitution preserved", "native checkAction and FruitTree shake handed to the mechanical executor" },
                new[] { "block_unready_or_empty_fruit_tree", "block_custom_fruit_tree_runtime", "block_native_shake_in_progress", "block_unverified_adjacent_route", "block_projection_drift", "block_direct_tree_debris_inventory_or_skill_mutation" }));

            Register(Option("foraging.harvest_tree_product", "foraging", "Shake one transparent seed-bearing wild Tree",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "player.skills_detail", "current_location.terrain_features", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact mature untapped seed-bearing base Tree selected", "guaranteed output and complete native stochastic output domain preserved without consuming RNG", "native checkAction and Tree shake handed to the mechanical executor" },
                new[] { "block_unready_or_seedless_tree", "block_custom_or_data_drifted_tree", "block_tapped_tree", "block_native_shake_in_progress", "block_empty_toolbar_slot_unavailable", "block_unverified_adjacent_route", "block_projection_drift", "block_direct_tree_rng_debris_inventory_or_skill_mutation" }));

            Register(Option("foraging.rummage_garbage", "foraging", "Rummage one transparent unchecked garbage can",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.safe_item_context", "current_location.garbage_cans", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact unchecked map Garbage action selected", "deterministic native item and delivery projection preserved without consuming RNG", "native checkAction and CheckGarbage handed to the mechanical executor" },
                new[] { "block_checked_or_unknown_can", "block_data_or_prediction_drift", "block_negative_friendship_witness", "block_direct_inventory_capacity", "block_empty_toolbar_slot_unavailable", "block_unverified_adjacent_route", "block_direct_checked_stat_friendship_inventory_debris_or_rng_mutation" }));

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

            Register(Option("mining.use_elevator", "mining", "Select one unlocked ordinary-mine elevator checkpoint through the native elevator menu",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[]
                {
                    "player.location_id", "player.tile_x", "player.tile_y", "player.deepest_mine_level", "player.current_mine_level",
                    "current_location.mine_elevator_action_tiles", "locations.collision_grid", "locations.route_graph", "locations.route_connectors",
                    "menus.active_menu", "menus.menu_specific_state"
                },
                new[] { "ordinary-mine elevator endpoint reached and opened natively", "one exact offered checkpoint selected through MineElevatorMenu", "native destination location and level verified" },
                new[] { "block_non_ordinary_mine_family", "block_locked_or_non_checkpoint_floor", "block_current_floor", "block_floor_zero_outside_mineshaft", "block_menu_identity_drift", "block_direct_enter_mine_or_warp" }));

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

            Register(Option("mining.activate_calico_statue", "mining", "Accept one exact projected Desert Festival Calico Statue effect on the loaded Skull Cavern floor",
                OptionBehaviorCategories.LongTermStrategic,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "mining.current_mine", "mining.calico_statue", "mining.tiles", "menus.active_menu" },
                new[] { "small model accepts or rejects the exact day-save-seeded next effect", "compiler replays the fresh seed and binds the one live unactivated tile and adjacent stand", "shared BFS reaches the statue", "one native MineShaft.checkAction advances rating and applies the exact team effect" },
                new[] { "block_not_desert_festival_skull_cavern", "block_no_unactivated_calico_statue", "block_projected_effect_changed", "block_no_reachable_adjacent_stand", "block_menu_or_player_busy", "block_seed_tile_or_team_state_drift", "block_direct_rating_effect_reward_health_stamina_buff_tile_or_rng_mutation" }));

            Register(Option("multiplayer.manage_wallet", "multiplayer", "Execute one explicitly confirmed native multiplayer wallet command",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.multiplayer_wallet", "menus.active_menu" },
                new[] { "player selects one exact schedule, cancellation, or transfer command", "compiler rebinds current wallet mode, participant balances, recipient response key and live LedgerBook stand", "shared BFS reaches the ManorHouse ledger", "native dialogue or digit-menu input produces an immediate receipt", "scheduled mode changes settle only at the native next-day wallet barrier" },
                new[] { "block_without_explicit_command_reason_and_confirmation", "block_mode_change_when_not_host", "block_transfer_outside_separate_wallet_mode", "block_unknown_recipient_or_invalid_amount", "block_menu_or_ledger_unavailable", "block_projection_or_balance_drift", "block_direct_wallet_flag_money_balance_or_stat_mutation" }));

            Register(Option("rewards.claim_pot_of_gold", "rewards", "Claim the exact Spring 17 Forest Pot of Gold through its native object interaction",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.pot_of_gold_reward", "current_location.debris", "menus.active_menu" },
                new[] { "shared BFS reaches one exact transparent adjacent stand", "one native GameLocation.checkAction removes the Pot of Gold", "the exact year-scaled GoldCoin and LeprechuanHat rewards remain conserved across inventory and ordinary debris pickup" },
                new[] { "block_not_spring_17_forest", "block_exact_pot_missing_or_drifted", "block_no_adjacent_stand", "block_player_or_menu_busy", "block_native_reward_debris_receipt_mismatch", "block_direct_object_inventory_or_debris_mutation" }));

            Register(Option("mining.choose_dwarf_statue_power", "mining", "Choose one exact daily power offered by a Dwarf King Statue",
                OptionBehaviorCategories.LongTermStrategic,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.StrategyValue,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.dwarf_king_statue_power", "menus.active_menu" },
                new[] { "small model selects one of exactly two deterministic daily powers", "compiler rebinds an exact live statue and adjacent stand", "native object action opens the exact icon menu", "native menu click applies the selected day buff" },
                new[] { "block_mining_mastery_locked", "block_existing_dwarf_statue_buff", "block_selected_power_not_offered", "block_no_exact_live_statue_or_adjacent_stand", "block_menu_or_player_busy", "block_offer_or_menu_drift", "block_direct_production_buff_mutation" }));

            Register(Option("rewards.claim_statue_blessing", "rewards", "Claim the deterministic daily farming-mastery Statue of Blessings reward",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.Mixed,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.statue_blessing", "menus.active_menu" },
                new[] { "small model emits one parameterless claim goal", "compiler binds the exact deterministic daily blessing and one live statue stand", "native object action applies the blessing and daily claim lock", "post-state verifies exactly the predicted blessing buff" },
                new[] { "block_farming_mastery_locked", "block_already_blessed_today", "block_no_exact_live_statue_or_adjacent_stand", "block_menu_or_player_busy", "block_day_weather_festival_or_object_drift", "block_direct_production_buff_mutation" }));

            Register(Option("world.rotate_house_plant", "world", "Rotate one exact placed House Plant through its native object interaction",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "current_location.objects", "menus.active_menu" },
                new[] { "one exact base House Plant and adjacent stand selected", "compiler binds an empty toolbar slot and current visual frame", "one native GameLocation.checkAction advances the observed frame", "permanent item identity and selected toolbar slot are preserved" },
                new[] { "block_not_explicitly_authorized", "block_non_base_or_non_house_plant_object", "block_no_empty_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_object_frame_or_identity_drift", "block_direct_parent_sheet_index_mutation" }));

            Register(Option("world.play_singing_stone", "world", "Play one exact placed Singing Stone through its native randomized crystal sound interaction",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "current_location.objects", "menus.active_menu" },
                new[] { "one exact base (BC)94 Singing Stone and adjacent stand selected", "compiler binds a safe toolbar slot and the complete native pitch distribution", "one native GameLocation.checkAction emits the crystal sound and sets shakeTimer to 100", "object identity and selected toolbar slot are preserved" },
                new[] { "block_not_explicitly_authorized", "block_non_base_or_non_singing_stone_object", "block_no_safe_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_object_or_distribution_projection_drift", "never_guess_exact_shared_rng_pitch" }));

            Register(Option("world.tune_flute_block", "world", "Advance one exact placed Flute Block by one native persistent pitch step for an explicit player request",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "current_location.objects", "menus.active_menu" },
                new[] { "one exact base (O)464 Flute Block and adjacent stand selected", "compiler rebinds current and next pitch plus a safe empty/tool slot", "one native GameLocation.checkAction advances persistent pitch and plays the base flute cue", "shake/scale receipt, item identity and selected toolbar slot are verified" },
                new[] { "block_not_explicitly_authorized", "block_non_base_or_non_flute_block_object", "block_no_safe_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_pitch_or_identity_projection_drift", "never_merge_adjacent_playback_with_tuning", "block_direct_pitch_or_animation_mutation" }));

            Register(Option("world.tune_drum_block", "world", "Advance one exact placed Drum Block by one native persistent tone step for an explicit player request",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "current_location.objects", "menus.active_menu" },
                new[] { "one exact base (O)463 Drum Block and adjacent stand selected", "compiler rebinds current and next tone plus a safe empty/tool slot", "one native GameLocation.checkAction advances persistent tone and plays drumkit0..6", "shake/scale receipt, item identity and selected toolbar slot are verified" },
                new[] { "block_not_explicitly_authorized", "block_non_base_or_non_drum_block_object", "block_no_safe_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_tone_or_identity_projection_drift", "never_merge_adjacent_playback_with_tuning", "block_direct_tone_or_animation_mutation" }));

            Register(Option("farming.read_farm_computer_report", "farming", "Open one exact Farm Computer native report for an explicit player request",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "current_location.objects", "menus.active_menu" },
                new[] { "transparent bridge publishes the exact native root-location aggregate and localized report", "compiler rebinds one exact base (BC)239 object and adjacent stand", "one native GameLocation.checkAction opens the delayed DialogueBox", "runtime verifies the exact report digest while preserving object identity and selected toolbar slot" },
                new[] { "block_not_explicitly_authorized", "block_non_base_or_non_farm_computer_object", "block_no_safe_toolbar_slot", "block_no_adjacent_stand", "block_menu_or_player_busy", "block_root_aggregate_or_report_projection_drift", "never_require_menu_read_for_strategy_information" }));

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

            Register(Option("executor.accept_daily_quest", "quest", "Accept the exact visible daily quest through the native Billboard button",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "quests.daily_quest_offer", "menus.active_menu" },
                new[] { "native Billboard receiveLeftClick invoked", "offer appears in actor quest log", "acceptedDailyQuest and two-day deadline verified" },
                new[] { "block_non_daily_billboard", "block_offer_identity_drift", "block_hidden_accept_button", "block_direct_quest_state_mutation" }));

            Register(Option("executor.accept_special_order", "quest", "Accept one exact visible offer through the native SpecialOrdersBoard button",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "quests.special_order_boards", "quests.special_orders", "quests.accepted_special_order_types", "menus.active_menu" },
                new[] { "native SpecialOrdersBoard receiveLeftClick invoked", "matching quest key and generation seed appear in team special orders", "matching order type becomes accepted" },
                new[] { "block_non_special_orders_board", "block_offer_identity_or_selection_drift", "block_hidden_accept_button", "block_direct_special_order_state_mutation" }));

            Register(Option("executor.claim_quest_reward", "quest", "Select and claim one exact ordinary quest money reward through native QuestLog clicks",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "quests.claimable_rewards", "quests.active_quests", "player.money", "menus.active_menu" },
                new[] { "native QuestLog row and rewardBox receiveLeftClick invoked", "money increased by exact reward", "native OnMoneyRewardClaimed and OnLeaveQuestPage effects verified" },
                new[] { "block_quest_log_identity_drift", "block_reward_not_claimable", "block_money_receipt_mismatch", "block_direct_money_or_quest_state_mutation" }));

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

            Register(Option("executor.choose_animal_purchase_response", "animals", "Choose one exact native animal-purchase dialogue response",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "farm.animal_purchase_catalog", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "exact Marnie Purchase or paged location response invoked through GameLocation.answerDialogue", "expected PurchaseAnimalsMenu or exact paged response transition observed" },
                new[] { "block_non_animal_purchase_dialogue", "block_response_key_drift", "block_unexpected_menu_result", "block_direct_menu_state_mutation" }));

            Register(Option("executor.purchase_animal", "animals", "Complete one exact native PurchaseAnimalsMenu transaction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.money", "farm.animal_purchase_catalog", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "exact stock button, compatible home and compiler-generated unique name selected", "native PurchaseAnimalsMenu lifecycle completed", "new animal identity, type, owner, home, name, occupancy and money delta verified" },
                new[] { "block_stock_or_home_projection_drift", "block_animal_house_full", "block_insufficient_money", "block_nonunique_name", "block_direct_animal_adoption_or_money_mutation" }));

            Register(Option("executor.manage_animal", "animals", "Drive one exact operation through the native AnimalQueryMenu",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time", "player.location_id", "player.tile_x", "player.tile_y", "player.money", "farm.animals", "locations.collision_grid", "menus.active_menu", "menus.menu_specific_state" },
                new[] { "shared moving-target navigation reaches the exact animal", "native pet/check-action opens AnimalQueryMenu", "native menu control applies and verifies the requested operation" },
                new[] { "block_animal_or_stand_tile_drift", "block_animal_query_menu_drift", "block_rename_reproduction_home_or_sale_projection_drift", "block_direct_animal_or_money_mutation" }));

            Register(Option("executor.cook_recipe", "crafting", "Cook one rebound recipe through the native kitchen or Cookout Kit CraftingPage",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "player.cooking", "locations.collision_grid", "menus.active_menu" },
                new[] { "native source interaction opens cooking CraftingPage", "native ingredient and Qi Seasoning consumption occurs in exact source order", "exact output, quality, order data, recipesCooked, quest callbacks and cooking achievements are verified" },
                new[] { "block_recipe_source_or_material_projection_drift", "block_foreign_kitchen_mutex", "block_output_capacity", "block_native_menu_or_postcondition_mismatch", "block_direct_inventory_recipe_stat_quest_or_achievement_mutation" }));

            Register(Option("executor.forge_item", "crafting", "Drive one rebound forge, enchant, ring combine, or unforge operation through the native ForgeMenu",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.inventory_capacity", "player.forge", "locations.collision_grid", "menus.active_menu" },
                new[] { "native forge action or Mini-Forge opens ForgeMenu", "native inventory or equipped-ring clicks fill exact slots and run one 1600ms operation", "exact input, shard, timesEnchanted, output state or complete native random-domain receipt is verified" },
                new[] { "block_forge_source_input_or_state_projection_drift", "block_insufficient_shards_or_output_capacity", "block_native_menu_or_postcondition_mismatch", "block_random_output_outside_native_domain", "block_direct_inventory_enchantment_ring_stat_or_achievement_mutation" }));

            Register(Option("executor.sleep", "recovery", "Terminal sleep macro",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time", "player.location_id", "player.tile_x", "player.tile_y", "current_location.home_context", "menus.active_menu", "menus.sleep_prompt_context", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "terminal sleep touch-action macro compiled" },
                new[] { "block_sleep_not_terminal", "block_sleep_target_unverified", "block_sleep_prompt_unsafe" }));

            Register(Option("recovery.sleep_in_tent", "recovery", "End the day in one exact placed Tent through the native temporary-bed branch",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time", "time.total_days", "player.location_id", "player.tile_x", "player.tile_y", "player.temporary_sleep", "current_location.large_terrain_features", "menus.active_menu", "menus.tent_sleep_prompt_context", "locations.collision_grid", "locations.route_action_branch_coverage" },
                new[] { "native SleepTent prompt and SleepTent_Yes start the shared sleep lifecycle", "date advances exactly one day and the player wakes at the same location and tile", "post-sleep save settles, the temporary-bed flag resets, and the exact Tent is destroyed" },
                new[] { "block_tent_sleep_not_terminal", "block_tent_identity_health_geometry_or_path_drift", "block_native_prompt_gate_closed", "block_cross_day_wake_save_or_destruction_receipt_mismatch", "block_direct_sleep_flag_date_location_or_tent_mutation" }));

            Register(Option("recovery.escape_object_trap", "recovery", "Recover from four cardinal blocking objects by removing one exact safely recoverable adjacent machine",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.object_trap_recovery", "player.active_object_qualified_id", "menus.active_menu", "current_location.objects", "farm.machines" },
                new[] { "four cardinal non-passable objects are observed", "one selected adjacent machine is removed through the existing recoverable native Pickaxe/debris executor", "the destructive Object.checkForAction null-tool fallback remains disabled" },
                new[] { "block_not_trapped", "block_no_safe_adjacent_machine", "block_machine_removal_projection_drift", "block_destructive_null_tool_fallback" }));

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

            Register(Option("executor.activate_calico_statue", "mining", "Execute and verify one native Desert Festival Calico Statue activation",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "mining.current_mine", "mining.calico_statue", "mining.tiles", "menus.active_menu" },
                new[] { "shared BFS reaches the exact adjacent stand", "one native Buildings tile 284 checkAction fires the activation event", "tile 285, rating plus one, activation count plus one and exact seeded effect dictionary are verified", "egg, refresh or speed side effects remain game-owned and are checked from the receipt" },
                new[] { "block_endpoint_or_projection_drift", "block_not_master_seed_receipt", "block_unverified_effect_or_side_effect", "block_unverified_route", "block_direct_rating_effect_reward_health_stamina_buff_tile_or_rng_mutation" }));

            Register(Option("executor.manage_multiplayer_wallet", "multiplayer", "Execute and verify one explicitly confirmed native ManorHouse wallet command",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.multiplayer_wallet", "menus.active_menu" },
                new[] { "shared BFS reaches the exact live LedgerBook stand", "native DialogueBox response clicks select one authorized mode command or recipient", "native DigitEntryMenu clicks enter an exact transfer amount", "pending flag, balances and gifted statistic are checked without production writes", "native next-day settlement is verified separately for shared-to-separate and separate-to-shared transitions" },
                new[] { "block_without_explicit_command_reason_and_confirmation", "block_mode_change_when_not_host", "block_transfer_outside_separate_wallet_mode", "block_unknown_recipient_or_invalid_amount", "block_endpoint_menu_or_projection_drift", "block_unverified_immediate_or_next_day_receipt", "block_direct_wallet_flag_money_balance_or_stat_mutation" }));

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

            Register(Option("executor.play_junimo_kart", "minigame", "Run one AI-only timed-equivalent Junimo Kart endless session to an exact quest score",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.has_skull_key", "quests.special_orders", "current_location.arcade_action_tiles", "menus.active_menu" },
                new[] { "equivalent session budget elapsed", "MineCart native score-submission callback invoked", "matching JKScoreObjective progress observed" },
                new[] { "block_missing_skull_key", "block_non_ai_execution_target", "block_wrong_minigame_or_mode", "block_unobserved_score_submission", "native_perfect_proxy_reserved_for_post_training_player_command" }));

            Register(Option("executor.play_prairie_king", "minigame", "Execute one AI-only timed-equivalent Prairie King completion through the native settlement branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.prairie_king", "locations.collision_grid", "menus.active_menu" },
                new[] { "native Arcade_Prairie entry and optional NewGame response observed", "equivalent session budget elapsed", "native AbigailGame phase-one settlement and exact stat deltas observed" },
                new[] { "block_non_ai_execution_target", "block_wrong_minigame_or_saved_progress_branch", "block_projection_drift", "block_unobserved_native_settlement", "block_direct_stats_mail_achievement_inventory_or_reward_mutation" }));

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

            Register(Option("executor.apply_tree_treatment", "foraging", "Apply one verified vinegar item to permanently stop moss growth on one verified tree",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "current_location.terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "one exact vinegar stack decreases", "target tree moss is cleared", "target tree permanently stops growing moss through native placement" },
                new[] { "block_missing_treatment_reason", "block_unverified_tree_runtime_type", "block_moss_growth_already_stopped", "block_vinegar_inventory_identity_drift", "block_unverified_route", "block_menu_unsafe_item_use" }));

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

            Register(Option("executor.harvest_fruit_tree", "foraging", "Harvest one verified FruitTree through native checkAction and shake",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "current_location.terrain_features", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the transparent adjacent stand tile", "native checkAction shakes the exact FruitTree", "fruit-list clearing and every qualified item, quality, and quantity delta are verified" },
                new[] { "block_target_not_exact_fruit_tree", "block_unready_or_empty_fruit_tree", "block_menu_unsafe_interact", "block_projection_drift", "block_direct_tree_debris_inventory_or_skill_mutation" }));

            Register(Option("executor.harvest_tree_product", "foraging", "Harvest one verified wild Tree seed product through native checkAction and shake",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.safe_item_context", "player.skills_detail", "current_location.terrain_features", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the transparent adjacent stand tile", "empty toolbar slot is selected and restored", "native checkAction shakes the exact Tree", "seed state, complete output-domain membership, and zero Foraging XP are verified" },
                new[] { "block_target_not_exact_tree", "block_unready_or_seedless_tree", "block_menu_unsafe_interact", "block_safe_slot_drift", "block_data_or_output_domain_drift", "block_direct_tree_rng_debris_inventory_or_skill_mutation" }));

            Register(Option("executor.rummage_garbage", "foraging", "Rummage one verified garbage can through native checkAction and CheckGarbage",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.safe_item_context", "current_location.garbage_cans", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the transparent adjacent stand tile", "empty toolbar slot is selected and restored", "native checkAction rummages the exact Garbage endpoint", "checked set, stat, output receipt and optional Linus friendship delta are verified" },
                new[] { "block_target_action_or_can_id_drift", "block_checked_or_prediction_drift", "block_negative_friendship_witness", "block_menu_unsafe_interact", "block_safe_slot_or_inventory_capacity_drift", "block_direct_checked_stat_friendship_inventory_debris_or_rng_mutation" }));

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

            Register(Option("executor.donate_field_office_piece", "island", "Donate one verified fossil through native FieldOfficeMenu clicks",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "world_progress.island_field_office", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the Field Office desk", "native FieldOfficeDesk mutex and Donate response open FieldOfficeMenu", "inventory and exact display-holder clicks consume one fossil", "native set rewards and collected-nut marker match the locked projection" },
                new[] { "block_field_office_not_current", "block_field_office_mutex", "block_inventory_piece_or_reward_projection_drift", "block_unverified_route", "block_direct_piece_reward_nut_mail_or_finale_mutation" }));

            Register(Option("executor.answer_field_office_survey", "island", "Answer one verified Field Office survey through native dialogue responses",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.island_field_office", "current_location.debris", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the Field Office survey wall", "native Survey Yes and exact Correct responses answer the unique next question", "native plant restore, collected-nut, walnut debris and finale trigger match the locked projection" },
                new[] { "block_field_office_not_current", "block_failed_survey_today", "block_question_or_answer_projection_drift", "block_unverified_route", "block_direct_plant_failed_lock_nut_debris_mail_or_finale_mutation" }));

            Register(Option("executor.manage_grange_display", "festival", "Apply one verified Fair grange display mutation through the native shared StorageContainer",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.grange_display", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one live Fair display interaction tile", "native Event.checkAction and StorageContainer clicks apply one exact removal or placement", "menu close releases the shared grange mutex and verifies inventory score and judging state" },
                new[] { "block_inactive_or_changed_fair_event", "block_grange_mutex", "block_inventory_display_or_score_drift", "block_unverified_route", "block_direct_team_display_inventory_score_or_judging_mutation" }));

            Register(Option("executor.play_fair_fishing_game", "festival", "Execute one verified native Fair FishingGame with shared predictive legal-input control",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.fair_fishing_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one live Fair fishing booth interaction tile", "native Event dialogue deducts exactly 50g and starts FishingGame", "the shared BobberBar controller completes the real 100-second session and verifies exact score perfection token and return receipts" },
                new[] { "block_inactive_or_changed_fair_event", "block_dialogue_or_entry_fee_drift", "block_unverified_native_session_or_result_formula", "block_unverified_route", "block_direct_money_score_fish_timer_reward_or_inventory_mutation" }));

            Register(Option("executor.play_fair_slingshot_game", "festival", "Execute one verified native Fair TargetGame with predictive moving-target intercept input",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.fair_slingshot_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one live Fair slingshot booth interaction tile", "native Event dialogue deducts exactly 50g and starts TargetGame", "native charged shots use the existing aim patch and verify score accuracy token and return receipts" },
                new[] { "block_inactive_or_changed_fair_event", "block_dialogue_or_entry_fee_drift", "block_unverified_native_session_or_result_formula", "block_unverified_route", "block_direct_money_target_score_accuracy_reward_timer_or_inventory_mutation" }));

            Register(Option("executor.play_fair_strength_game", "festival", "Execute one verified native Fair StrengthGame with predictive maximum-power input",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.fair_strength_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the exact x=29 stand adjacent to live Buildings tile 540", "native Event.checkAction directly opens StrengthGame without a fee or entry dialogue", "one predictive native click drives the real swing result and verifies exactly one star token plus native cleanup" },
                new[] { "block_inactive_or_changed_fair_event", "block_entry_endpoint_or_power_timing_drift", "block_unverified_native_result_or_cleanup", "block_unverified_route", "block_direct_power_timer_score_reward_animation_or_inventory_mutation" }));

            Register(Option("executor.spin_fair_wheel", "festival", "Execute one verified native Fair green wheel wager and record its stochastic settlement",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.fair_wheel_spin", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one live Buildings 308 or 309 wheel endpoint", "native wheelBet Green dialogue and NumberSelectionMenu submit the exact bounded wager", "the real WheelSpinGame owns randomness score settlement result text and menu exit" },
                new[] { "block_inactive_or_changed_fair_event", "block_dialogue_number_selection_or_wager_drift", "block_unverified_native_random_settlement_or_cleanup", "block_unverified_route", "block_direct_rng_rotation_wager_score_result_or_menu_mutation" }));

            Register(Option("executor.play_calico_jack", "minigame", "Execute one verified native CalicoJack round with exact deterministic seed replay",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.club_coins", "player.has_club_card", "player.calico_jack", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the exact live ClubCards or BlackJack table", "native Play dialogue constructs CalicoJack with the projected bet and seed", "shared exact seed replay validates every dealt card and chooses native hit or stand", "native result settlement and quit verify the exact Qi-coin delta without direct mutation" },
                new[] { "block_club_or_table_projection_drift", "block_dialogue_bet_or_seed_replay_drift", "block_unverified_native_settlement_or_cleanup", "block_unverified_route", "block_direct_card_rng_coin_result_or_minigame_mutation" }));

            Register(Option("executor.play_slots", "minigame", "Execute one verified native Slots spin and record its stochastic settlement",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.club_coins", "player.has_club_card", "player.slots", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one exact live ClubSlots endpoint", "native Slots 10- or 100-coin button starts exactly one spin", "the real shared RNG owns reels and payout", "result icons, payout multiplier, coin delta, times-played increment and Done cleanup are verified without direct mutation" },
                new[] { "block_club_or_machine_projection_drift", "block_bet_probability_or_input_contract_drift", "block_unverified_native_random_settlement_or_cleanup", "block_unverified_route", "block_direct_rng_reel_coin_result_or_stat_mutation" }));

            Register(Option("executor.play_crane_game", "minigame", "Execute one verified native Movie Theater Crane Game session",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.money", "player.inventory", "player.crane_game", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches one exact live CraneGame action tile", "native Yes deducts exactly 500g and constructs the real CraneGame", "right and down input drive all three attempts against live prizes and physics", "native ItemGrabMenu rewards, money delta and cleanup are conserved and verified" },
                new[] { "block_movie_theater_machine_or_projection_drift", "block_fee_dialogue_or_input_contract_drift", "block_unverified_reward_transfer_or_cleanup", "block_unverified_route", "block_direct_rng_money_prize_position_state_or_inventory_mutation" }));

            Register(Option("executor.play_darts", "minigame", "Execute one verified native Pirate Cove darts victory",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.darts_game", "world_progress.golden_walnuts", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the exact live DartsGame action tile", "native Yes constructs Darts with the projected 20, 15 or 10 dart allowance", "mouse position and left-button charge timing drive the real 301-point board", "native result dialogue and limited-nut request advance exactly one Darts reward" },
                new[] { "block_pirate_night_endpoint_or_projection_drift", "block_dialogue_dart_allowance_or_input_contract_drift", "block_unverified_native_score_reward_or_cleanup", "block_unverified_route", "block_direct_score_dart_count_timer_rng_reward_or_progress_mutation" }));

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

            Register(Option("executor.renovate_home", "housing", "Apply one verified farmhouse renovation through native Carpenter and RenovateMenu input",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "world_progress.marriage_house", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches Robin's counter", "native Renovate response opens the exact live HouseRenovations row", "native RenovateMenu hover and world-region click applies only the rebound branch", "money, FirstPurchase marker, action state, renovation event and return are verified" },
                new[] { "block_non_player_command_or_confirmation_drift", "block_data_catalog_shop_order_or_region_drift", "block_requirement_money_crib_or_obstruction_drift", "block_native_menu_or_return_lifecycle_drift", "block_direct_money_mail_NetInt_map_furniture_menu_viewport_or_event_mutation" }));

            Register(Option("executor.construct_building", "buildings", "Construct one exact verified building through native Carpenter dialogue and placement",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches the verified builder service", "native builder menu selects the exact blueprint", "native placement starts the exact construction countdown" },
                new[] { "block_missing_authorized_quest_or_general_strategy_purpose", "block_blueprint_resource_or_placement_drift", "block_active_construction", "block_direct_money_inventory_building_or_quest_mutation" }));

            Register(Option("executor.change_building_skin", "buildings", "Apply one exact verified building skin or paint region through native Carpenter appearance menus",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.building_skin_catalog", "player.building_paint_catalog", "locations.collision_grid", "menus.active_menu" },
                new[] { "BFS reaches Robin's counter through one shared lifecycle", "frozen parameters select exact BuildingSkinMenu or BuildingPaintMenu native controls", "exact skin or target paint region plus sibling-region invariants are verified" },
                new[] { "block_building_identity_appearance_menu_or_permission_drift", "block_nonshortest_skin_or_mouse_unreachable_paint_target", "block_unacknowledged_paint_reset_or_sibling_mutation", "block_direct_skin_or_paint_mutation" }));

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

            Register(Option("executor.place_cookout_kit", "crafting", "Place one verified Cookout Kit at one exact native-legal tile for same-day cooking",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.cookout_kit_placement", "player.cooking", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates exact Torch (BC)278 with fragility 1 and destroyOvernight true", "one Cookout Kit is consumed", "the placed Torch becomes a native same-day cooking endpoint" },
                new[] { "block_missing_cookout_reason", "block_inventory_or_projection_identity_drift", "block_unreachable_adjacent_stand", "block_native_placement_recheck", "block_cross_day_use_plan" }));

            Register(Option("executor.place_tent", "recovery", "Place one verified Tent Kit through the native directional 3x2 outdoor placement branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.tent_placement", "current_location.large_terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates one exact TerrainFeatures.Tent at the direction-derived center anchor", "one exact base Tent Kit is consumed", "the 3x2 footprint, initial health, passability and separate sleep handoff match the native branch" },
                new[] { "block_missing_tent_reason_or_native_contract", "block_inventory_projection_or_calendar_drift", "block_indoor_festival_or_illegal_directional_rectangle", "block_unreachable_or_protected_footprint", "block_native_placement_recheck" }));

            Register(Option("executor.place_crab_pot", "fishing", "Place one verified inventory Crab Pot at one exact native-legal water tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.crab_pot_placement", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates one exact owned CrabPot at the rebound water tile", "one inventory Crab Pot is consumed", "initial bait output and harvest state match the current owner professions and native constructor" },
                new[] { "block_missing_crab_pot_reason_or_production_signature", "block_inventory_owner_or_projection_identity_drift", "block_nonwater_or_native_excluded_location", "block_unreachable_adjacent_shore_stand", "block_native_placement_recheck" }));

            Register(Option("executor.place_fence", "farming", "Place one verified inventory fence or functional gate at one exact route-safe native-legal tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.fence_placement", "current_location.objects", "current_location.chests", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates one exact base Fence with live Data/Fences health bounds", "one matching inventory item is consumed", "neighbor draw topology and closed placement state match", "virtual occupancy preserves the reachable domain and protected access" },
                new[] { "block_missing_layout_reason_or_native_contract", "block_inventory_data_or_projection_identity_drift", "block_nonfunctional_gate_topology", "block_route_or_protected_access_disconnect", "block_native_placement_recheck" }));

            Register(Option("executor.place_flooring", "farming", "Place one verified inventory floor or path item at one exact reachable native-legal tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.flooring_placement", "current_location.terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates one exact base TerrainFeatures.Flooring from live Data/FloorsAndPaths", "one matching inventory item is consumed", "same-floor eight-neighbor topology and native random view domain match", "the passable target preserves the current reachable domain" },
                new[] { "block_missing_layout_reason_or_native_contract", "block_inventory_data_or_projection_identity_drift", "block_existing_terrain_feature_or_neighbor_topology_drift", "block_unreachable_target_or_adjacent_stand", "block_native_placement_recheck" }));

            Register(Option("executor.plant_grass", "farming", "Plant one verified normal or blue grass starter at one exact upstream-selected native-legal tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.grass_placement", "current_location.objects", "current_location.terrain_features", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates exact TerrainFeatures.Grass type 1 or 7 with four initial weeds", "one matching grass starter is consumed", "the passable target preserves the current reachable domain" },
                new[] { "block_missing_layout_reason_or_native_contract", "block_inventory_variant_or_projection_identity_drift", "block_existing_object_or_terrain_feature", "block_unreachable_target_or_adjacent_stand", "block_native_placement_recheck" }));

            Register(Option("executor.place_furniture", "housing", "Place one exact inventory furniture item with a verified native rotation, footprint and endpoint",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.furniture_placement", "current_location.furniture", "current_location.chests", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement preserves the exact vanilla furniture runtime identity and virtual rotation result", "the result appears in location furniture or the exact empty table held-object endpoint", "one matching inventory item is consumed", "nonpassable footprints preserve routes and existing access" },
                new[] { "block_custom_or_missing_furniture_factory", "block_inventory_rotation_or_projection_drift", "block_invalid_wall_table_or_location_endpoint", "block_route_or_protected_access_disconnect", "block_native_placement_recheck" }));

            Register(Option("executor.place_sign", "farming", "Place one exact empty display-item sign or text sign through its native branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.sign_placement", "current_location.objects", "current_location.chests", "locations.collision_grid", "menus.active_menu" },
                new[] { "native placement creates the exact display-sign or text-sign runtime branch", "the placed sign starts with no display item or text", "one matching inventory item is consumed", "the nonpassable target preserves routes and existing access" },
                new[] { "block_missing_sign_layout_reason", "block_inventory_catalog_or_branch_drift", "block_nonempty_payload_request", "block_route_or_protected_access_disconnect", "block_native_placement_recheck" }));

            Register(Option("executor.set_sign_display_item", "farming", "Set or replace one placed display-item sign payload through its native interaction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "native Sign interaction copies the selected item into the exact base display sign", "display type matches the native item family", "the source inventory reference, stack and serialized state remain unchanged", "replacement of an existing display payload is explicitly authorized" },
                new[] { "block_text_or_custom_sign_target", "block_source_or_target_projection_drift", "block_unapproved_existing_payload_replacement", "block_unreachable_adjacent_stand", "block_native_interaction_or_post_state_mismatch" }));

            Register(Option("executor.edit_text_sign", "farming", "Edit one exact placed text sign through its native text input menu",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "native text-sign interaction opens the exact TitleTextInputMenu contract", "keyboard input obeys the 60 UTF-16-code-unit limit and native filtering", "the native callback trims and updates raw and displayed sign text", "showNextIndex exactly matches whether displayed text is empty" },
                new[] { "block_display_or_custom_sign_target", "block_target_or_previous_text_projection_drift", "block_unapproved_existing_text_replacement", "block_unreachable_adjacent_stand_or_open_menu", "block_native_menu_or_post_state_mismatch" }));

            Register(Option("executor.load_crab_pot_bait", "fishing", "Load one exact native-accepted bait item into one verified empty Crab Pot",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "current_location.objects", "locations.collision_grid", "menus.active_menu" },
                new[] { "native GameLocation checkAction assigns one exact bait unit and current player ownership", "native active-item reduction consumes exactly one matching bait", "ready and held-output state remain empty" },
                new[] { "block_missing_bait_reason_or_native_contract", "block_crab_pot_lifecycle_owner_or_runtime_type_drift", "block_inventory_bait_identity_or_unit_state_drift", "block_unreachable_adjacent_stand", "block_native_drop_in_probe_recheck" }));

            Register(Option("executor.read_book", "skills", "Read one verified inventory book through native performUseAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.book_candidates", "player.skills_detail", "menus.active_menu" },
                new[] { "native book animation starts and one item is consumed", "skill XP, permanent level, new-level queue, mastery, stat, mail, recipe, and feedback deltas are verified", "native animation settling wait is scheduled" },
                new[] { "block_native_book_use_gate", "block_inventory_identity_drift", "block_projection_drift", "block_direct_skill_stat_mail_or_recipe_mutation" }));

            Register(Option("executor.read_secret_note", "inventory", "Read one exactly projected Secret Note or Journal Scrap through native performUseAction",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.secret_note_candidates", "menus.active_menu" },
                new[] { "the native unseen-note selector chooses the rebound note id", "native performUseAction records exactly one newly seen note and opens its LetterViewerMenu", "note 10 or 23 native quest side effects are verified", "the native caller consumes exactly one matching inventory item" },
                new[] { "block_native_secret_note_use_gate", "block_inventory_or_unseen_set_drift", "block_selection_or_side_effect_projection_drift", "block_direct_seen_note_quest_menu_or_inventory_mutation" }));

            Register(Option("executor.use_firework", "inventory", "Launch one exact inventory firework at one explicit native-legal target tile",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.PlayerCommandOnly,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.firework_placement", "menus.active_menu" },
                new[] { "explicit player command selects one exact tile and one of the three vanilla firework variants", "native placement creates the exact fuse and rocket sprite graph", "one matching inventory item is consumed", "random rocket acceleration and explosion id remain bounded runtime outcomes rather than guessed reads" },
                new[] { "block_not_explicitly_authorized", "block_inventory_variant_or_projection_identity_drift", "block_non_native_legal_or_transiently_occupied_target", "block_nonadjacent_stand_or_open_menu", "never_advance_or_guess_shared_rng_during_read", "block_direct_sprite_audio_or_inventory_mutation" }));

            Register(Option("executor.use_horse_flute", "movement", "Use one reusable Horse Flute through the exact native delayed team-warp branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.horse_flute", "menus.active_menu" },
                new[] { "the native start and delayed horse-warp restrictions both pass", "an owned horse already adjacent is a successful no-op", "otherwise the owned horse is warped through the team event after 1500 ms", "the reusable flute inventory stack is unchanged" },
                new[] { "block_native_object_use_gate_or_horse_warp_restriction", "block_inventory_or_owned_horse_identity_drift", "block_expected_adjacent_or_delayed_result_drift", "block_open_menu", "block_direct_horse_position_team_event_or_inventory_mutation" }));

            Register(Option("executor.use_monster_musk", "combat", "Consume one exact Monster Musk through its native delayed Buff 24 branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.monster_musk", "menus.active_menu" },
                new[] { "native use consumes exactly one Monster Musk", "the 750 ms native callback removes and replaces Buff 24 with its 600000 ms data contract", "ordinary-mine and volcano monster spawn multipliers are transparently bound to Buff 24" },
                new[] { "block_native_object_use_gate", "block_inventory_buff_data_or_active_buff_projection_drift", "block_animation_or_spawn_semantics_drift", "block_open_menu", "block_direct_buff_sprite_audio_or_inventory_mutation" }));

            Register(Option("executor.use_rain_totem", "farming", "Consume one exact Rain Totem through its native location-context weather branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "player.rain_totem", "menus.active_menu" },
                new[] { "native use consumes exactly one Rain Totem", "the affected vanilla location context tomorrow-weather becomes Rain", "default-context festival tomorrow and redundant Rain states are excluded before consumption" },
                new[] { "block_native_object_use_or_effect_gate", "block_inventory_location_context_weather_or_projection_drift", "block_animation_contract_drift", "block_open_menu", "block_direct_weather_sprite_audio_or_inventory_mutation" }));

            Register(Option("executor.use_return_scepter", "movement", "Use one reusable Return Scepter through its native exact-home delayed warp",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.return_scepter", "menus.active_menu" },
                new[] { "the exact base Wand runs through Farmer.BeginUsingTool and Tool.InstantUse", "the native delayed callback resolves the current player's own FarmHouse or Cabin front door", "the reusable Return Scepter remains in the same inventory slot with unchanged stack" },
                new[] { "block_native_wand_or_executor_use_gate", "block_missing_home_or_redundant_destination", "block_inventory_home_destination_or_projection_drift", "block_animation_contract_drift", "block_open_menu", "block_direct_warp_position_invincibility_movement_or_inventory_mutation" }));

            Register(Option("executor.use_treasure_totem", "foraging", "Consume one exact Treasure Totem through its native artifact-spot ring generation branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.treasure_totem", "menus.active_menu" },
                new[] { "native use consumes exactly one Treasure Totem", "TreasureTotemsUsed increments exactly once", "artifact spots appear on the exact eligible subset of the native 16-tile rounded-distance ring" },
                new[] { "block_native_object_use_or_outdoors_gate", "block_inventory_center_tile_ring_or_counter_projection_drift", "block_zero_spawn_consumption", "block_open_menu", "block_direct_world_object_counter_audio_visual_or_inventory_mutation" }));

            Register(Option("executor.use_warp_totem", "movement", "Consume one exact Warp Totem through its native delayed and festival-routed destination branch",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.tile_x", "player.tile_y", "player.inventory", "player.warp_totem", "menus.active_menu" },
                new[] { "native use consumes exactly one selected Warp Totem", "Farm uses the live WarpTotemEntry or exact farm-type fallback while the other four variants retain their native destinations", "active and passive festival routing is resolved before consumption and the final native location/tile is verified" },
                new[] { "block_native_object_use_gate", "block_inventory_destination_festival_or_projection_drift", "block_pre_festival_consumption_without_warp", "block_multiplayer_festival_ready_check", "block_redundant_exact_destination", "block_open_menu", "block_direct_warp_position_state_audio_visual_or_inventory_mutation" }));

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
