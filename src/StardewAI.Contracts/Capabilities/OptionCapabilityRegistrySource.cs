using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Options;

namespace StardewAI.Contracts.Capabilities
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CapabilityRegistrationStatus
    {
        Unknown,
        Registered
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CapabilityReadStatus
    {
        Unknown,
        RequiredFactContractDeclared
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CapabilityCandidateStatus
    {
        Unknown,
        NotApplicable,
        Declared,
        PartiallyBlocked,
        Blocked
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CapabilityCompilerStatus
    {
        Unknown,
        Unbound,
        ParameterCompilerDeclared,
        StepCompilerDeclared,
        StepAndParameterCompilerDeclared
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CapabilityVerifierStatus
    {
        Unknown,
        ContractDeclared,
        PendingRuntimeEvidence,
        RuntimeVerified
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CapabilityProductIntegrationStatus
    {
        Unknown,
        NotIntegrated,
        ProductIntegrated
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TrainingEvidenceGateStatus
    {
        Missing,
        DeclaredOnly,
        RuntimeVerified
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TrainingAdmissionExclusionReason
    {
        NotPolicyTrainingOption,
        ReadEvidenceMissing,
        CandidateEvidenceMissing,
        CompilerEvidenceMissing,
        RuntimeEvidenceMissing,
        OutputEvidenceMissing,
        ExplicitPlayerConfirmationRequired,
        PlayerCommandOnly
    }

    public sealed class OptionCapabilityDeclaration
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; internal set; } = OptionCapabilityRegistrySource.SchemaVersion;

        [JsonPropertyName("option_id")]
        public string OptionId { get; internal set; } = string.Empty;

        [JsonPropertyName("registration_status")]
        public CapabilityRegistrationStatus RegistrationStatus { get; internal set; }

        [JsonPropertyName("read_status")]
        public CapabilityReadStatus ReadStatus { get; internal set; }

        [JsonPropertyName("candidate_status")]
        public CapabilityCandidateStatus CandidateStatus { get; internal set; }

        [JsonPropertyName("compiler_status")]
        public CapabilityCompilerStatus CompilerStatus { get; internal set; }

        [JsonPropertyName("harness_dispatch_supported")]
        public bool HarnessDispatchSupported { get; internal set; }

        [JsonPropertyName("product_executor_supported")]
        public bool ProductExecutorSupported { get; internal set; }

        [JsonPropertyName("internal_execution_pipeline_supported")]
        public bool InternalExecutionPipelineSupported { get; internal set; }

        [JsonPropertyName("before_verifier_status")]
        public CapabilityVerifierStatus BeforeVerifierStatus { get; internal set; }

        [JsonPropertyName("after_verifier_status")]
        public CapabilityVerifierStatus AfterVerifierStatus { get; internal set; }

        [JsonPropertyName("runtime_evidence_status")]
        public OptionRuntimeStatus RuntimeEvidenceStatus { get; internal set; }

        [JsonPropertyName("training_eligibility")]
        public OptionTrainingEligibility TrainingEligibility { get; internal set; }

        [JsonPropertyName("autonomous_candidate_enabled")]
        public bool AutonomousCandidateEnabled { get; internal set; }

        [JsonPropertyName("player_confirmation_required")]
        public bool PlayerConfirmationRequired { get; internal set; }

        [JsonPropertyName("host_only")]
        public bool HostOnly { get; internal set; }

        [JsonPropertyName("product_integration_status")]
        public CapabilityProductIntegrationStatus ProductIntegrationStatus { get; internal set; }

        [JsonPropertyName("policy_training_candidate")]
        public bool PolicyTrainingCandidate { get; internal set; }

        [JsonPropertyName("invocation_policy")]
        public OptionInvocationPolicy InvocationPolicy { get; internal set; } = OptionInvocationPolicy.Unknown;

        [JsonPropertyName("read_training_gate")]
        public TrainingEvidenceGateStatus ReadTrainingGate { get; internal set; }

        [JsonPropertyName("candidate_training_gate")]
        public TrainingEvidenceGateStatus CandidateTrainingGate { get; internal set; }

        [JsonPropertyName("compiler_training_gate")]
        public TrainingEvidenceGateStatus CompilerTrainingGate { get; internal set; }

        [JsonPropertyName("runtime_training_gate")]
        public TrainingEvidenceGateStatus RuntimeTrainingGate { get; internal set; }

        [JsonPropertyName("output_training_gate")]
        public TrainingEvidenceGateStatus OutputTrainingGate { get; internal set; }

        [JsonPropertyName("read_evidence_ids")]
        public string[] ReadEvidenceIds { get; internal set; } = Array.Empty<string>();

        [JsonPropertyName("candidate_evidence_ids")]
        public string[] CandidateEvidenceIds { get; internal set; } = Array.Empty<string>();

        [JsonPropertyName("compiler_evidence_ids")]
        public string[] CompilerEvidenceIds { get; internal set; } = Array.Empty<string>();

        [JsonPropertyName("runtime_evidence_ids")]
        public string[] RuntimeEvidenceIds { get; internal set; } = Array.Empty<string>();

        [JsonPropertyName("output_evidence_ids")]
        public string[] OutputEvidenceIds { get; internal set; } = Array.Empty<string>();

        [JsonPropertyName("training_exclusion_reasons")]
        public TrainingAdmissionExclusionReason[] TrainingExclusionReasons { get; internal set; } =
            Array.Empty<TrainingAdmissionExclusionReason>();

        [JsonPropertyName("training_evidence_scope")]
        public string TrainingEvidenceScope { get; internal set; } = "not_admitted";
    }

    public sealed class DailyCandidateCapabilityDeclaration
    {
        [JsonPropertyName("kind")]
        public string Kind { get; internal set; } = string.Empty;

        [JsonPropertyName("compilable")]
        public bool Compilable { get; internal set; }

        [JsonPropertyName("block_reason")]
        public string BlockReason { get; internal set; } = string.Empty;
    }

    public static class OptionCapabilityRegistrySource
    {
        public const string SchemaVersion = "capability_registry.v3";

        private sealed class TrainingEvidence
        {
            public string Scope { get; set; } = string.Empty;
            public string[] ReadEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] CandidateEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] CompilerEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] RuntimeEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] OutputEvidenceIds { get; set; } = Array.Empty<string>();
        }

        private sealed class CapabilitySeed
        {
            public CapabilitySeed(
                bool stepCompiler,
                bool parameterCompiler,
                bool harnessDispatch,
                bool internalExecution,
                bool autonomousCandidate,
                bool playerConfirmation,
                bool playerCommandOnly,
                bool calibrationOnly,
                TrainingEvidence evidence)
            {
                StepCompiler = stepCompiler;
                ParameterCompiler = parameterCompiler;
                HarnessDispatch = harnessDispatch;
                InternalExecution = internalExecution;
                AutonomousCandidate = autonomousCandidate;
                PlayerConfirmation = playerConfirmation;
                PlayerCommandOnly = playerCommandOnly;
                CalibrationOnly = calibrationOnly;
                Evidence = evidence;
            }

            public bool StepCompiler { get; }
            public bool ParameterCompiler { get; }
            public bool HarnessDispatch { get; }
            public bool InternalExecution { get; }
            public bool AutonomousCandidate { get; }
            public bool PlayerConfirmation { get; }
            public bool PlayerCommandOnly { get; }
            public bool CalibrationOnly { get; }
            public TrainingEvidence Evidence { get; }
        }

        private static readonly IReadOnlyDictionary<string, CapabilitySeed> NativeObjectCapabilitySeeds =
            new ReadOnlyDictionary<string, CapabilitySeed>(
                new Dictionary<string, CapabilitySeed>(StringComparer.Ordinal)
                {
                    ["world.rotate_house_plant"] = NativeObjectSeed(
                        autonomous: false, playerCommandOnly: true,
                        "vanilla_all_eight_base_house_plant_visual_frames_empty_hand_native_location_object_interaction_double_call_edge_permanent_identity_and_selected_slot_receipt",
                        "EVD-271"),
                    ["world.play_singing_stone"] = NativeObjectSeed(
                        autonomous: false, playerCommandOnly: true,
                        "vanilla_exact_base_(BC)94_shared_rng_uniform_crystal_pitch_distribution_native_location_object_interaction_shake_timer_identity_and_selected_slot_receipt",
                        "EVD-274"),
                    ["world.tune_flute_block"] = NativeObjectSeed(
                        autonomous: false, playerCommandOnly: true,
                        "vanilla_exact_base_(O)464_persistent_25_pitch_cycle_safe_flute_cue_native_location_object_interaction_shake_scale_identity_and_selected_slot_receipt",
                        "EVD-281"),
                    ["world.tune_drum_block"] = NativeObjectSeed(
                        autonomous: false, playerCommandOnly: true,
                        "vanilla_exact_base_(O)463_persistent_7_tone_cycle_drumkit0_to_6_native_location_object_interaction_shake_scale_identity_and_selected_slot_receipt",
                        "EVD-282"),
                    ["farming.read_farm_computer_report"] = NativeObjectSeed(
                        autonomous: false, playerCommandOnly: true,
                        "vanilla_exact_base_(BC)239_root_location_native_aggregate_localized_report_native_delayed_dialogue_identity_and_selected_slot_receipt",
                        "EVD-280"),
                    ["farming.collect_slime_ball"] = NativeObjectSeed(
                        autonomous: true, playerCommandOnly: false,
                        "vanilla_exact_SlimeHutch_base_fragility_2_slime_ball_seeded_slime_and_petrified_slime_projection_native_location_action_object_removal_conserved_inventory_plus_debris_output_and_shared_pickup_handoff",
                        "EVD-272"),
                    ["animals.withdraw_feed_hopper_hay"] = NativeObjectSeed(
                        autonomous: true, playerCommandOnly: false,
                        "vanilla_exact_base_(BC)99_AnimalHouse_root_silo_animal_and_placed_hay_projection_native_location_action_exact_(O)178_inventory_transfer_conservation_identity_and_selected_slot_receipt",
                        "EVD-276"),
                    ["animals.collect_auto_grabber_contents"] = NativeObjectSeed(
                        autonomous: true, playerCommandOnly: false,
                        "vanilla_exact_base_(BC)165_native_held_Chest_inventory_capacity_projection_shared_object_movement_native_ItemGrabMenu_stack_transfer_conservation_remaining_contents_identity_and_selected_slot_receipt",
                        "EVD-278"),
                    ["movement.use_mini_obelisk"] = NativeObjectCalibrationSeed(
                        "vanilla_exact_base_(BC)238_native_first_two_pair_raw_object_order_farther_destination_down_left_right_up_landing_shared_object_movement_native_delayed_same_location_teleport_pair_identity_and_selected_slot_receipt",
                        "EVD-279")
                });

        private static readonly HashSet<string> StepCompilerIds = Set(
            "minigame.play_calico_jack", "executor.play_calico_jack", "minigame.play_crane_game", "executor.play_crane_game",
            "buildings.change_skin", "executor.change_building_skin", "buildings.paint",
            "quest.accept_daily", "quest.accept_special_order", "quest.claim_reward", "mail.process_letter", "mining.use_elevator",
            "farm.maintain_crops", "farm.process_machines", "farm.collect_animal_products", "animals.purchase", "animals.manage_animal", "crafting.cook_recipe", "crafting.forge_item", "buildings.construct", "farm.care_for_pets", "museum.donate_items", "island.field_office_donate", "island.field_office_survey", "festival.manage_grange_display", "festival.play_fishing_game", "festival.play_slingshot_game", "festival.play_strength_game", "festival.spin_wheel", "community_center.donate_bundle_items", "joja.advance_development", "quest.advance", "farm.collect_machine_outputs", "farm.load_supported_machine_input", "farm.establish_supported_machine_capacity", "farm.fulfill_machine_task_demand", "fishing.catch_fish", "fishing.collect_crab_pots", "fishing.service_fish_ponds", "fishing.manage_fish_pond", "housing.advance_farmhouse", "housing.renovate", "foraging.clear_green_rain_bushes", "foraging.collect_spawned_objects", "foraging.harvest_bushes", "foraging.harvest_fruit_tree", "foraging.harvest_tree_product", "foraging.rummage_garbage", "foraging.harvest_ginger", "foraging.pan_ore_spot", "mining.claim_reward_chests", "rewards.claim_pot_of_gold", "mining.choose_dwarf_statue_power", "rewards.claim_statue_blessing", "skills.read_books", "skills.choose_profession", "economy.buy_supplies", "economy.sell_items", "recovery.stabilize_day", "recovery.sleep_in_tent", "recovery.escape_object_trap",
            "executor.move_to_tile", "executor.traverse_connector", "executor.face_direction",
            "executor.interact", "executor.accept_daily_quest", "executor.accept_special_order", "executor.claim_quest_reward", "executor.buy_shop_item", "executor.sell_shop_item",
            "executor.choose_dialogue_response", "executor.choose_animal_purchase_response", "executor.purchase_animal", "executor.manage_animal", "executor.cook_recipe", "executor.forge_item", "executor.sleep", "executor.wait_ticks",
            "executor.clear_obstacle", "executor.break_farm_resource_clump",
            "executor.break_current_location_resource_clump", "executor.water_crop", "executor.apply_fertilizer", "executor.apply_tree_treatment", "executor.till_soil",
            "executor.plant_seed", "executor.plant_grass", "executor.harvest_crop", "executor.harvest_giant_crop",
            "executor.pickup_debris", "executor.collect_spawned_object", "executor.harvest_ginger",
            "executor.harvest_bush", "executor.harvest_fruit_tree", "executor.harvest_tree_product", "executor.rummage_garbage", "executor.claim_mine_reward_chest", "executor.collect_crab_pot",
            "executor.collect_fish_pond_output", "executor.complete_fish_pond_request",
            "executor.collect_animal_product", "executor.pet_interact", "executor.fill_pet_bowl",
            "executor.donate_museum_item", "executor.donate_field_office_piece", "executor.answer_field_office_survey", "executor.manage_grange_display", "executor.donate_community_center_item",
            "executor.purchase_joja_membership", "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade", "executor.renovate_home", "executor.construct_building", "executor.pan_ore_spot",
            "executor.collect_machine_output", "executor.load_machine_input",
            "executor.name_hatched_animal",
            "economy.ship_items", "executor.craft_machine_item", "executor.craft_storage_item", "executor.craft_quest_item", "executor.place_machine", "executor.remove_machine", "executor.place_storage", "executor.place_cookout_kit", "executor.place_tent", "executor.place_crab_pot", "executor.place_fence", "executor.place_flooring", "executor.place_furniture", "executor.place_sign", "executor.set_sign_display_item", "executor.edit_text_sign", "executor.load_crab_pot_bait",
            "executor.read_book", "executor.read_secret_note", "executor.use_firework", "executor.use_horse_flute", "executor.use_monster_musk", "executor.use_rain_totem", "executor.use_return_scepter", "executor.use_treasure_totem", "executor.use_warp_totem", "executor.catch_fish", "executor.play_junimo_kart", "executor.play_fair_fishing_game", "executor.play_fair_slingshot_game", "executor.play_fair_strength_game", "executor.spin_fair_wheel",
            "executor.cool_volcano_lava", "executor.break_volcano_stone",
            "executor.break_volcano_container", "executor.combat_volcano_monster",
            "executor.mine_stone", "executor.break_container", "executor.break_resource_clump",
            "executor.combat_monster", "executor.shoot_monster", "executor.place_bomb",
            "executor.place_staircase",
            "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft",
            "executor.exit_mine", "executor.social_interact", "executor.quest_npc_interact",
            "executor.quest_drop_box_donate", "executor.select_safe_item_slot",
            "executor.close_menu", "executor.ship_inventory_item_to_bin",
            "executor.transfer_material");

        private static readonly HashSet<string> ParameterCompilerIds = Set(
            "exploration.visit_location", "executor.traverse_connector",
            "executor.select_safe_item_slot", "executor.close_menu", "mining.reach_depth",
            "mining.acquire_golden_scythe", "mining.obtain_skull_key",
            "volcano.reach_caldera", "recovery.stabilize_day", "recovery.escape_object_trap", "rewards.claim_pot_of_gold", "mining.choose_dwarf_statue_power", "rewards.claim_statue_blessing", "fishing.manage_fish_pond", "housing.renovate", "executor.renovate_home", "executor.buy_shop_item",
            "social.talk_npc", "social.gift_npc", "social.advance_partnership",
            "inventory.transfer_item", "executor.transfer_material");

        private static readonly HashSet<string> HarnessDispatchIds = Set(
            "executor.play_calico_jack", "executor.play_crane_game",
            "executor.change_building_skin",
            "executor.accept_daily_quest", "executor.accept_special_order", "executor.claim_quest_reward",
            "executor.move_to_tile", "executor.traverse_connector",
            "executor.face_direction", "executor.interact", "executor.buy_shop_item",
            "executor.sell_shop_item", "executor.choose_dialogue_response", "executor.choose_animal_purchase_response", "executor.purchase_animal", "executor.manage_animal", "executor.cook_recipe", "executor.forge_item", "executor.sleep", "recovery.sleep_in_tent",
            "executor.wait_ticks", "executor.clear_obstacle", "executor.break_farm_resource_clump",
            "executor.break_current_location_resource_clump", "executor.water_crop", "executor.apply_fertilizer", "executor.apply_tree_treatment", "executor.till_soil",
            "executor.plant_seed", "executor.plant_grass", "executor.harvest_crop", "executor.harvest_giant_crop",
            "executor.pickup_debris", "executor.collect_spawned_object", "executor.harvest_ginger",
            "executor.harvest_bush", "executor.harvest_fruit_tree", "executor.harvest_tree_product", "executor.rummage_garbage", "executor.claim_mine_reward_chest", "rewards.claim_pot_of_gold", "mining.choose_dwarf_statue_power", "rewards.claim_statue_blessing", "executor.collect_crab_pot",
            "executor.collect_fish_pond_output", "executor.complete_fish_pond_request",
            "fishing.manage_fish_pond",
            "executor.collect_animal_product", "executor.pet_interact", "executor.fill_pet_bowl",
            "executor.donate_museum_item", "executor.donate_field_office_piece", "executor.answer_field_office_survey", "executor.manage_grange_display", "executor.donate_community_center_item",
            "executor.purchase_joja_membership", "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade", "executor.renovate_home", "executor.construct_building", "executor.pan_ore_spot",
            "executor.collect_machine_output", "executor.load_machine_input",
            "executor.name_hatched_animal",
            "executor.craft_machine_item", "executor.craft_storage_item", "executor.craft_quest_item", "executor.place_machine", "executor.remove_machine", "executor.place_storage", "executor.place_cookout_kit", "executor.place_tent", "executor.place_crab_pot", "executor.place_fence", "executor.place_flooring", "executor.place_furniture", "executor.place_sign", "executor.set_sign_display_item", "executor.edit_text_sign", "executor.load_crab_pot_bait",
            "executor.read_book", "executor.read_secret_note", "executor.use_firework", "executor.use_horse_flute", "executor.use_monster_musk", "executor.use_rain_totem", "executor.use_return_scepter", "executor.use_treasure_totem", "executor.use_warp_totem", "executor.catch_fish", "executor.play_junimo_kart", "executor.play_fair_fishing_game", "executor.play_fair_slingshot_game", "executor.play_fair_strength_game", "executor.spin_fair_wheel",
            "executor.cool_volcano_lava", "executor.break_volcano_stone",
            "executor.break_volcano_container", "executor.combat_volcano_monster",
            "executor.mine_stone", "executor.break_container", "executor.break_resource_clump",
            "executor.combat_monster", "executor.shoot_monster", "executor.place_bomb",
            "executor.place_staircase",
            "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft",
            "executor.exit_mine", "executor.social_interact", "executor.quest_npc_interact",
            "executor.quest_drop_box_donate", "executor.select_safe_item_slot",
            "executor.close_menu", "executor.ship_inventory_item_to_bin",
            "executor.transfer_material");

        private static readonly HashSet<string> InternalHighLevelExecutionIds = Set(
            "minigame.play_calico_jack", "minigame.play_crane_game",
            "buildings.change_skin", "buildings.paint",
            "quest.accept_daily", "quest.accept_special_order", "quest.claim_reward", "mail.process_letter",
            "recovery.stabilize_day", "recovery.escape_object_trap", "farm.maintain_crops", "farm.process_machines", "farm.collect_machine_outputs", "farm.load_supported_machine_input", "farm.establish_supported_machine_capacity", "farm.fulfill_machine_task_demand",
            "farm.collect_animal_products", "animals.purchase", "animals.manage_animal", "crafting.cook_recipe", "crafting.forge_item", "buildings.construct", "farm.care_for_pets", "museum.donate_items", "island.field_office_donate", "island.field_office_survey", "festival.manage_grange_display", "festival.play_fishing_game", "festival.play_slingshot_game", "festival.play_strength_game", "festival.spin_wheel", "community_center.donate_bundle_items", "joja.advance_development", "skills.read_books", "skills.choose_profession", "housing.advance_farmhouse", "housing.renovate",
            "fishing.catch_fish", "fishing.collect_crab_pots", "fishing.service_fish_ponds", "fishing.manage_fish_pond", "mining.choose_dwarf_statue_power", "rewards.claim_statue_blessing",
            "foraging.collect_spawned_objects", "foraging.harvest_ginger",
            "foraging.harvest_bushes", "foraging.harvest_fruit_tree", "foraging.harvest_tree_product", "foraging.rummage_garbage", "foraging.clear_green_rain_bushes",
            "foraging.pan_ore_spot", "mining.reach_depth", "mining.use_elevator", "mining.obtain_skull_key",
            "mining.claim_reward_chests", "mining.acquire_golden_scythe",
            "volcano.reach_caldera", "economy.buy_supplies", "economy.sell_items", "economy.ship_items",
            "exploration.visit_location", "inventory.transfer_item");

        private static readonly HashSet<string> AutonomousCandidateIds = Set(
            "farm.maintain_crops", "farm.collect_machine_outputs", "farm.load_supported_machine_input", "farm.establish_supported_machine_capacity", "farm.fulfill_machine_task_demand", "farm.collect_animal_products", "farm.care_for_pets", "island.field_office_survey", "festival.manage_grange_display", "festival.play_strength_game",
            "strategy.grandpa_progress", "exploration.visit_location", "fishing.collect_crab_pots",
            "foraging.collect_spawned_objects", "foraging.harvest_ginger",
            "foraging.harvest_bushes", "foraging.harvest_fruit_tree", "foraging.harvest_tree_product", "foraging.rummage_garbage", "foraging.clear_green_rain_bushes",
            "foraging.pan_ore_spot", "mining.claim_reward_chests", "rewards.claim_pot_of_gold", "mining.choose_dwarf_statue_power", "rewards.claim_statue_blessing", "mining.use_elevator", "quest.claim_reward", "mail.process_letter", "recovery.stabilize_day",
            "executor.move_to_tile", "executor.traverse_connector", "executor.face_direction",
            "executor.interact", "executor.claim_quest_reward", "executor.close_menu", "executor.wait_ticks",
            "executor.claim_mine_reward_chest", "executor.mine_stone", "executor.break_container",
            "executor.break_resource_clump", "executor.combat_monster", "executor.descend_ladder",
            "executor.exit_mine", "executor.cool_volcano_lava", "executor.break_volcano_stone",
            "executor.break_volcano_container", "executor.combat_volcano_monster",
            "executor.clear_obstacle", "executor.break_farm_resource_clump", "executor.water_crop",
            "executor.break_current_location_resource_clump", "executor.till_soil",
            "executor.harvest_crop", "executor.harvest_giant_crop", "executor.pickup_debris",
            "executor.collect_spawned_object", "executor.harvest_ginger", "executor.harvest_bush", "executor.harvest_fruit_tree", "executor.harvest_tree_product", "executor.rummage_garbage",
            "executor.collect_crab_pot", "executor.collect_fish_pond_output",
            "executor.collect_animal_product", "executor.pet_interact", "executor.fill_pet_bowl", "executor.answer_field_office_survey", "executor.manage_grange_display", "executor.play_fair_strength_game",
            "executor.pan_ore_spot", "executor.collect_machine_output",
            "executor.name_hatched_animal", "executor.select_safe_item_slot",
            "executor.transfer_material");

        private static readonly HashSet<string> PlayerConfirmationIds = Set(
            "minigame.play_crane_game", "executor.play_crane_game",
            "museum.donate_items", "island.field_office_donate", "community_center.donate_bundle_items",
            "fishing.manage_fish_pond",
            "joja.advance_development", "housing.advance_farmhouse", "housing.renovate", "mining.acquire_golden_scythe", "social.advance_partnership",
            "buildings.change_skin", "buildings.paint",
            "executor.change_building_skin", "executor.place_furniture",
            "executor.set_sign_display_item", "executor.edit_text_sign", "executor.use_firework",
            "executor.choose_dialogue_response", "executor.donate_museum_item", "executor.donate_field_office_piece",
            "executor.donate_community_center_item", "executor.purchase_joja_membership",
            "executor.purchase_joja_project", "executor.purchase_farmhouse_upgrade", "executor.renovate_home", "executor.construct_building");

        private static readonly HashSet<string> PlayerCommandOnlyIds = Set(
            "minigame.play_crane_game", "executor.play_crane_game",
            "buildings.change_skin", "buildings.paint",
            "fishing.manage_fish_pond", "housing.renovate",
            "executor.change_building_skin", "executor.place_furniture",
            "executor.set_sign_display_item", "executor.edit_text_sign", "executor.use_firework", "executor.renovate_home");

        private static readonly HashSet<string> HostOnlyIds = Set(
            "buildings.construct", "joja.advance_development", "housing.advance_farmhouse",
            "executor.purchase_joja_membership", "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade", "executor.construct_building");

        private static readonly string[] RegisteredOptionIds =
        {
            "minigame.play_calico_jack", "executor.play_calico_jack", "minigame.play_crane_game", "executor.play_crane_game",
            "buildings.change_skin", "executor.change_building_skin", "buildings.paint",
            "quest.accept_daily", "executor.accept_daily_quest", "quest.accept_special_order", "executor.accept_special_order", "quest.claim_reward", "executor.claim_quest_reward", "mail.process_letter",
            "farm.maintain_crops", "farm.process_machines", "farm.collect_machine_outputs", "farm.load_supported_machine_input", "farm.establish_supported_machine_capacity", "farm.fulfill_machine_task_demand", "farm.collect_animal_products", "animals.purchase", "animals.manage_animal", "crafting.cook_recipe", "crafting.forge_item",
            "buildings.construct", "farm.care_for_pets", "museum.donate_items", "island.field_office_donate", "island.field_office_survey", "festival.manage_grange_display", "festival.play_fishing_game", "festival.play_slingshot_game", "festival.play_strength_game", "festival.spin_wheel",
            "community_center.donate_bundle_items", "joja.advance_development",
            "housing.advance_farmhouse", "housing.renovate", "skills.read_books", "skills.choose_profession", "economy.buy_supplies",
            "economy.sell_items", "economy.ship_items", "inventory.transfer_item", "social.talk_npc", "social.gift_npc", "social.advance_partnership",
            "quest.advance", "strategy.grandpa_progress", "exploration.visit_location",
            "fishing.catch_fish", "fishing.collect_crab_pots", "fishing.service_fish_ponds", "fishing.manage_fish_pond",
            "foraging.collect_spawned_objects", "foraging.harvest_ginger",
            "foraging.harvest_bushes", "foraging.harvest_fruit_tree", "foraging.harvest_tree_product", "foraging.rummage_garbage", "foraging.clear_green_rain_bushes",
            "foraging.pan_ore_spot", "mining.reach_depth", "mining.use_elevator", "mining.obtain_skull_key",
            "mining.claim_reward_chests", "rewards.claim_pot_of_gold", "mining.choose_dwarf_statue_power", "rewards.claim_statue_blessing", "mining.acquire_golden_scythe",
            "volcano.reach_caldera", "recovery.stabilize_day", "recovery.sleep_in_tent", "recovery.escape_object_trap",
            "executor.move_to_tile", "executor.traverse_connector", "executor.face_direction",
            "executor.interact", "executor.buy_shop_item", "executor.sell_shop_item",
            "executor.choose_dialogue_response", "executor.choose_animal_purchase_response", "executor.purchase_animal", "executor.manage_animal", "executor.cook_recipe", "executor.forge_item", "executor.sleep", "executor.close_menu",
            "executor.wait_ticks", "executor.claim_mine_reward_chest", "executor.mine_stone",
            "executor.break_container", "executor.break_resource_clump",
            "executor.combat_monster", "executor.shoot_monster", "executor.place_bomb",
            "executor.place_staircase",
            "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft",
            "executor.exit_mine", "executor.cool_volcano_lava", "executor.break_volcano_stone",
            "executor.break_volcano_container", "executor.combat_volcano_monster",
            "executor.catch_fish", "executor.play_junimo_kart", "executor.play_fair_fishing_game", "executor.play_fair_slingshot_game", "executor.play_fair_strength_game", "executor.spin_fair_wheel", "executor.ship_inventory_item_to_bin",
            "executor.transfer_material",
            "executor.social_interact", "executor.quest_npc_interact",
            "executor.quest_drop_box_donate", "executor.clear_obstacle",
            "executor.break_farm_resource_clump", "executor.break_current_location_resource_clump",
            "executor.water_crop", "executor.apply_fertilizer", "executor.apply_tree_treatment", "executor.plant_seed", "executor.plant_grass", "executor.till_soil", "executor.harvest_crop",
            "executor.harvest_giant_crop", "executor.pickup_debris",
            "executor.collect_spawned_object", "executor.harvest_ginger", "executor.harvest_bush", "executor.harvest_fruit_tree", "executor.harvest_tree_product", "executor.rummage_garbage",
            "executor.collect_crab_pot", "executor.collect_fish_pond_output",
            "executor.complete_fish_pond_request", "executor.collect_animal_product",
            "executor.pet_interact", "executor.fill_pet_bowl", "executor.donate_museum_item", "executor.donate_field_office_piece", "executor.answer_field_office_survey", "executor.manage_grange_display",
            "executor.donate_community_center_item", "executor.purchase_joja_membership",
            "executor.purchase_joja_project", "executor.purchase_farmhouse_upgrade", "executor.renovate_home", "executor.construct_building",
            "executor.pan_ore_spot", "executor.collect_machine_output",
            "executor.load_machine_input", "executor.name_hatched_animal",
            "executor.craft_machine_item", "executor.craft_storage_item", "executor.craft_quest_item",
            "executor.place_machine", "executor.remove_machine", "executor.place_storage", "executor.place_cookout_kit", "executor.place_tent", "executor.place_crab_pot", "executor.place_fence", "executor.place_flooring", "executor.place_furniture", "executor.place_sign", "executor.set_sign_display_item", "executor.edit_text_sign", "executor.load_crab_pot_bait",
            "executor.read_book", "executor.read_secret_note", "executor.use_firework", "executor.use_horse_flute", "executor.use_monster_musk", "executor.use_rain_totem", "executor.use_return_scepter", "executor.use_treasure_totem", "executor.use_warp_totem", "executor.select_safe_item_slot"
        };

        private static readonly HashSet<string> CalibrationOnlyHighLevelIds = Set(
            "farm.maintain_crops",
            "farm.process_machines",
            "recovery.stabilize_day",
            "recovery.sleep_in_tent",
            "recovery.escape_object_trap");

        private static readonly IReadOnlyDictionary<string, TrainingEvidence> TrainingEvidenceByOptionId =
            new ReadOnlyDictionary<string, TrainingEvidence>(
                new Dictionary<string, TrainingEvidence>(StringComparer.Ordinal)
                {
                    ["buildings.change_skin"] = VerifiedEvidence(
                        "vanilla_actor_exact_live_Pet_Bowl_default_to_Stone_skin_current_Robin_service_native_CarpenterMenu_shortest_click_and_paint_reset_receipt",
                        "EVD-249"),
                    ["executor.change_building_skin"] = VerifiedEvidence(
                        "vanilla_actor_shared_native_building_appearance_executor_exact_Pet_Bowl_skin_or_Farmhouse_first_region_mouse_reachable_custom_paint_with_strict_receipt",
                        "EVD-249", "EVD-250"),
                    ["buildings.paint"] = VerifiedEvidence(
                        "vanilla_actor_exact_live_Farmhouse_first_paint_region_current_Robin_service_native_CarpenterMenu_mouse_reachable_custom_HSL_and_unchanged_sibling_receipt",
                        "EVD-250"),
                    ["buildings.construct"] = VerifiedEvidence(
                        "vanilla_host_purpose_bound_exact_live_base_blueprint_current_Robin_service_native_CarpenterMenu_Coop_on_Farm_money_material_placement_and_countdown_receipt",
                        "EVD-248"),
                    ["executor.construct_building"] = VerifiedEvidence(
                        "vanilla_host_exact_authorized_quest_or_general_strategy_Robin_service_native_CarpenterMenu_Coop_on_Farm_money_material_placement_and_countdown_receipt",
                        "EVD-248"),
                    ["fishing.catch_fish"] = VerifiedEvidence(
                        "vanilla_current_or_resolved_route_exact_fishable_cast_native_max_power_stochastic_distribution_bobber_bar_or_special_no_minigame_receipt_and_idle_cleanup",
                        "EVD-228"),
                    ["executor.catch_fish"] = VerifiedEvidence(
                        "vanilla_exact_fishable_cast_native_max_power_legal_input_bobber_control_stochastic_distribution_receipt_and_idle_cleanup",
                        "EVD-228"),
                    ["housing.advance_farmhouse"] = VerifiedEvidence(
                        "vanilla_host_exact_level_0_to_1_level_1_to_2_and_level_2_to_3_transparent_carpenter_candidate_daily_plan_native_dialogue_purchase_and_immediate_money_material_countdown_receipt",
                        "EVD-229"),
                    ["executor.purchase_farmhouse_upgrade"] = VerifiedEvidence(
                        "vanilla_host_exact_level_0_to_1_level_1_to_2_and_level_2_to_3_native_Carpenter_action_Upgrade_Yes_money_material_and_three_day_countdown_receipt",
                        "EVD-229"),
                    ["housing.renovate"] = VerifiedEvidence(
                        "vanilla_exact_live_18_entry_Data_HomeRenovations_explicit_player_command_candidate_daily_plan_fresh_collision_stand_rebind_native_Carpenter_HouseRenovations_RenovateMenu_all_branch_money_FirstPurchase_action_animation_return_and_no_refund_receipt",
                        "EVD-301"),
                    ["executor.renovate_home"] = VerifiedEvidence(
                        "vanilla_exact_live_18_entry_native_Carpenter_Renovate_HouseRenovations_shop_order_RenovateMenu_hover_world_region_click_all_branch_money_FirstPurchase_Value_Mail_animation_return_and_negative_price_no_FirstPurchase_no_refund_receipt",
                        "EVD-301"),
                    ["animals.purchase"] = VerifiedEvidence(
                        "vanilla_exact_live_stock_compatible_home_money_name_and_native_PurchaseAnimalsMenu_terminal_receipt_with_source_verified_rolling_route_Marnie_service_and_multi_location_paging",
                        "EVD-247"),
                    ["executor.choose_animal_purchase_response"] = VerifiedEvidence(
                        "vanilla_exact_native_Marnie_Purchase_paged_next_previous_and_exact_location_response_with_expected_menu_stage_receipt",
                        "EVD-247"),
                    ["executor.purchase_animal"] = VerifiedEvidence(
                        "vanilla_exact_native_PurchaseAnimalsMenu_stock_scroll_random_actual_type_home_selection_unique_name_money_owner_occupancy_and_return_to_shop_receipt",
                        "EVD-247"),
                    ["animals.manage_animal"] = VerifiedEvidence(
                        "vanilla_exact_loaded_base_animal_explicit_rename_reproduction_toggle_move_home_or_irreversible_sale_through_native_pet_and_AnimalQueryMenu_with_strict_receipt",
                        "EVD-252"),
                    ["executor.manage_animal"] = VerifiedEvidence(
                        "vanilla_exact_loaded_base_animal_native_initial_pet_query_menu_rename_reproduction_move_home_and_sale_four_branch_runtime_receipt",
                        "EVD-252"),
                    ["crafting.cook_recipe"] = VerifiedEvidence(
                        "vanilla_exact_learned_recipe_explicit_purpose_native_kitchen_or_cookout_source_material_and_qi_seasoning_consumption_output_quality_recipesCooked_quest_and_achievement_callback_receipt",
                        "EVD-253"),
                    ["executor.cook_recipe"] = VerifiedEvidence(
                        "vanilla_native_GameLocation_ActivateKitchen_mutex_or_Cookout_Torch_CraftingPage_recipe_click_exact_material_seasoning_output_and_recipesCooked_receipt",
                        "EVD-253"),
                    ["crafting.forge_item"] = VerifiedEvidence(
                        "vanilla_exact_loaded_forge_action_or_MiniForge_all_live_inventory_and_equipped_ring_inputs_all_nine_ForgeMenu_operation_families_exact_or_complete_native_random_output_contract",
                        "EVD-254"),
                    ["executor.forge_item"] = VerifiedEvidence(
                        "vanilla_native_ForgeMenu_inventory_or_equipment_slot_click_start_or_unforge_1600ms_lifecycle_exact_shard_timesEnchanted_input_and_output_domain_receipt",
                        "EVD-254"),
                    ["executor.apply_tree_treatment"] = VerifiedEvidence(
                        "vanilla_exact_current_location_Tree_and_inventory_vinegar_native_Object_placementAction_permanent_moss_suppression_stack_and_tree_flag_receipt",
                        "EVD-255"),
                    ["executor.place_cookout_kit"] = VerifiedEvidence(
                        "vanilla_exact_current_location_inventory_Cookout_Kit_native_Utility_tryToPlaceItem_Torch_278_destroyOvernight_stack_and_transparent_cooking_endpoint_receipt",
                        "EVD-256"),
                    ["executor.place_tent"] = VerifiedEvidence(
                        "vanilla_exact_base_TentKit_native_directional_3x2_outdoor_tomorrow_festival_area_clear_TerrainFeatures_Tent_stack_and_sleep_handoff_receipt",
                        "EVD-265"),
                    ["recovery.sleep_in_tent"] = VerifiedEvidence(
                        "vanilla_exact_loaded_Tent_canonical_grab_geometry_native_SleepTent_prompt_SleepTent_Yes_shared_cross_day_save_same_location_tile_wake_temporary_flag_reset_and_overnight_destruction_receipt",
                        "EVD-266"),
                    ["executor.place_crab_pot"] = VerifiedEvidence(
                        "vanilla_exact_current_location_inventory_Crab_Pot_native_Utility_tryToPlaceItem_CrabPot_owner_initial_state_stack_and_transparent_production_context_receipt",
                        "EVD-257"),
                    ["executor.place_fence"] = VerifiedEvidence(
                        "vanilla_all_five_exact_inventory_fence_identities_native_Utility_tryToPlaceItem_Fence_health_draw_gate_stack_and_route_safe_receipt",
                        "EVD-259"),
                    ["executor.place_flooring"] = VerifiedEvidence(
                        "vanilla_all_live_inventory_floor_path_identities_native_Utility_tryToPlaceItem_TerrainFeatures_Flooring_data_connection_view_passability_stack_receipt",
                        "EVD-260"),
                    ["executor.plant_grass"] = VerifiedEvidence(
                        "vanilla_exact_base_(O)297_and_(O)BlueGrassStarter_native_Utility_tryToPlaceItem_TerrainFeatures_Grass_type_1_or_7_four_initial_weeds_passability_and_stack_receipt",
                        "EVD-283"),
                    ["executor.place_furniture"] = VerifiedEvidence(
                        "vanilla_inventory_furniture_factory_runtime_rotation_wall_ground_table_endpoint_rectangular_footprint_stack_and_route_safe_native_placement_receipt",
                        "EVD-261"),
                    ["executor.place_sign"] = VerifiedEvidence(
                        "vanilla_all_live_sign_item_and_TextSign_inventory_identities_native_Utility_tryToPlaceItem_exact_empty_runtime_branch_stack_and_route_safe_receipt",
                        "EVD-262"),
                    ["executor.set_sign_display_item"] = VerifiedEvidence(
                        "vanilla_exact_base_Sign_selected_inventory_item_native_GameLocation_checkAction_getOne_display_type_source_state_preservation_and_authorized_replacement_receipt",
                        "EVD-263"),
                    ["executor.edit_text_sign"] = VerifiedEvidence(
                        "vanilla_exact_base_TextSign_native_GameLocation_checkAction_TitleTextInputMenu_60_code_unit_keyboard_filter_trim_token_display_showNextIndex_and_replacement_receipt",
                        "EVD-264"),
                    ["executor.load_crab_pot_bait"] = VerifiedEvidence(
                        "vanilla_exact_current_location_empty_base_CrabPot_native_Category_minus_21_probe_GameLocation_checkAction_bait_owner_inventory_unit_state_and_lifecycle_receipt",
                        "EVD-258"),
                    ["joja.advance_development"] = VerifiedEvidence(
                        "vanilla_host_exact_undecided_route_membership_with_or_without_first_Morris_greeting_and_all_five_joja_project_candidates_daily_plan_native_purchase_immediate_money_pending_mail_and_next_day_settlement",
                        "EVD-232"),
                    ["executor.purchase_joja_membership"] = VerifiedEvidence(
                        "vanilla_host_exact_JoinJoja_native_Morris_greeting_offer_confirmation_5000_money_and_JojaMember_next_day_settlement",
                        "EVD-232"),
                    ["executor.purchase_joja_project"] = VerifiedEvidence(
                        "vanilla_host_exact_JoinJoja_native_JojaCDMenu_all_five_button_price_cc_mail_joja_mail_and_next_day_settlement",
                        "EVD-232"),
                    ["farm.process_machines"] = BoundedEvidence(
                        "vanilla_bounded_aggregate_of_existing_exact_machine_service_native_craft_current_or_resolved_route_placement_idle_relocation_ordinary_storage_and_deterministic_incubator_naming_chains",
                        readEvidenceIds: new[] { "EVD-227" },
                        candidateEvidenceIds: new[] { "EVD-227" },
                        compilerEvidenceIds: new[] { "EVD-227" },
                        runtimeEvidenceIds: new[] { "EVD-227" },
                        outputEvidenceIds: new[] { "EVD-227" }),
                    ["farm.maintain_crops"] = VerifiedEvidence(
                        "vanilla_current_location_one_exact_candidate_per_fresh_snapshot_native_terrain_HoeDirt_water_plant_fertilize_grab_or_scythe_harvest_IndoorPot_fertilize_and_giant_crop_axe_lifecycle",
                        "EVD-226"),
                    ["exploration.visit_location"] = BoundedEvidence(
                        "vanilla_current_location_one_exact_resolved_cross_location_connector_or_one_exact_clearable_route_obstacle_then_fresh_snapshot",
                        readEvidenceIds: new[] { "EVD-025", "EVD-058" },
                        candidateEvidenceIds: new[] { "EVD-042", "EVD-103", "EVD-218" },
                        compilerEvidenceIds: new[] { "EVD-058", "EVD-103", "EVD-218" },
                        runtimeEvidenceIds: new[] { "EVD-058", "EVD-189", "EVD-218" },
                        outputEvidenceIds: new[] { "EVD-058", "EVD-189", "EVD-218" }),
                    ["economy.buy_supplies"] = BoundedEvidence(
                        "vanilla_safe_single_money_purchase_rolling_resolved_route_exact_shop_endpoint_optional_whitelisted_dialogue_native_buy_and_menu_cleanup",
                        readEvidenceIds: new[] { "EVD-013", "EVD-014", "EVD-015", "EVD-219" },
                        candidateEvidenceIds: new[] { "EVD-059", "EVD-219" },
                        compilerEvidenceIds: new[] { "EVD-062", "EVD-219" },
                        runtimeEvidenceIds: new[] { "EVD-062", "EVD-219" },
                        outputEvidenceIds: new[] { "EVD-062", "EVD-219" }),
                    ["economy.sell_items"] = BoundedEvidence(
                        "vanilla_one_explicitly_authorized_unprotected_positive_value_stack_rolling_resolved_route_exact_shop_endpoint_optional_whitelisted_dialogue_native_sale_and_background_safe_menu_cleanup",
                        readEvidenceIds: new[] { "EVD-018", "EVD-020", "EVD-022", "EVD-024", "EVD-220" },
                        candidateEvidenceIds: new[] { "EVD-038", "EVD-220" },
                        compilerEvidenceIds: new[] { "EVD-220" },
                        runtimeEvidenceIds: new[] { "EVD-220" },
                        outputEvidenceIds: new[] { "EVD-220" }),
                    ["economy.ship_items"] = BoundedEvidence(
                        "vanilla_one_explicitly_authorized_unprotected_positive_shipping_payout_item_rolling_resolved_route_exact_bin_approach_native_single_item_deposit_immediate_inventory_bin_receipt_and_delayed_day_settlement",
                        readEvidenceIds: new[] { "EVD-018", "EVD-020", "EVD-022", "EVD-024", "EVD-221" },
                        candidateEvidenceIds: new[] { "EVD-038", "EVD-221" },
                        compilerEvidenceIds: new[] { "EVD-221" },
                        runtimeEvidenceIds: new[] { "EVD-221" },
                        outputEvidenceIds: new[] { "EVD-221" }),
                    ["farm.collect_animal_products"] = VerifiedEvidence(
                        "vanilla_current_location_exact_ready_base_farm_animal_milk_pail_shears_cracker_single_double_native_inventory_receipt_stats_farming_xp_energy_and_friendship",
                        "EVD-222"),
                    ["farm.care_for_pets"] = VerifiedEvidence(
                        "vanilla_current_location_exact_base_pet_native_checkAction_normal_and_max_friendship_gift_output_dynamic_bounding_box_rebind_and_base_pet_bowl_watering_native_sleep_dayUpdate_durable_settlement",
                        "EVD-223"),
                    ["museum.donate_items"] = VerifiedEvidence(
                        "vanilla_current_location_exact_donatable_object_native_MuseumMenu_fade_inventory_display_and_confirm_exit_quest24_completion_all_data_driven_pending_item_rewards_supported_non_item_reward_actions_and_collection_achievement",
                        "EVD-224"),
                    ["island.field_office_donate"] = VerifiedEvidence(
                        "vanilla_exact_11_slot_field_office_fossil_mapping_rolling_island_route_native_FieldOfficeDesk_mutex_Safari_Donate_FieldOfficeMenu_inventory_holder_and_exit_set_reward_nut_and_finale_readiness_receipt",
                        "EVD-302"),
                    ["executor.donate_field_office_piece"] = VerifiedEvidence(
                        "vanilla_native_FieldOfficeDesk_mutex_Safari_Donate_FieldOfficeMenu_exact_inventory_and_piece_holder_click_one_fossil_set_reward_collected_nut_and_exit_receipt",
                        "EVD-302"),
                    ["island.field_office_survey"] = VerifiedEvidence(
                        "vanilla_unique_next_FieldOfficeSurvey_rolling_island_route_exact_22_or_18_native_dialogue_plant_collected_nut_walnut_debris_failed_day_lock_and_finale_receipt",
                        "EVD-303"),
                    ["executor.answer_field_office_survey"] = VerifiedEvidence(
                        "vanilla_native_FieldOfficeSurvey_Survey_Yes_exact_Correct_response_plant_collected_nut_walnut_debris_and_finale_receipt",
                        "EVD-303"),
                    ["minigame.play_calico_jack"] = VerifiedEvidence(
                        "vanilla_missing_(BC)126_rarecrow_currency_demand_exact_ClubCards_table_seed_replay_hidden_card_future_draw_native_round_coin_settlement_and_single_round_exit_receipt",
                        "EVD-304"),
                    ["executor.play_calico_jack"] = VerifiedEvidence(
                        "vanilla_native_ClubCards_or_BlackJack_Play_real_CalicoJack_shared_exact_seed_replay_hit_or_stand_coin_delta_and_quit_executor_calibration_only",
                        "EVD-304"),
                    ["minigame.play_crane_game"] = VerifiedEvidence(
                        "vanilla_MovieTheater_machine_occupancy_500g_three_attempt_live_prize_physics_native_directional_input_and_ItemGrabMenu_reward_conservation_player_command_only",
                        "EVD-305"),
                    ["executor.play_crane_game"] = VerifiedEvidence(
                        "vanilla_native_CraneGame_right_down_input_live_prize_selection_three_attempts_exact_fee_reward_transfer_and_cleanup_executor_calibration_only",
                        "EVD-305"),
                    ["community_center.donate_bundle_items"] = VerifiedEvidence(
                        "vanilla_current_location_exact_live_BundleData_native_JunimoNoteMenu_bundle_inventory_ingredient_and_exit_lifecycle_bundle_reward_area_completion_mail_new_note_camera_settlement_and_distinct_bulletin_interaction_endpoint",
                        "EVD-225"),
                    ["farm.collect_machine_outputs"] = VerifiedEvidence(
                        "vanilla_current_location_exact_ready_non_incubator_machine_output_native_inventory_receipt_structured_skill_and_mastery",
                        "EVD-213"),
                    ["farm.load_supported_machine_input"] = VerifiedEvidence(
                        "vanilla_current_location_exact_placement_bound_positive_deterministic_machine_support_input_no_additional_consumption_unreserved_native_load_and_processing_completion",
                        "EVD-214"),
                    ["farm.establish_supported_machine_capacity"] = VerifiedEvidence(
                        "vanilla_current_location_single_bounded_positive_machine_capacity_craft_exact_placement_binding_deterministic_input_load_processing_completion_and_training_rows_or_exact_ordinary_or_special_collection_task_capacity_craft_or_inventory_placement_zero_additional_consumption_natural_collect_receipt",
                        "EVD-215",
                        "EVD-217"),
                    ["farm.fulfill_machine_task_demand"] = VerifiedEvidence(
                        "vanilla_current_location_existing_machine_exact_zero_additional_consumption_input_source_natural_processing_and_native_ordinary_or_special_collection_receipt",
                        "EVD-216"),
                    ["fishing.collect_crab_pots"] = VerifiedEvidence(
                        "vanilla_current_location_exact_ready_base_crab_pot_native_collect_book_double_inventory_receipt_fishing_xp_caught_fish_bait_and_ready_reset",
                        "EVD-209"),
                    ["fishing.service_fish_ponds"] = VerifiedEvidence(
                        "vanilla_exact_completed_fish_pond_native_output_collect_and_authorized_population_request_inventory_fishing_xp_gate_and_reset_lifecycle",
                        "EVD-210"),
                    ["fishing.manage_fish_pond"] = VerifiedEvidence(
                        "vanilla_exact_completed_species_bound_fish_pond_explicit_player_command_native_right_click_PondQueryMenu_cycle_netting_or_confirmed_ClearPond_exact_fish_debris_reset_and_preserved_state_receipt",
                        "EVD-297"),
                    ["foraging.collect_spawned_objects"] = VerifiedEvidence(
                        "vanilla_current_location_exact_base_spawned_object_ordinary_botanist_deterministic_gatherer_special_724519_and_farm_interior_native_pickup_matrix",
                        "EVD-211"),
                    ["foraging.clear_green_rain_bushes"] = VerifiedEvidence(
                        "vanilla_current_location_exact_base_green_rain_resource_clump_indexes_44_46_seeded_core_outputs_bounded_secret_note_native_axe_and_task_receipt",
                        "EVD-212"),
                    ["inventory.transfer_item"] = VerifiedEvidence(
                        "explicit_bidirectional_player_normal_chest_transfer",
                        "EVD-192"),
                    ["foraging.harvest_ginger"] = VerifiedEvidence(
                        "vanilla_current_location_exact_ginger_dry_standard_rain_efficient_full_inventory_debris_energy_xp_matrix",
                        "EVD-119"),
                    ["foraging.harvest_bushes"] = VerifiedEvidence(
                        "vanilla_current_location_exact_bush_berry_standard_botanist_tea_leaf_golden_walnut_collected_walnut_and_cooldown_matrix",
                        "EVD-120"),
                    ["foraging.harvest_fruit_tree"] = VerifiedEvidence(
                        "vanilla_exact_fruit_tree_single_and_three_fruit_quality_lightning_coal_empty_and_active_shake_native_checkAction_matrix",
                        "EVD-298"),
                    ["foraging.harvest_tree_product"] = VerifiedEvidence(
                        "vanilla_exact_base_wild_tree_seed_hazelnut_island_palm_complete_random_output_domain_no_seed_active_shake_and_tapped_native_checkAction_matrix",
                        "EVD-299"),
                    ["foraging.rummage_garbage"] = VerifiedEvidence(
                        "vanilla_exact_map_Garbage_action_locked_Data_GarbageCans_deterministic_nonmutating_empty_standard_direct_inventory_hat_desert_multiple_debris_checked_and_NPC_reaction_native_checkAction_matrix",
                        "EVD-300"),
                    ["foraging.pan_ore_spot"] = VerifiedEvidence(
                        "vanilla_current_location_exact_active_ore_spot_live_pan_reward_projection_copper_steel_lifecycle_receipt_xp_times_panned_and_respawn_observation",
                        "EVD-208"),
                    ["mining.claim_reward_chests"] = VerifiedEvidence(
                        "loaded_vanilla_mineshaft_exact_reward_chests_fixed_stardrop_forced_random_receipt_and_cleanup_matrix",
                        "EVD-122"),
                    ["rewards.claim_pot_of_gold"] = VerifiedEvidence(
                        "vanilla_spring_17_Forest_exact_PotOfGold_native_checkAction_full_inventory_year_scaled_GoldCoin_and_LeprechuanHat_debris_conservation_and_shared_pickup_handoff",
                        "EVD-268"),
                    ["mining.choose_dwarf_statue_power"] = VerifiedEvidence(
                        "vanilla_mining_mastery_exact_daily_two_offer_rng_all_five_power_projections_native_object_menu_click_and_selected_day_buff_receipt",
                        "EVD-269"),
                    ["rewards.claim_statue_blessing"] = VerifiedEvidence(
                        "vanilla_farming_mastery_exact_daily_rng_rain_festival_denominator_all_seven_effect_projections_native_object_action_and_day_buff_receipt",
                        "EVD-270"),
                    ["mining.reach_depth"] = VerifiedEvidence(
                        "candidate_bound_ordinary_mine_rolling_current_floor_supported_steps_and_unlocked_native_elevator_checkpoint_shortcut",
                        "EVD-095",
                        "EVD-246"),
                    ["mining.use_elevator"] = VerifiedEvidence(
                        "vanilla_ordinary_mines_unlocked_checkpoint_exact_endpoint_native_MineElevatorMenu_selection_and_bidirectional_destination_receipt",
                        "EVD-246"),
                    ["mining.obtain_skull_key"] = VerifiedEvidence(
                        "ordinary_mines_floor_119_to_120_native_skull_key_chest_claim_false_to_true_and_exit",
                        "EVD-106"),
                    ["mining.acquire_golden_scythe"] = VerifiedEvidence(
                        "vanilla_quarry_mine_sentinel_77377_daily_plan_rolling_native_combat_clearance_movement_golden_scythe_claim_and_mine_exit_with_explicit_confirmation",
                        "EVD-231"),
                    ["recovery.stabilize_day"] = BoundedEvidence(
                        "all_current_recovery_candidates_including_rolling_cross_map_return_and_terminal_native_sleep",
                        readEvidenceIds: new[] { "EVD-045", "EVD-046" },
                        candidateEvidenceIds: new[] { "EVD-044", "EVD-050" },
                        compilerEvidenceIds: new[] { "EVD-050", "EVD-195" },
                        runtimeEvidenceIds: new[] { "EVD-195" },
                        outputEvidenceIds: new[] { "EVD-195" }),
                    ["social.gift_npc"] = BoundedEvidence(
                        "vanilla_current_loaded_npc_gift_same_map_or_rolling_resolved_route_with_single_item_consumed_to_null",
                        readEvidenceIds: new[] { "EVD-076" },
                        candidateEvidenceIds: new[] { "EVD-076", "EVD-104" },
                        compilerEvidenceIds: new[] { "EVD-076", "EVD-104", "EVD-196" },
                        runtimeEvidenceIds: new[] { "EVD-196" },
                        outputEvidenceIds: new[] { "EVD-196" }),
                    ["social.advance_partnership"] = VerifiedEvidence(
                        "vanilla_current_loaded_exact_bouquet_marriage_proposal_or_krobus_roommate_transition_and_native_cross_day_wedding_settlement_with_explicit_confirmation",
                        "EVD-230"),
                    ["social.talk_npc"] = VerifiedEvidence(
                        "vanilla_current_loaded_npc_talk_same_map_or_rolling_resolved_route_with_safe_dialogue_close",
                        "EVD-076",
                        "EVD-105"),
                    ["skills.read_books"] = VerifiedEvidence(
                        "all_six_vanilla_base_book_branch_families_exact_projection_native_use_and_durable_output",
                        "EVD-124"),
                    ["executor.read_secret_note"] = VerifiedEvidence(
                        "vanilla_exact_secret_note_and_journal_scrap_unseen_selection_native_use_note_seen_quest_menu_and_single_item_receipt",
                        "EVD-284"),
                    ["executor.use_firework"] = VerifiedEvidence(
                        "vanilla_all_three_exact_base_firework_variants_native_placement_transient_collision_fuse_sprite_random_domain_and_single_item_receipt_player_command_only",
                        "EVD-285"),
                    ["executor.use_horse_flute"] = VerifiedEvidence(
                        "vanilla_exact_base_horse_flute_all_native_use_and_warp_restrictions_adjacent_noop_delayed_recheck_team_event_mutex_warp_and_reusable_inventory_receipt_executor_calibration_only",
                        "EVD-286"),
                    ["executor.use_monster_musk"] = VerifiedEvidence(
                        "vanilla_exact_base_monster_musk_native_object_use_single_item_consumption_delayed_callback_buff24_remove_replace_full_duration_and_ordinary_mine_volcano_double_spawn_semantics_executor_calibration_only",
                        "EVD-287"),
                    ["executor.use_rain_totem"] = VerifiedEvidence(
                        "vanilla_exact_base_rain_totem_native_object_use_single_item_consumption_location_context_allow_and_routing_default_festival_guard_context_weather_transition_animation_and_receipt_executor_calibration_only",
                        "EVD-288"),
                    ["executor.use_return_scepter"] = VerifiedEvidence(
                        "vanilla_exact_base_return_scepter_native_instant_tool_use_own_farmhouse_or_cabin_front_door_delayed_warp_full_transition_and_reusable_inventory_receipt_executor_calibration_only",
                        "EVD-289"),
                    ["executor.use_treasure_totem"] = VerifiedEvidence(
                        "vanilla_exact_base_treasure_totem_native_object_use_single_item_consumption_outdoors_gate_all_16_rounded_distance_ring_tiles_exact_spawn_subset_global_treasure_totem_counter_and_artifact_spot_receipt_executor_calibration_only",
                        "EVD-290"),
                    ["executor.use_warp_totem"] = VerifiedEvidence(
                        "vanilla_all_five_exact_base_warp_totems_native_object_use_single_item_consumption_farm_map_property_all_fixed_destinations_active_and_passive_festival_routing_delayed_warp_and_final_state_receipt_executor_calibration_only",
                        "EVD-291"),
                    ["festival.manage_grange_display"] = VerifiedEvidence(
                        "vanilla_fall16_shared_team_grange_exact_live_sell_price_quality_category_optimizer_one_fresh_snapshot_native_StorageContainer_mutation_and_post_judging_retrieval_receipt",
                        "EVD-292"),
                    ["executor.manage_grange_display"] = VerifiedEvidence(
                        "vanilla_fall16_exact_native_Event_checkAction_grange_mutex_StorageContainer_single_display_mutation_inventory_score_and_judging_receipt_executor_calibration_only",
                        "EVD-292"),
                    ["festival.play_fishing_game"] = VerifiedEvidence(
                        "vanilla_fall16_exact_50g_100_second_native_FishingGame_stardrop_bounded_candidate_shared_predictive_legal_input_exact_stochastic_score_perfection_star_token_and_festival_return_receipt",
                        "EVD-293"),
                    ["executor.play_fair_fishing_game"] = VerifiedEvidence(
                        "vanilla_fall16_native_Event_checkAction_fishingGame_Play_dialogue_50g_fee_real_FishingGame_shared_predictive_legal_input_exact_stochastic_reward_and_cleanup_executor_calibration_only",
                        "EVD-293"),
                    ["festival.play_slingshot_game"] = VerifiedEvidence(
                        "vanilla_fall16_exact_50g_50_second_native_TargetGame_stardrop_bounded_candidate_shared_predictive_intercept_legal_input_exact_target_schedule_accuracy_multiplier_score_star_token_and_festival_return_receipt",
                        "EVD-294"),
                    ["executor.play_fair_slingshot_game"] = VerifiedEvidence(
                        "vanilla_fall16_native_Event_checkAction_slingshotGame_Play_dialogue_50g_fee_real_TargetGame_shared_predictive_intercept_legal_input_exact_reward_and_cleanup_executor_calibration_only",
                        "EVD-294"),
                    ["festival.play_strength_game"] = VerifiedEvidence(
                        "vanilla_fall16_free_exact_one_token_stardrop_top_up_live_buildings_540_x29_endpoint_single_native_click_predictive_maximum_power_exact_star_token_result_dialogue_and_cleanup_receipt",
                        "EVD-295"),
                    ["executor.play_fair_strength_game"] = VerifiedEvidence(
                        "vanilla_fall16_native_Event_checkAction_direct_StrengthGame_shared_BFS_settled_movement_single_native_click_original_168_80ms_8_frame_swing_power_99_or_100_exact_one_token_and_cleanup_executor_calibration_only",
                        "EVD-295"),
                    ["festival.spin_wheel"] = VerifiedEvidence(
                        "vanilla_fall16_stardrop_bounded_green_zero_luck_kelly_7_of_15_wager_exact_22_of_30_constructor_distribution_effective_LuckLevel_native_random_plus_or_minus_wager_and_cleanup_receipt",
                        "EVD-296"),
                    ["executor.spin_fair_wheel"] = VerifiedEvidence(
                        "vanilla_fall16_native_Event_checkAction_wheelBet_Green_NumberSelectionMenu_exact_wager_real_WheelSpinGame_random_settlement_and_cleanup_executor_calibration_only",
                        "EVD-296"),
                    ["skills.choose_profession"] = VerifiedEvidence(
                        "all_30_vanilla_professions_five_skills_level_5_and_both_level_10_branches_exact_live_menu_projection_shared_level_up_completion_persistent_profession_pending_level_menu_and_immediate_health_stamina_receipts",
                        "EVD-244"),
                    ["mail.process_letter"] = VerifiedEvidence(
                        "vanilla_native_order_owned_farm_mailbox_all_locked_Data_mail_directives_exact_LetterViewer_pages_attachments_quests_special_orders_stardrop_overflow_and_native_receipts",
                        "EVD-245"),
                    ["volcano.reach_caldera"] = VerifiedEvidence(
                        "vanilla_volcano_generated_levels_0_to_9_rolling_native_actions_typed_combat_intent_to_caldera",
                        "EVD-190",
                        "EVD-191")
                });

        private static readonly IReadOnlyList<OptionCapabilityDeclaration> Options = BuildOptions();
        private static readonly IReadOnlyDictionary<string, OptionCapabilityDeclaration> OptionsById =
            new ReadOnlyDictionary<string, OptionCapabilityDeclaration>(
                Options.ToDictionary(row => row.OptionId, StringComparer.Ordinal));
        private static readonly IReadOnlyCollection<string> EligibleTrainingOptionIds =
            new ReadOnlyCollection<string>(Options
                .Where(TrainingEligibilityPolicy.IsEligible)
                .Select(row => row.OptionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        private static readonly IReadOnlyList<DailyCandidateCapabilityDeclaration> Candidates =
            new ReadOnlyCollection<DailyCandidateCapabilityDeclaration>(new[]
            {
                SupportedCandidate("accept_daily_quest"),
                SupportedCandidate("daily_quest_board_approach"),
                SupportedCandidate("accept_special_order"),
                SupportedCandidate("claim_quest_reward"),
                SupportedCandidate("special_order_board_approach"),
                SupportedCandidate("special_order_board_open"),
                SupportedCandidate("special_order_board_dialogue_advance"),
                SupportedCandidate("buy_shop_item"), SupportedCandidate("catch_fish"),
                SupportedCandidate("claim_mine_reward_chest"),
                SupportedCandidate("claim_pot_of_gold"),
                SupportedCandidate("choose_dwarf_statue_power"),
                SupportedCandidate("claim_statue_blessing"),
                SupportedCandidate("rotate_house_plant"),
                SupportedCandidate("play_singing_stone"),
                SupportedCandidate("tune_flute_block"),
                SupportedCandidate("tune_drum_block"),
                SupportedCandidate("read_farm_computer_report"),
                SupportedCandidate("collect_slime_ball"),
                SupportedCandidate("withdraw_feed_hopper_hay"),
                SupportedCandidate("collect_auto_grabber_contents"),
                SupportedCandidate("use_mini_obelisk"),
                SupportedCandidate("clear_farm_resource_clump"),
                SupportedCandidate("clear_green_rain_resource_clump"),
                SupportedCandidate("clear_obstacle_tile"), SupportedCandidate("collect_animal_product"),
                SupportedCandidate("animal_purchase_select_service"),
                SupportedCandidate("animal_purchase_navigate_location_page"),
                SupportedCandidate("animal_purchase_select_location"),
                SupportedCandidate("purchase_animal"),
                SupportedCandidate("manage_animal"),
                SupportedCandidate("manage_fish_pond"),
                SupportedCandidate("cook_recipe"),
                SupportedCandidate("forge_item"),
                SupportedCandidate("collect_crab_pot"), SupportedCandidate("collect_fish_pond_output"),
                SupportedCandidate("collect_machine_output_tile"),
                SupportedCandidate("collect_spawned_object"),
                SupportedCandidate("complete_fish_pond_request"),
                SupportedCandidate("construct_quest_building"),
                SupportedCandidate("construct_building"),
                SupportedCandidate("change_building_skin"),
                SupportedCandidate("paint_building_region"),
                SupportedCandidate("craft_machine_item"),
                SupportedCandidate("craft_storage_item"),
                SupportedCandidate("craft_quest_item"),
                SupportedCandidate("place_machine_item"),
                SupportedCandidate("relocate_machine_item"),
                SupportedCandidate("place_storage_item"),
                SupportedCandidate("donate_community_center_item"),
                SupportedCandidate("donate_museum_item"), SupportedCandidate("donate_field_office_piece"), SupportedCandidate("answer_field_office_survey"), SupportedCandidate("manage_grange_display"), SupportedCandidate("play_fair_fishing_game"), SupportedCandidate("play_fair_slingshot_game"), SupportedCandidate("play_fair_strength_game"), SupportedCandidate("spin_fair_wheel"), SupportedCandidate("fill_pet_bowl"),
                SupportedCandidate("play_calico_jack"),
                SupportedCandidate("play_crane_game"),
                SupportedCandidate("harvest_bush"), SupportedCandidate("harvest_fruit_tree"), SupportedCandidate("harvest_tree_product"), SupportedCandidate("rummage_garbage"), SupportedCandidate("harvest_crop_tile"),
                SupportedCandidate("harvest_giant_crop_tile"), SupportedCandidate("harvest_ginger"),
                SupportedCandidate("interact_endpoint"), SupportedCandidate("load_machine_input_tile"),
                SupportedCandidate("mailbox_approach"),
                SupportedCandidate("mine_elevator_approach"),
                SupportedCandidate("name_hatched_animal"),
                SupportedCandidate("mining_acquire_golden_scythe_plan_envelope"),
                SupportedCandidate("mining_collect_quest_resource_plan_envelope"),
                SupportedCandidate("mining_obtain_skull_key_plan_envelope"),
                SupportedCandidate("mining_reach_depth_plan_envelope"),
                SupportedCandidate("mining_slay_monsters_plan_envelope"), SupportedCandidate("pan_ore_spot"),
                SupportedCandidate("pet_daily_interaction"), SupportedCandidate("pickup_debris_item"),
                SupportedCandidate("plant_seed_tile"), SupportedCandidate("apply_fertilizer_tile"),
                SupportedCandidate("purchase_farmhouse_expansion"),
                SupportedCandidate("purchase_farmhouse_upgrade"),
                SupportedCandidate("renovate_home"),
                SupportedCandidate("purchase_joja_membership"),
                SupportedCandidate("purchase_joja_project"), SupportedCandidate("read_inventory_book"),
                SupportedCandidate("choose_profession"),
                SupportedCandidate("open_mailbox_letter"),
                SupportedCandidate("open_mine_elevator"),
                SupportedCandidate("process_open_letter"),
                SupportedCandidate("select_mine_elevator_floor"),
                SupportedCandidate("recovery_close_menu"), SupportedCandidate("recovery_refresh_plan"),
                SupportedCandidate("recovery_resume_sleep_prompt"),
                SupportedCandidate("recovery_return_home"),
                SupportedCandidate("recovery_sleep_before_collapse"),
                SupportedCandidate("recovery_sleep_immediately"),
                SupportedCandidate("recovery_escape_object_trap"), SupportedCandidate("route_connector_tile"),
                SupportedCandidate("quest_drop_box_donation"),
                SupportedCandidate("play_junimo_kart"),
                SupportedCandidate("quest_npc_interaction"),
                SupportedCandidate("ship_inventory_item_to_bin"),
                SupportedCandidate("transfer_inventory_item"),
                SupportedCandidate("social_continuation_retry_wait"),
                SupportedCandidate("social_gift_current"), SupportedCandidate("social_talk_current"),
                SupportedCandidate("partnership_bouquet_current"),
                SupportedCandidate("partnership_propose_marriage_current"),
                SupportedCandidate("partnership_propose_roommate_current"),
                SupportedCandidate("volcano_reach_caldera_plan_envelope"),
                SupportedCandidate("water_crop_tile"), SupportedCandidate("sell_shop_item"),
                BlockedCandidate("purchase_service_gate", "purchase_service_gate_excluded_upstream"),
                BlockedCandidate("purchase_stage_blocked", "purchase_stage_not_compilable"),
                BlockedCandidate("quest_candidate", "quest_objective_binding_not_executable"),
                BlockedCandidate("special_order_candidate", "special_order_objective_binding_not_executable")
            });

        static OptionCapabilityRegistrySource()
        {
            if (Candidates.Select(row => row.Kind).Distinct(StringComparer.Ordinal).Count() != Candidates.Count)
            {
                throw new InvalidOperationException("Capability registry contains duplicate daily candidate kinds.");
            }

            if (Candidates.Any(row =>
                string.IsNullOrWhiteSpace(row.Kind) ||
                (!row.Compilable && string.IsNullOrWhiteSpace(row.BlockReason))))
            {
                throw new InvalidOperationException("Capability registry contains an incomplete daily candidate declaration.");
            }

            if (EligibleTrainingOptionIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "Capability registry produced an empty policy-training allowlist.");
            }

            if (Options.Any(row =>
                (TrainingEligibilityPolicy.IsEligible(row) && row.TrainingExclusionReasons.Length != 0) ||
                (!TrainingEligibilityPolicy.IsEligible(row) && row.TrainingExclusionReasons.Length == 0)))
            {
                throw new InvalidOperationException(
                    "Capability registry contains an inconsistent training-admission declaration.");
            }

            if (Options.Any(row =>
                TrainingEligibilityPolicy.IsEligible(row) &&
                (string.IsNullOrWhiteSpace(row.TrainingEvidenceScope) ||
                 string.Equals(row.TrainingEvidenceScope, "not_admitted", StringComparison.Ordinal))))
            {
                throw new InvalidOperationException(
                    "Capability registry contains an eligible option without an evidence scope.");
            }
        }

        public static IReadOnlyList<OptionCapabilityDeclaration> All => Options;
        public static IReadOnlyList<DailyCandidateCapabilityDeclaration> DailyCandidates => Candidates;
        public static IReadOnlyCollection<string> RegisteredIds { get; } =
            new ReadOnlyCollection<string>(RegisteredOptionIds
                .Concat(NativeObjectCapabilitySeeds.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        public static IReadOnlyCollection<string> TrainingAllowlist => EligibleTrainingOptionIds;

        public static bool TryGet(string optionId, out OptionCapabilityDeclaration declaration)
        {
            return OptionsById.TryGetValue(optionId, out declaration!);
        }

        public static OptionCapabilityDeclaration GetRequired(string optionId)
        {
            if (!TryGet(optionId, out var declaration))
            {
                throw new KeyNotFoundException($"No capability declaration for option '{optionId}'.");
            }

            return declaration;
        }

        private static IReadOnlyList<OptionCapabilityDeclaration> BuildOptions()
        {
            var rows = RegisteredOptionIds.Concat(NativeObjectCapabilitySeeds.Keys).Distinct(StringComparer.Ordinal).Select(id =>
            {
                NativeObjectCapabilitySeeds.TryGetValue(id, out var seed);
                var hasStep = seed?.StepCompiler ?? StepCompilerIds.Contains(id);
                var hasParameter = seed?.ParameterCompiler ?? ParameterCompilerIds.Contains(id);
                var candidateStatus = id.StartsWith("executor.", StringComparison.Ordinal)
                    ? CapabilityCandidateStatus.NotApplicable
                    : id == "quest.advance"
                        ? QuestActionCoverageCatalog.All.Any(row =>
                            string.Equals(
                                row.BindingStatus,
                                QuestActionCoverageCatalog.Blocked,
                                StringComparison.Ordinal))
                            ? CapabilityCandidateStatus.PartiallyBlocked
                            : CapabilityCandidateStatus.Declared
                        : CapabilityCandidateStatus.Declared;
                var compilerStatus = hasStep && hasParameter
                    ? CapabilityCompilerStatus.StepAndParameterCompilerDeclared
                    : hasStep
                        ? CapabilityCompilerStatus.StepCompilerDeclared
                        : hasParameter
                            ? CapabilityCompilerStatus.ParameterCompilerDeclared
                            : CapabilityCompilerStatus.Unbound;
                var policyTrainingCandidate =
                    !id.StartsWith("executor.", StringComparison.Ordinal) &&
                    !(seed?.CalibrationOnly ?? CalibrationOnlyHighLevelIds.Contains(id)) &&
                    !(seed?.PlayerCommandOnly ?? PlayerCommandOnlyIds.Contains(id));
                var evidence = seed?.Evidence;
                evidence ??= TrainingEvidenceByOptionId.TryGetValue(id, out var legacyEvidence)
                    ? legacyEvidence
                    : null;
                evidence ??= new TrainingEvidence();
                var readGate = Gate(evidence.ReadEvidenceIds, declared: true);
                var candidateGate = Gate(
                    evidence.CandidateEvidenceIds,
                    candidateStatus == CapabilityCandidateStatus.Declared);
                var compilerGate = Gate(
                    evidence.CompilerEvidenceIds,
                    compilerStatus != CapabilityCompilerStatus.Unbound &&
                    ((seed?.HarnessDispatch ?? HarnessDispatchIds.Contains(id)) ||
                     (seed?.InternalExecution ?? InternalHighLevelExecutionIds.Contains(id))));
                var runtimeGate = Gate(evidence.RuntimeEvidenceIds, declared: false);
                var outputGate = Gate(evidence.OutputEvidenceIds, declared: false);
                var exclusions = BuildTrainingExclusionReasons(
                    policyTrainingCandidate,
                    seed?.PlayerConfirmation ?? PlayerConfirmationIds.Contains(id),
                    seed?.PlayerCommandOnly ?? PlayerCommandOnlyIds.Contains(id),
                    readGate,
                    candidateGate,
                    compilerGate,
                    runtimeGate,
                    outputGate);
                var trainingEligible = exclusions.Length == 0;
                return new OptionCapabilityDeclaration
                {
                    OptionId = id,
                    RegistrationStatus = CapabilityRegistrationStatus.Registered,
                    ReadStatus = CapabilityReadStatus.RequiredFactContractDeclared,
                    CandidateStatus = candidateStatus,
                    CompilerStatus = compilerStatus,
                    HarnessDispatchSupported = seed?.HarnessDispatch ?? HarnessDispatchIds.Contains(id),
                    ProductExecutorSupported = false,
                    InternalExecutionPipelineSupported =
                        (seed?.HarnessDispatch ?? HarnessDispatchIds.Contains(id)) ||
                        (seed?.InternalExecution ?? InternalHighLevelExecutionIds.Contains(id)),
                    BeforeVerifierStatus = trainingEligible
                        ? CapabilityVerifierStatus.RuntimeVerified
                        : CapabilityVerifierStatus.ContractDeclared,
                    AfterVerifierStatus = outputGate == TrainingEvidenceGateStatus.RuntimeVerified
                        ? CapabilityVerifierStatus.RuntimeVerified
                        : CapabilityVerifierStatus.PendingRuntimeEvidence,
                    RuntimeEvidenceStatus = runtimeGate == TrainingEvidenceGateStatus.RuntimeVerified
                        ? OptionRuntimeStatus.RuntimeVerified
                        : OptionRuntimeStatus.RegisteredOnly,
                    TrainingEligibility = trainingEligible
                        ? OptionTrainingEligibility.Eligible
                        : readGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                          candidateGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                          compilerGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                          runtimeGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                          outputGate == TrainingEvidenceGateStatus.RuntimeVerified
                            ? OptionTrainingEligibility.EvaluationOnly
                            : OptionTrainingEligibility.BlockedPendingRuntimeEvidence,
                    AutonomousCandidateEnabled = seed?.AutonomousCandidate ?? AutonomousCandidateIds.Contains(id),
                    PlayerConfirmationRequired = seed?.PlayerConfirmation ?? PlayerConfirmationIds.Contains(id),
                    HostOnly = HostOnlyIds.Contains(id),
                    ProductIntegrationStatus = CapabilityProductIntegrationStatus.NotIntegrated,
                    PolicyTrainingCandidate = policyTrainingCandidate,
                    InvocationPolicy = (seed?.PlayerCommandOnly ?? PlayerCommandOnlyIds.Contains(id))
                        ? OptionInvocationPolicy.PlayerCommandOnly
                        : OptionInvocationPolicy.PolicyOrAutonomous,
                    ReadTrainingGate = readGate,
                    CandidateTrainingGate = candidateGate,
                    CompilerTrainingGate = compilerGate,
                    RuntimeTrainingGate = runtimeGate,
                    OutputTrainingGate = outputGate,
                    ReadEvidenceIds = evidence.ReadEvidenceIds,
                    CandidateEvidenceIds = evidence.CandidateEvidenceIds,
                    CompilerEvidenceIds = evidence.CompilerEvidenceIds,
                    RuntimeEvidenceIds = evidence.RuntimeEvidenceIds,
                    OutputEvidenceIds = evidence.OutputEvidenceIds,
                    TrainingExclusionReasons = exclusions,
                    TrainingEvidenceScope = trainingEligible ? evidence.Scope : "not_admitted"
                };
            }).ToArray();

            ValidateSource(rows);
            return new ReadOnlyCollection<OptionCapabilityDeclaration>(rows);
        }

        private static void ValidateSource(IReadOnlyCollection<OptionCapabilityDeclaration> rows)
        {
            var registered = rows.Select(row => row.OptionId).ToHashSet(StringComparer.Ordinal);
            if (registered.Count != rows.Count)
            {
                throw new InvalidOperationException("Capability registry contains duplicate option IDs.");
            }

            var dangling = StepCompilerIds
                .Concat(ParameterCompilerIds)
                .Concat(HarnessDispatchIds)
                .Concat(InternalHighLevelExecutionIds)
                .Concat(AutonomousCandidateIds)
                .Concat(PlayerConfirmationIds)
                .Concat(PlayerCommandOnlyIds)
                .Concat(HostOnlyIds)
                .Concat(CalibrationOnlyHighLevelIds)
                .Concat(TrainingEvidenceByOptionId.Keys)
                .Where(id => !registered.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (dangling.Length > 0)
            {
                throw new InvalidOperationException(
                    "Capability registry contains dangling IDs: " + string.Join(",", dangling));
            }
        }

        private static HashSet<string> Set(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.Ordinal);
        }

        private static CapabilitySeed NativeObjectSeed(
            bool autonomous,
            bool playerCommandOnly,
            string scope,
            params string[] evidenceIds) =>
            new CapabilitySeed(
                stepCompiler: true,
                parameterCompiler: true,
                harnessDispatch: true,
                internalExecution: true,
                autonomousCandidate: autonomous,
                playerConfirmation: playerCommandOnly,
                playerCommandOnly: playerCommandOnly,
                calibrationOnly: false,
                evidence: VerifiedEvidence(scope, evidenceIds));

        private static CapabilitySeed NativeObjectCalibrationSeed(
            string scope,
            params string[] evidenceIds) =>
            new CapabilitySeed(
                stepCompiler: true,
                parameterCompiler: true,
                harnessDispatch: true,
                internalExecution: true,
                autonomousCandidate: true,
                playerConfirmation: false,
                playerCommandOnly: false,
                calibrationOnly: true,
                evidence: VerifiedEvidence(scope, evidenceIds));

        private static TrainingEvidence VerifiedEvidence(string scope, params string[] evidenceIds)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new InvalidOperationException("Verified training evidence requires a bounded scope.");
            }

            var ids = evidenceIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (ids.Length == 0)
            {
                throw new InvalidOperationException("Verified training evidence requires at least one evidence ID.");
            }

            return BoundedEvidence(scope, ids, ids, ids, ids, ids);
        }

        private static TrainingEvidence BoundedEvidence(
            string scope,
            string[]? readEvidenceIds = null,
            string[]? candidateEvidenceIds = null,
            string[]? compilerEvidenceIds = null,
            string[]? runtimeEvidenceIds = null,
            string[]? outputEvidenceIds = null)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new InvalidOperationException("Bounded evidence requires a nonempty scope.");
            }

            return new TrainingEvidence
            {
                Scope = scope,
                ReadEvidenceIds = NormalizeEvidenceIds(readEvidenceIds),
                CandidateEvidenceIds = NormalizeEvidenceIds(candidateEvidenceIds),
                CompilerEvidenceIds = NormalizeEvidenceIds(compilerEvidenceIds),
                RuntimeEvidenceIds = NormalizeEvidenceIds(runtimeEvidenceIds),
                OutputEvidenceIds = NormalizeEvidenceIds(outputEvidenceIds)
            };
        }

        private static string[] NormalizeEvidenceIds(IEnumerable<string>? evidenceIds)
        {
            return (evidenceIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static TrainingEvidenceGateStatus Gate(string[] evidenceIds, bool declared)
        {
            return evidenceIds.Length > 0
                ? TrainingEvidenceGateStatus.RuntimeVerified
                : declared
                    ? TrainingEvidenceGateStatus.DeclaredOnly
                    : TrainingEvidenceGateStatus.Missing;
        }

        private static TrainingAdmissionExclusionReason[] BuildTrainingExclusionReasons(
            bool policyTrainingCandidate,
            bool playerConfirmationRequired,
            bool playerCommandOnly,
            TrainingEvidenceGateStatus readGate,
            TrainingEvidenceGateStatus candidateGate,
            TrainingEvidenceGateStatus compilerGate,
            TrainingEvidenceGateStatus runtimeGate,
            TrainingEvidenceGateStatus outputGate)
        {
            var reasons = new List<TrainingAdmissionExclusionReason>();
            if (!policyTrainingCandidate)
                reasons.Add(TrainingAdmissionExclusionReason.NotPolicyTrainingOption);
            if (readGate != TrainingEvidenceGateStatus.RuntimeVerified)
                reasons.Add(TrainingAdmissionExclusionReason.ReadEvidenceMissing);
            if (candidateGate != TrainingEvidenceGateStatus.RuntimeVerified)
                reasons.Add(TrainingAdmissionExclusionReason.CandidateEvidenceMissing);
            if (compilerGate != TrainingEvidenceGateStatus.RuntimeVerified)
                reasons.Add(TrainingAdmissionExclusionReason.CompilerEvidenceMissing);
            if (runtimeGate != TrainingEvidenceGateStatus.RuntimeVerified)
                reasons.Add(TrainingAdmissionExclusionReason.RuntimeEvidenceMissing);
            if (outputGate != TrainingEvidenceGateStatus.RuntimeVerified)
                reasons.Add(TrainingAdmissionExclusionReason.OutputEvidenceMissing);
            if (playerConfirmationRequired)
                reasons.Add(TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired);
            if (playerCommandOnly)
                reasons.Add(TrainingAdmissionExclusionReason.PlayerCommandOnly);
            return reasons.Distinct().ToArray();
        }

        private static DailyCandidateCapabilityDeclaration SupportedCandidate(string kind)
        {
            return new DailyCandidateCapabilityDeclaration { Kind = kind, Compilable = true };
        }

        private static DailyCandidateCapabilityDeclaration BlockedCandidate(string kind, string reason)
        {
            return new DailyCandidateCapabilityDeclaration
            {
                Kind = kind,
                Compilable = false,
                BlockReason = reason
            };
        }
    }

    public static class TrainingEligibilityPolicy
    {
        public static bool IsEligible(OptionCapabilityDeclaration declaration)
        {
            return declaration.RuntimeEvidenceStatus >= OptionRuntimeStatus.RuntimeVerified &&
                declaration.TrainingEligibility == OptionTrainingEligibility.Eligible &&
                declaration.PolicyTrainingCandidate &&
                declaration.InvocationPolicy != OptionInvocationPolicy.PlayerCommandOnly &&
                !declaration.PlayerConfirmationRequired &&
                declaration.ReadTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                declaration.CandidateTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                declaration.CompilerTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                declaration.RuntimeTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                declaration.OutputTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                !string.IsNullOrWhiteSpace(declaration.TrainingEvidenceScope) &&
                !string.Equals(declaration.TrainingEvidenceScope, "not_admitted", StringComparison.Ordinal);
        }

        public static bool IsEligible(
            OptionRuntimeStatus runtimeEvidenceStatus,
            OptionTrainingEligibility declaredEligibility,
            bool autonomousCandidateEnabled,
            bool playerConfirmationRequired)
        {
            return runtimeEvidenceStatus >= OptionRuntimeStatus.RuntimeVerified &&
                declaredEligibility == OptionTrainingEligibility.Eligible &&
                autonomousCandidateEnabled &&
                !playerConfirmationRequired;
        }
    }

    public static class RuntimeTestHarnessDispatchCatalog
    {
        public static IReadOnlyCollection<string> OptionIds { get; } =
            new ReadOnlyCollection<string>(OptionCapabilityRegistrySource.All
                .Where(row => row.HarnessDispatchSupported)
                .Select(row => row.OptionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        public static bool IsSupported(string optionId)
        {
            return OptionCapabilityRegistrySource.TryGet(optionId, out var row) &&
                row.HarnessDispatchSupported;
        }
    }

    public static class ProductExecutorCapabilityCatalog
    {
        public static IReadOnlyCollection<string> OptionIds { get; } =
            new ReadOnlyCollection<string>(OptionCapabilityRegistrySource.All
                .Where(row => row.ProductExecutorSupported)
                .Select(row => row.OptionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        public static bool IsSupported(string optionId)
        {
            return OptionCapabilityRegistrySource.TryGet(optionId, out var row) &&
                row.ProductExecutorSupported;
        }
    }
}
