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
        ExplicitPlayerConfirmationRequired
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
        public const string SchemaVersion = "capability_registry.v2";

        private sealed class TrainingEvidence
        {
            public string Scope { get; set; } = string.Empty;
            public string[] ReadEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] CandidateEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] CompilerEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] RuntimeEvidenceIds { get; set; } = Array.Empty<string>();
            public string[] OutputEvidenceIds { get; set; } = Array.Empty<string>();
        }

        private static readonly HashSet<string> StepCompilerIds = Set(
            "farm.maintain_crops", "farm.process_machines", "foraging.harvest_bushes", "foraging.harvest_ginger", "mining.claim_reward_chests", "skills.read_books", "recovery.stabilize_day",
            "executor.move_to_tile", "executor.traverse_connector", "executor.face_direction",
            "executor.interact", "executor.buy_shop_item", "executor.sell_shop_item",
            "executor.choose_dialogue_response", "executor.sleep", "executor.wait_ticks",
            "executor.clear_obstacle", "executor.break_farm_resource_clump",
            "executor.break_current_location_resource_clump", "executor.till_soil",
            "executor.plant_seed", "executor.harvest_crop", "executor.harvest_giant_crop",
            "executor.pickup_debris", "executor.collect_spawned_object", "executor.harvest_ginger",
            "executor.harvest_bush", "executor.claim_mine_reward_chest", "executor.collect_crab_pot",
            "executor.collect_fish_pond_output", "executor.complete_fish_pond_request",
            "executor.collect_animal_product", "executor.pet_interact", "executor.fill_pet_bowl",
            "executor.donate_museum_item", "executor.donate_community_center_item",
            "executor.purchase_joja_membership", "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade", "executor.pan_ore_spot",
            "executor.collect_machine_output", "executor.load_machine_input",
            "executor.name_hatched_animal",
            "executor.craft_machine_item", "executor.craft_storage_item", "executor.place_machine", "executor.remove_machine", "executor.place_storage",
            "executor.read_book", "executor.catch_fish",
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
            "volcano.reach_caldera", "recovery.stabilize_day", "executor.buy_shop_item",
            "social.talk_npc", "social.gift_npc", "farm.maintain_crops",
            "inventory.transfer_item", "executor.transfer_material");

        private static readonly HashSet<string> HarnessDispatchIds = Set(
            "farm.maintain_crops", "executor.move_to_tile", "executor.traverse_connector",
            "executor.face_direction", "executor.interact", "executor.buy_shop_item",
            "executor.sell_shop_item", "executor.choose_dialogue_response", "executor.sleep",
            "executor.wait_ticks", "executor.clear_obstacle", "executor.break_farm_resource_clump",
            "executor.break_current_location_resource_clump", "executor.till_soil",
            "executor.plant_seed", "executor.harvest_crop", "executor.harvest_giant_crop",
            "executor.pickup_debris", "executor.collect_spawned_object", "executor.harvest_ginger",
            "executor.harvest_bush", "executor.claim_mine_reward_chest", "executor.collect_crab_pot",
            "executor.collect_fish_pond_output", "executor.complete_fish_pond_request",
            "executor.collect_animal_product", "executor.pet_interact", "executor.fill_pet_bowl",
            "executor.donate_museum_item", "executor.donate_community_center_item",
            "executor.purchase_joja_membership", "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade", "executor.pan_ore_spot",
            "executor.collect_machine_output", "executor.load_machine_input",
            "executor.name_hatched_animal",
            "executor.craft_machine_item", "executor.craft_storage_item", "executor.place_machine", "executor.remove_machine", "executor.place_storage",
            "executor.read_book", "executor.catch_fish",
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
            "recovery.stabilize_day", "farm.maintain_crops", "farm.process_machines",
            "farm.collect_animal_products", "farm.care_for_pets", "skills.read_books",
            "fishing.catch_fish", "fishing.collect_crab_pots", "fishing.service_fish_ponds",
            "foraging.collect_spawned_objects", "foraging.harvest_ginger",
            "foraging.harvest_bushes", "foraging.clear_green_rain_bushes",
            "foraging.pan_ore_spot", "mining.reach_depth", "mining.obtain_skull_key",
            "mining.claim_reward_chests", "mining.acquire_golden_scythe",
            "volcano.reach_caldera", "economy.buy_supplies", "economy.sell_items",
            "exploration.visit_location", "inventory.transfer_item");

        private static readonly HashSet<string> AutonomousCandidateIds = Set(
            "farm.maintain_crops", "farm.collect_animal_products", "farm.care_for_pets",
            "strategy.grandpa_progress", "exploration.visit_location", "fishing.collect_crab_pots",
            "foraging.collect_spawned_objects", "foraging.harvest_ginger",
            "foraging.harvest_bushes", "foraging.clear_green_rain_bushes",
            "foraging.pan_ore_spot", "mining.claim_reward_chests", "recovery.stabilize_day",
            "executor.move_to_tile", "executor.traverse_connector", "executor.face_direction",
            "executor.interact", "executor.close_menu", "executor.wait_ticks",
            "executor.claim_mine_reward_chest", "executor.mine_stone", "executor.break_container",
            "executor.break_resource_clump", "executor.combat_monster", "executor.descend_ladder",
            "executor.exit_mine", "executor.cool_volcano_lava", "executor.break_volcano_stone",
            "executor.break_volcano_container", "executor.combat_volcano_monster",
            "executor.clear_obstacle", "executor.break_farm_resource_clump",
            "executor.break_current_location_resource_clump", "executor.till_soil",
            "executor.harvest_crop", "executor.harvest_giant_crop", "executor.pickup_debris",
            "executor.collect_spawned_object", "executor.harvest_ginger", "executor.harvest_bush",
            "executor.collect_crab_pot", "executor.collect_fish_pond_output",
            "executor.collect_animal_product", "executor.pet_interact", "executor.fill_pet_bowl",
            "executor.pan_ore_spot", "executor.collect_machine_output",
            "executor.name_hatched_animal", "executor.select_safe_item_slot",
            "executor.transfer_material");

        private static readonly HashSet<string> PlayerConfirmationIds = Set(
            "museum.donate_items", "community_center.donate_bundle_items",
            "joja.advance_development", "mining.acquire_golden_scythe",
            "executor.choose_dialogue_response", "executor.donate_museum_item",
            "executor.donate_community_center_item", "executor.purchase_joja_membership",
            "executor.purchase_joja_project");

        private static readonly HashSet<string> HostOnlyIds = Set(
            "joja.advance_development", "housing.advance_farmhouse",
            "executor.purchase_joja_membership", "executor.purchase_joja_project",
            "executor.purchase_farmhouse_upgrade");

        private static readonly string[] RegisteredOptionIds =
        {
            "farm.maintain_crops", "farm.process_machines", "farm.collect_animal_products",
            "farm.care_for_pets", "museum.donate_items",
            "community_center.donate_bundle_items", "joja.advance_development",
            "housing.advance_farmhouse", "skills.read_books", "economy.buy_supplies",
            "economy.sell_items", "economy.ship_items", "inventory.transfer_item", "social.talk_npc", "social.gift_npc",
            "quest.advance", "strategy.grandpa_progress", "exploration.visit_location",
            "fishing.catch_fish", "fishing.collect_crab_pots", "fishing.service_fish_ponds",
            "foraging.collect_spawned_objects", "foraging.harvest_ginger",
            "foraging.harvest_bushes", "foraging.clear_green_rain_bushes",
            "foraging.pan_ore_spot", "mining.reach_depth", "mining.obtain_skull_key",
            "mining.claim_reward_chests", "mining.acquire_golden_scythe",
            "volcano.reach_caldera", "recovery.stabilize_day",
            "executor.move_to_tile", "executor.traverse_connector", "executor.face_direction",
            "executor.interact", "executor.buy_shop_item", "executor.sell_shop_item",
            "executor.choose_dialogue_response", "executor.sleep", "executor.close_menu",
            "executor.wait_ticks", "executor.claim_mine_reward_chest", "executor.mine_stone",
            "executor.break_container", "executor.break_resource_clump",
            "executor.combat_monster", "executor.shoot_monster", "executor.place_bomb",
            "executor.place_staircase",
            "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft",
            "executor.exit_mine", "executor.cool_volcano_lava", "executor.break_volcano_stone",
            "executor.break_volcano_container", "executor.combat_volcano_monster",
            "executor.catch_fish", "executor.ship_inventory_item_to_bin",
            "executor.transfer_material",
            "executor.social_interact", "executor.quest_npc_interact",
            "executor.quest_drop_box_donate", "executor.clear_obstacle",
            "executor.break_farm_resource_clump", "executor.break_current_location_resource_clump",
            "executor.plant_seed", "executor.till_soil", "executor.harvest_crop",
            "executor.harvest_giant_crop", "executor.pickup_debris",
            "executor.collect_spawned_object", "executor.harvest_ginger", "executor.harvest_bush",
            "executor.collect_crab_pot", "executor.collect_fish_pond_output",
            "executor.complete_fish_pond_request", "executor.collect_animal_product",
            "executor.pet_interact", "executor.fill_pet_bowl", "executor.donate_museum_item",
            "executor.donate_community_center_item", "executor.purchase_joja_membership",
            "executor.purchase_joja_project", "executor.purchase_farmhouse_upgrade",
            "executor.pan_ore_spot", "executor.collect_machine_output",
            "executor.load_machine_input", "executor.name_hatched_animal",
            "executor.craft_machine_item", "executor.craft_storage_item",
            "executor.place_machine", "executor.remove_machine", "executor.place_storage",
            "executor.read_book", "executor.select_safe_item_slot"
        };

        private static readonly HashSet<string> CalibrationOnlyHighLevelIds = Set(
            "farm.maintain_crops",
            "farm.process_machines",
            "recovery.stabilize_day");

        private static readonly IReadOnlyDictionary<string, TrainingEvidence> TrainingEvidenceByOptionId =
            new ReadOnlyDictionary<string, TrainingEvidence>(
                new Dictionary<string, TrainingEvidence>(StringComparer.Ordinal)
                {
                    ["inventory.transfer_item"] = VerifiedEvidence(
                        "explicit_bidirectional_player_normal_chest_transfer",
                        "EVD-192"),
                    ["foraging.harvest_ginger"] = VerifiedEvidence(
                        "vanilla_current_location_exact_ginger_dry_standard_rain_efficient_full_inventory_debris_energy_xp_matrix",
                        "EVD-119"),
                    ["foraging.harvest_bushes"] = VerifiedEvidence(
                        "vanilla_current_location_exact_bush_berry_standard_botanist_tea_leaf_golden_walnut_collected_walnut_and_cooldown_matrix",
                        "EVD-120"),
                    ["mining.claim_reward_chests"] = VerifiedEvidence(
                        "loaded_vanilla_mineshaft_exact_reward_chests_fixed_stardrop_forced_random_receipt_and_cleanup_matrix",
                        "EVD-122"),
                    ["mining.reach_depth"] = VerifiedEvidence(
                        "candidate_bound_ordinary_mine_rolling_current_floor_supported_steps",
                        "EVD-095"),
                    ["mining.obtain_skull_key"] = VerifiedEvidence(
                        "ordinary_mines_floor_119_to_120_native_skull_key_chest_claim_false_to_true_and_exit",
                        "EVD-106"),
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
                    ["social.talk_npc"] = VerifiedEvidence(
                        "vanilla_current_loaded_npc_talk_same_map_or_rolling_resolved_route_with_safe_dialogue_close",
                        "EVD-076",
                        "EVD-105"),
                    ["skills.read_books"] = VerifiedEvidence(
                        "all_six_vanilla_base_book_branch_families_exact_projection_native_use_and_durable_output",
                        "EVD-124"),
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
                SupportedCandidate("buy_shop_item"), SupportedCandidate("catch_fish"),
                SupportedCandidate("claim_mine_reward_chest"),
                SupportedCandidate("clear_farm_resource_clump"),
                SupportedCandidate("clear_green_rain_resource_clump"),
                SupportedCandidate("clear_obstacle_tile"), SupportedCandidate("collect_animal_product"),
                SupportedCandidate("collect_crab_pot"), SupportedCandidate("collect_fish_pond_output"),
                SupportedCandidate("collect_machine_output_tile"),
                SupportedCandidate("collect_spawned_object"),
                SupportedCandidate("complete_fish_pond_request"),
                SupportedCandidate("craft_machine_item"),
                SupportedCandidate("craft_storage_item"),
                SupportedCandidate("place_machine_item"),
                SupportedCandidate("relocate_machine_item"),
                SupportedCandidate("place_storage_item"),
                SupportedCandidate("donate_community_center_item"),
                SupportedCandidate("donate_museum_item"), SupportedCandidate("fill_pet_bowl"),
                SupportedCandidate("harvest_bush"), SupportedCandidate("harvest_crop_tile"),
                SupportedCandidate("harvest_giant_crop_tile"), SupportedCandidate("harvest_ginger"),
                SupportedCandidate("interact_endpoint"), SupportedCandidate("load_machine_input_tile"),
                SupportedCandidate("name_hatched_animal"),
                SupportedCandidate("mining_acquire_golden_scythe_plan_envelope"),
                SupportedCandidate("mining_collect_quest_resource_plan_envelope"),
                SupportedCandidate("mining_obtain_skull_key_plan_envelope"),
                SupportedCandidate("mining_reach_depth_plan_envelope"),
                SupportedCandidate("mining_slay_monsters_plan_envelope"), SupportedCandidate("pan_ore_spot"),
                SupportedCandidate("pet_daily_interaction"), SupportedCandidate("pickup_debris_item"),
                SupportedCandidate("plant_seed_tile"), SupportedCandidate("purchase_farmhouse_expansion"),
                SupportedCandidate("purchase_farmhouse_upgrade"),
                SupportedCandidate("purchase_joja_membership"),
                SupportedCandidate("purchase_joja_project"), SupportedCandidate("read_inventory_book"),
                SupportedCandidate("recovery_close_menu"), SupportedCandidate("recovery_refresh_plan"),
                SupportedCandidate("recovery_resume_sleep_prompt"),
                SupportedCandidate("recovery_return_home"),
                SupportedCandidate("recovery_sleep_before_collapse"),
                SupportedCandidate("recovery_sleep_immediately"), SupportedCandidate("route_connector_tile"),
                SupportedCandidate("quest_drop_box_donation"),
                SupportedCandidate("quest_npc_interaction"),
                SupportedCandidate("ship_inventory_item_to_bin"),
                SupportedCandidate("transfer_inventory_item"),
                SupportedCandidate("social_continuation_retry_wait"),
                SupportedCandidate("social_gift_current"), SupportedCandidate("social_talk_current"),
                SupportedCandidate("volcano_reach_caldera_plan_envelope"),
                SupportedCandidate("water_crop_tile"), SupportedCandidate("sell_shop_item"),
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
            var rows = RegisteredOptionIds.Select(id =>
            {
                var hasStep = StepCompilerIds.Contains(id);
                var hasParameter = ParameterCompilerIds.Contains(id);
                var candidateStatus = id.StartsWith("executor.", StringComparison.Ordinal)
                    ? CapabilityCandidateStatus.NotApplicable
                    : id == "quest.advance"
                        ? CapabilityCandidateStatus.PartiallyBlocked
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
                    !CalibrationOnlyHighLevelIds.Contains(id);
                TrainingEvidenceByOptionId.TryGetValue(id, out var evidence);
                evidence ??= new TrainingEvidence();
                var readGate = Gate(evidence.ReadEvidenceIds, declared: true);
                var candidateGate = Gate(
                    evidence.CandidateEvidenceIds,
                    candidateStatus == CapabilityCandidateStatus.Declared);
                var compilerGate = Gate(
                    evidence.CompilerEvidenceIds,
                    compilerStatus != CapabilityCompilerStatus.Unbound &&
                    (HarnessDispatchIds.Contains(id) || InternalHighLevelExecutionIds.Contains(id)));
                var runtimeGate = Gate(evidence.RuntimeEvidenceIds, declared: false);
                var outputGate = Gate(evidence.OutputEvidenceIds, declared: false);
                var exclusions = BuildTrainingExclusionReasons(
                    policyTrainingCandidate,
                    PlayerConfirmationIds.Contains(id),
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
                    HarnessDispatchSupported = HarnessDispatchIds.Contains(id),
                    ProductExecutorSupported = false,
                    InternalExecutionPipelineSupported =
                        HarnessDispatchIds.Contains(id) || InternalHighLevelExecutionIds.Contains(id),
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
                    AutonomousCandidateEnabled = AutonomousCandidateIds.Contains(id),
                    PlayerConfirmationRequired = PlayerConfirmationIds.Contains(id),
                    HostOnly = HostOnlyIds.Contains(id),
                    ProductIntegrationStatus = CapabilityProductIntegrationStatus.NotIntegrated,
                    PolicyTrainingCandidate = policyTrainingCandidate,
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
