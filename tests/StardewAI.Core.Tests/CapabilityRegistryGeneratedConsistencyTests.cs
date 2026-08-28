using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CapabilityRegistryGeneratedConsistencyTests
{
    [Fact]
    public void CapabilityCatalogGeneratedConsistencyTests()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        Assert.Equal(
            OptionCapabilityRegistrySource.All.Select(row => row.OptionId).OrderBy(id => id, StringComparer.Ordinal),
            registry.All.Select(row => row.OptionId).OrderBy(id => id, StringComparer.Ordinal));

        foreach (var option in registry.All)
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(option.OptionId);
            Assert.Equal(declaration.RegistrationStatus, option.RegistrationStatus);
            Assert.Equal(declaration.ReadStatus, option.ReadStatus);
            Assert.Equal(declaration.CandidateStatus, option.CandidateStatus);
            Assert.Equal(declaration.CompilerStatus, option.CompilerStatus);
            Assert.Equal(declaration.HarnessDispatchSupported, option.HarnessDispatchSupported);
            Assert.Equal(declaration.ProductExecutorSupported, option.ProductExecutorSupported);
            Assert.Equal(declaration.RuntimeEvidenceStatus, option.RuntimeStatus);
            Assert.Equal(declaration.TrainingEligibility, option.TrainingEligibility);
            Assert.Equal(declaration.PolicyTrainingCandidate, option.PolicyTrainingCandidate);
            Assert.Equal(declaration.InvocationPolicy, option.InvocationPolicy);
            Assert.Equal(declaration.ReadTrainingGate, option.ReadTrainingGate);
            Assert.Equal(declaration.CandidateTrainingGate, option.CandidateTrainingGate);
            Assert.Equal(declaration.CompilerTrainingGate, option.CompilerTrainingGate);
            Assert.Equal(declaration.RuntimeTrainingGate, option.RuntimeTrainingGate);
            Assert.Equal(declaration.OutputTrainingGate, option.OutputTrainingGate);
            Assert.Equal(declaration.ReadEvidenceIds, option.ReadEvidenceIds);
            Assert.Equal(declaration.CandidateEvidenceIds, option.CandidateEvidenceIds);
            Assert.Equal(declaration.CompilerEvidenceIds, option.CompilerEvidenceIds);
            Assert.Equal(declaration.RuntimeEvidenceIds, option.RuntimeEvidenceIds);
            Assert.Equal(declaration.OutputEvidenceIds, option.OutputEvidenceIds);
            Assert.Equal(declaration.TrainingExclusionReasons, option.TrainingExclusionReasons);
            Assert.Equal(declaration.TrainingEvidenceScope, option.TrainingEvidenceScope);
            Assert.Equal(
                option.TrainingRole != TrainingRoles.ExecutorCalibration &&
                option.TrainingRole != TrainingRoles.PlayerCommandOnly,
                declaration.PolicyTrainingCandidate);
        }
    }

    [Fact]
    public void HarnessSupportDoesNotImplyRuntimeEvidenceTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("executor.interact");

        Assert.True(declaration.HarnessDispatchSupported);
        Assert.False(declaration.ProductExecutorSupported);
        Assert.Equal(OptionRuntimeStatus.RegisteredOnly, declaration.RuntimeEvidenceStatus);
        Assert.Equal(
            OptionTrainingEligibility.BlockedPendingRuntimeEvidence,
            declaration.TrainingEligibility);
        Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void ProductSupportDoesNotImplyTrainingEligibilityTests()
    {
        const bool productExecutorSupported = true;

        Assert.True(productExecutorSupported);
        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RegisteredOnly,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: true,
            playerConfirmationRequired: false));
    }

    [Fact]
    public void EveryFullActionHasStepCompilerTests()
    {
        var missing = new StardewAI.Core.OptionRegistry.OptionRegistry().All
            .Where(row => row.CompilerResponsibility == CompilerResponsibilities.FullActionExpansion)
            .Where(row =>
                !ActionQueueCompiler.HasStepCompiler(row.OptionId) &&
                !DailyPlanCompiler.HasOptionCompiler(row.OptionId))
            .Select(row => row.OptionId);

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryCompiledExecutorHasDeclaredDispatchStatusTests()
    {
        foreach (var optionId in ActionQueueCompiler.StepCompilerOptionIds
            .Where(id => id.StartsWith("executor.", StringComparison.Ordinal)))
        {
            Assert.True(OptionCapabilityRegistrySource.TryGet(optionId, out var declaration));
            Assert.Equal(
                RuntimeTestHarnessDispatchCatalog.IsSupported(optionId),
                declaration.HarnessDispatchSupported);
            Assert.Equal(
                ProductExecutorCapabilityCatalog.IsSupported(optionId),
                declaration.ProductExecutorSupported);
        }
    }

    [Fact]
    public void EveryLiteralCandidateKindIsClassifiedTests()
    {
        var optionRegistryRoot = Path.Combine(FindRepositoryRoot(), "src", "StardewAI.Core", "OptionRegistry");
        var generatedKinds = Directory
            .EnumerateFiles(optionRegistryRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "Kind = \"(?<kind>[^\"]+)\"",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["kind"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var classifiedKinds = OptionCapabilityRegistrySource.DailyCandidates
            .Select(row => row.Kind)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(generatedKinds.Except(classifiedKinds, StringComparer.Ordinal));
        Assert.Equal(
            classifiedKinds.OrderBy(value => value, StringComparer.Ordinal),
            DailyPlanCandidateCapabilityCatalog.All
                .Select(row => row.Kind)
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void UnknownRuntimeOptionFailsClosedTests()
    {
        Assert.False(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.unknown"));
        Assert.False(ProductExecutorCapabilityCatalog.IsSupported("executor.unknown"));
        Assert.False(OptionCapabilityRegistrySource.TryGet("executor.unknown", out _));

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        Assert.Contains("runtime_executor_option_not_supported:", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "pending.Completion.SetResult(ExecuteMaintainCropsNoOp(pending.Request));",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingAllowlistRequiresRuntimeEvidenceTests()
    {
        Assert.NotEmpty(OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(
            new[]
            {
                "animals.manage_animal",
                "animals.purchase",
                "animals.withdraw_feed_hopper_hay",
                "buildings.construct",
                "crafting.cook_recipe",
                "crafting.forge_item",
                "economy.buy_supplies",
                "economy.sell_items",
                "economy.ship_items",
                "exploration.visit_location",
                "farm.care_for_pets",
                "farm.collect_animal_products",
                "farm.collect_machine_outputs",
                "farm.establish_supported_machine_capacity",
                "farm.fulfill_machine_task_demand",
                "farm.load_supported_machine_input",
                "farming.collect_slime_ball",
                "fishing.catch_fish", "fishing.collect_crab_pots", "fishing.service_fish_ponds", "foraging.clear_green_rain_bushes", "foraging.collect_spawned_objects", "foraging.harvest_bushes", "foraging.harvest_ginger", "foraging.pan_ore_spot",
                "inventory.transfer_item",
                "mail.process_letter",
                "mining.choose_dwarf_statue_power", "mining.claim_reward_chests", "mining.obtain_skull_key", "mining.reach_depth", "mining.use_elevator",
                "rewards.claim_pot_of_gold", "rewards.claim_statue_blessing",
                "skills.choose_profession", "skills.read_books", "social.gift_npc", "social.talk_npc", "volcano.reach_caldera"
            },
            OptionCapabilityRegistrySource.TrainingAllowlist);

        Assert.All(OptionCapabilityRegistrySource.TrainingAllowlist, optionId =>
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
            Assert.True(declaration.RuntimeEvidenceStatus >= OptionRuntimeStatus.RuntimeVerified);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
            Assert.NotEmpty(declaration.RuntimeEvidenceIds);
            Assert.NotEmpty(declaration.OutputEvidenceIds);
            Assert.Empty(declaration.TrainingExclusionReasons);
            var expectedScope = optionId switch
            {
                "animals.manage_animal" => "vanilla_exact_loaded_base_animal_explicit_rename_reproduction_toggle_move_home_or_irreversible_sale_through_native_pet_and_AnimalQueryMenu_with_strict_receipt",
                "animals.purchase" => "vanilla_exact_live_stock_compatible_home_money_name_and_native_PurchaseAnimalsMenu_terminal_receipt_with_source_verified_rolling_route_Marnie_service_and_multi_location_paging",
                "animals.withdraw_feed_hopper_hay" => "vanilla_exact_base_(BC)99_AnimalHouse_root_silo_animal_and_placed_hay_projection_native_location_action_exact_(O)178_inventory_transfer_conservation_identity_and_selected_slot_receipt",
                "buildings.change_skin" => "vanilla_actor_exact_live_Pet_Bowl_default_to_Stone_skin_current_Robin_service_native_CarpenterMenu_shortest_click_and_paint_reset_receipt",
                "buildings.construct" => "vanilla_host_purpose_bound_exact_live_base_blueprint_current_Robin_service_native_CarpenterMenu_Coop_on_Farm_money_material_placement_and_countdown_receipt",
                "buildings.paint" => "vanilla_actor_exact_live_Farmhouse_first_paint_region_current_Robin_service_native_CarpenterMenu_mouse_reachable_custom_HSL_and_unchanged_sibling_receipt",
                "crafting.cook_recipe" => "vanilla_exact_learned_recipe_explicit_purpose_native_kitchen_or_cookout_source_material_and_qi_seasoning_consumption_output_quality_recipesCooked_quest_and_achievement_callback_receipt",
                "crafting.forge_item" => "vanilla_exact_loaded_forge_action_or_MiniForge_all_live_inventory_and_equipped_ring_inputs_all_nine_ForgeMenu_operation_families_exact_or_complete_native_random_output_contract",
                "exploration.visit_location" => "vanilla_current_location_one_exact_resolved_cross_location_connector_or_one_exact_clearable_route_obstacle_then_fresh_snapshot",
                "economy.buy_supplies" => "vanilla_safe_single_money_purchase_rolling_resolved_route_exact_shop_endpoint_optional_whitelisted_dialogue_native_buy_and_menu_cleanup",
                "economy.sell_items" => "vanilla_one_explicitly_authorized_unprotected_positive_value_stack_rolling_resolved_route_exact_shop_endpoint_optional_whitelisted_dialogue_native_sale_and_background_safe_menu_cleanup",
                "economy.ship_items" => "vanilla_one_explicitly_authorized_unprotected_positive_shipping_payout_item_rolling_resolved_route_exact_bin_approach_native_single_item_deposit_immediate_inventory_bin_receipt_and_delayed_day_settlement",
                "farm.collect_animal_products" => "vanilla_current_location_exact_ready_base_farm_animal_milk_pail_shears_cracker_single_double_native_inventory_receipt_stats_farming_xp_energy_and_friendship",
                "farm.care_for_pets" => "vanilla_current_location_exact_base_pet_native_checkAction_normal_and_max_friendship_gift_output_dynamic_bounding_box_rebind_and_base_pet_bowl_watering_native_sleep_dayUpdate_durable_settlement",
                "farm.collect_machine_outputs" => "vanilla_current_location_exact_ready_non_incubator_machine_output_native_inventory_receipt_structured_skill_and_mastery",
                "farm.establish_supported_machine_capacity" => "vanilla_current_location_single_bounded_positive_machine_capacity_craft_exact_placement_binding_deterministic_input_load_processing_completion_and_training_rows_or_exact_ordinary_or_special_collection_task_capacity_craft_or_inventory_placement_zero_additional_consumption_natural_collect_receipt",
                "farm.fulfill_machine_task_demand" => "vanilla_current_location_existing_machine_exact_zero_additional_consumption_input_source_natural_processing_and_native_ordinary_or_special_collection_receipt",
                "farm.load_supported_machine_input" => "vanilla_current_location_exact_placement_bound_positive_deterministic_machine_support_input_no_additional_consumption_unreserved_native_load_and_processing_completion",
                "farming.collect_slime_ball" => "vanilla_exact_SlimeHutch_base_fragility_2_slime_ball_seeded_slime_and_petrified_slime_projection_native_location_action_object_removal_conserved_inventory_plus_debris_output_and_shared_pickup_handoff",
                "fishing.catch_fish" => "vanilla_current_or_resolved_route_exact_fishable_cast_native_max_power_stochastic_distribution_bobber_bar_or_special_no_minigame_receipt_and_idle_cleanup",
                "fishing.collect_crab_pots" => "vanilla_current_location_exact_ready_base_crab_pot_native_collect_book_double_inventory_receipt_fishing_xp_caught_fish_bait_and_ready_reset",
                "fishing.service_fish_ponds" => "vanilla_exact_completed_fish_pond_native_output_collect_and_authorized_population_request_inventory_fishing_xp_gate_and_reset_lifecycle",
                "foraging.clear_green_rain_bushes" => "vanilla_current_location_exact_base_green_rain_resource_clump_indexes_44_46_seeded_core_outputs_bounded_secret_note_native_axe_and_task_receipt",
                "foraging.collect_spawned_objects" => "vanilla_current_location_exact_base_spawned_object_ordinary_botanist_deterministic_gatherer_special_724519_and_farm_interior_native_pickup_matrix",
                "foraging.harvest_bushes" => "vanilla_current_location_exact_bush_berry_standard_botanist_tea_leaf_golden_walnut_collected_walnut_and_cooldown_matrix",
                "foraging.harvest_ginger" => "vanilla_current_location_exact_ginger_dry_standard_rain_efficient_full_inventory_debris_energy_xp_matrix",
                "foraging.pan_ore_spot" => "vanilla_current_location_exact_active_ore_spot_live_pan_reward_projection_copper_steel_lifecycle_receipt_xp_times_panned_and_respawn_observation",
                "inventory.transfer_item" => "explicit_bidirectional_player_normal_chest_transfer",
                "mail.process_letter" => "vanilla_native_order_owned_farm_mailbox_all_locked_Data_mail_directives_exact_LetterViewer_pages_attachments_quests_special_orders_stardrop_overflow_and_native_receipts",
                "mining.choose_dwarf_statue_power" => "vanilla_mining_mastery_exact_daily_two_offer_rng_all_five_power_projections_native_object_menu_click_and_selected_day_buff_receipt",
                "mining.claim_reward_chests" => "loaded_vanilla_mineshaft_exact_reward_chests_fixed_stardrop_forced_random_receipt_and_cleanup_matrix",
                "mining.obtain_skull_key" => "ordinary_mines_floor_119_to_120_native_skull_key_chest_claim_false_to_true_and_exit",
                "mining.reach_depth" => "candidate_bound_ordinary_mine_rolling_current_floor_supported_steps_and_unlocked_native_elevator_checkpoint_shortcut",
                "mining.use_elevator" => "vanilla_ordinary_mines_unlocked_checkpoint_exact_endpoint_native_MineElevatorMenu_selection_and_bidirectional_destination_receipt",
                "rewards.claim_pot_of_gold" => "vanilla_spring_17_Forest_exact_PotOfGold_native_checkAction_full_inventory_year_scaled_GoldCoin_and_LeprechuanHat_debris_conservation_and_shared_pickup_handoff",
                "rewards.claim_statue_blessing" => "vanilla_farming_mastery_exact_daily_rng_rain_festival_denominator_all_seven_effect_projections_native_object_action_and_day_buff_receipt",
                "skills.choose_profession" => "all_30_vanilla_professions_five_skills_level_5_and_both_level_10_branches_exact_live_menu_projection_shared_level_up_completion_persistent_profession_pending_level_menu_and_immediate_health_stamina_receipts",
                "skills.read_books" => "all_six_vanilla_base_book_branch_families_exact_projection_native_use_and_durable_output",
                "social.gift_npc" => "vanilla_current_loaded_npc_gift_same_map_or_rolling_resolved_route_with_single_item_consumed_to_null",
                "social.talk_npc" => "vanilla_current_loaded_npc_talk_same_map_or_rolling_resolved_route_with_safe_dialogue_close",
                "volcano.reach_caldera" => "vanilla_volcano_generated_levels_0_to_9_rolling_native_actions_typed_combat_intent_to_caldera",
                "world.rotate_house_plant" => "vanilla_all_eight_base_house_plant_visual_frames_empty_hand_native_location_object_interaction_double_call_edge_permanent_identity_and_selected_slot_receipt",
                _ => throw new InvalidOperationException("Unexpected training option: " + optionId)
            };
            Assert.Equal(expectedScope, declaration.TrainingEvidenceScope);
        });

        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RuntimeVerified,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: false,
            playerConfirmationRequired: false));
        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RuntimeVerified,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: true,
            playerConfirmationRequired: true));
    }

    [Fact]
    public void AnimalProductAdmissionRequiresExactNativeToolMatrixAndEvd222Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("farm.collect_animal_products");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("farm.collect_animal_products"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("farm.collect_animal_products"));
        Assert.Equal(new[] { "EVD-222" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-222" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-222" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-222" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-222" }, declaration.OutputEvidenceIds);
        Assert.Contains("milk_pail_shears", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("cracker_single_double", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
    }

    [Fact]
    public void CropMaintenanceClosesFiveGatesButRemainsExecutorCalibrationOnlyTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("farm.maintain_crops");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, declaration.TrainingEligibility);
        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("farm.maintain_crops"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("farm.maintain_crops"));
        Assert.Equal(new[] { "EVD-226" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-226" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-226" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-226" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-226" }, declaration.OutputEvidenceIds);
        Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
        Assert.Equal(new[] { TrainingAdmissionExclusionReason.NotPolicyTrainingOption }, declaration.TrainingExclusionReasons);
    }

    [Fact]
    public void MachineProcessingClosesBoundedAggregateFiveGatesButRemainsExecutorCalibrationOnlyTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("farm.process_machines");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, declaration.TrainingEligibility);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("farm.process_machines"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("farm.process_machines"));
        Assert.Equal(new[] { "EVD-227" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-227" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-227" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-227" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-227" }, declaration.OutputEvidenceIds);
        Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
        Assert.Equal(new[] { TrainingAdmissionExclusionReason.NotPolicyTrainingOption }, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain("farm.process_machines", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void FarmhouseUpgradeClosesFiveGatesButRetainsHostPlayerConfirmationTests()
    {
        var highLevel = OptionCapabilityRegistrySource.GetRequired("housing.advance_farmhouse");
        var primitive = OptionCapabilityRegistrySource.GetRequired("executor.purchase_farmhouse_upgrade");

        Assert.False(TrainingEligibilityPolicy.IsEligible(highLevel));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, highLevel.TrainingEligibility);
        Assert.True(highLevel.PlayerConfirmationRequired);
        Assert.True(highLevel.HostOnly);
        Assert.True(highLevel.InternalExecutionPipelineSupported);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, highLevel.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler(highLevel.OptionId));
        Assert.False(ActionQueueCompiler.HasStepCompiler(highLevel.OptionId));
        Assert.Equal(new[] { TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired }, highLevel.TrainingExclusionReasons);

        Assert.False(TrainingEligibilityPolicy.IsEligible(primitive));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, primitive.TrainingEligibility);
        Assert.True(primitive.PlayerConfirmationRequired);
        Assert.True(primitive.HostOnly);
        Assert.True(primitive.HarnessDispatchSupported);
        Assert.True(ActionQueueCompiler.HasStepCompiler(primitive.OptionId));
        Assert.Equal(
            new[] { TrainingAdmissionExclusionReason.NotPolicyTrainingOption, TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired },
            primitive.TrainingExclusionReasons);

        foreach (var declaration in new[] { highLevel, primitive })
        {
            Assert.Equal(new[] { "EVD-229" }, declaration.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-229" }, declaration.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-229" }, declaration.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-229" }, declaration.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-229" }, declaration.OutputEvidenceIds);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
            Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
            Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        }
    }

    [Fact]
    public void JojaDevelopmentClosesFiveGatesButRetainsHostPlayerConfirmationTests()
    {
        var highLevel = OptionCapabilityRegistrySource.GetRequired("joja.advance_development");
        var primitives = new[]
        {
            OptionCapabilityRegistrySource.GetRequired("executor.purchase_joja_membership"),
            OptionCapabilityRegistrySource.GetRequired("executor.purchase_joja_project")
        };

        Assert.True(highLevel.InternalExecutionPipelineSupported);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, highLevel.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler(highLevel.OptionId));
        Assert.False(ActionQueueCompiler.HasStepCompiler(highLevel.OptionId));

        foreach (var declaration in new[] { highLevel }.Concat(primitives))
        {
            Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
            Assert.Equal(OptionTrainingEligibility.EvaluationOnly, declaration.TrainingEligibility);
            Assert.True(declaration.PlayerConfirmationRequired);
            Assert.True(declaration.HostOnly);
            Assert.Equal(new[] { "EVD-232" }, declaration.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-232" }, declaration.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-232" }, declaration.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-232" }, declaration.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-232" }, declaration.OutputEvidenceIds);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
            Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
            Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        }

        foreach (var primitive in primitives)
        {
            Assert.True(primitive.HarnessDispatchSupported);
            Assert.True(ActionQueueCompiler.HasStepCompiler(primitive.OptionId));
            Assert.Equal(
                new[] { TrainingAdmissionExclusionReason.NotPolicyTrainingOption, TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired },
                primitive.TrainingExclusionReasons);
        }
        Assert.Equal(
            new[] { TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired },
            highLevel.TrainingExclusionReasons);
    }

    [Fact]
    public void PetCareAdmissionRequiresBothNativeBranchesAndEvd223Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("farm.care_for_pets");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("farm.care_for_pets"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("farm.care_for_pets"));
        Assert.Equal(new[] { "EVD-223" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-223" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-223" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-223" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-223" }, declaration.OutputEvidenceIds);
        Assert.Contains("max_friendship_gift_output", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("native_sleep_dayUpdate_durable_settlement", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
    }

    [Fact]
    public void MuseumDonationClosesFiveGatesButRetainsPlayerConfirmationTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("museum.donate_items");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, declaration.TrainingEligibility);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("museum.donate_items"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("museum.donate_items"));
        Assert.Equal(new[] { "EVD-224" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-224" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-224" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-224" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-224" }, declaration.OutputEvidenceIds);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
        Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
        Assert.DoesNotContain("museum.donate_items", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void CommunityCenterDonationClosesFiveGatesButRetainsPlayerConfirmationTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("community_center.donate_bundle_items");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, declaration.TrainingEligibility);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("community_center.donate_bundle_items"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("community_center.donate_bundle_items"));
        Assert.Equal(new[] { "EVD-225" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-225" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-225" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-225" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-225" }, declaration.OutputEvidenceIds);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
        Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
        Assert.DoesNotContain("community_center.donate_bundle_items", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void VisitLocationAdmissionRequiresBoundedRollingRouteEvidence()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "exploration.visit_location");

        Assert.Equal(OptionTrainingEligibility.Eligible, declaration.TrainingEligibility);
        Assert.Contains("EVD-218", declaration.CandidateEvidenceIds);
        Assert.Contains("EVD-218", declaration.CompilerEvidenceIds);
        Assert.Contains("EVD-218", declaration.RuntimeEvidenceIds);
        Assert.Contains("EVD-218", declaration.OutputEvidenceIds);
        Assert.Contains(
            "one_exact_resolved_cross_location_connector",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "exploration.visit_location",
            OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(
            "executor.traverse_connector",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void BuySuppliesAdmissionRequiresExactRollingPurchaseEvidence()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "economy.buy_supplies");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(
            CapabilityCompilerStatus.StepCompilerDeclared,
            declaration.CompilerStatus);
        Assert.Contains("EVD-219", declaration.ReadEvidenceIds);
        Assert.Contains("EVD-219", declaration.CandidateEvidenceIds);
        Assert.Contains("EVD-219", declaration.CompilerEvidenceIds);
        Assert.Contains("EVD-219", declaration.RuntimeEvidenceIds);
        Assert.Contains("EVD-219", declaration.OutputEvidenceIds);
        Assert.Contains(
            "safe_single_money_purchase",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "economy.buy_supplies",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void SellItemsAdmissionRequiresExactRollingSaleEvidence()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "economy.sell_items");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(
            CapabilityCompilerStatus.StepCompilerDeclared,
            declaration.CompilerStatus);
        Assert.Contains("EVD-220", declaration.ReadEvidenceIds);
        Assert.Contains("EVD-220", declaration.CandidateEvidenceIds);
        Assert.Contains("EVD-220", declaration.CompilerEvidenceIds);
        Assert.Contains("EVD-220", declaration.RuntimeEvidenceIds);
        Assert.Contains("EVD-220", declaration.OutputEvidenceIds);
        Assert.Contains(
            "explicitly_authorized_unprotected_positive_value_stack",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "economy.sell_items",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void ShipItemsAdmissionRequiresExactRollingDepositAndSettlementEvidence()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "economy.ship_items");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(
            CapabilityCompilerStatus.StepCompilerDeclared,
            declaration.CompilerStatus);
        Assert.Contains("EVD-221", declaration.ReadEvidenceIds);
        Assert.Contains("EVD-221", declaration.CandidateEvidenceIds);
        Assert.Contains("EVD-221", declaration.CompilerEvidenceIds);
        Assert.Contains("EVD-221", declaration.RuntimeEvidenceIds);
        Assert.Contains("EVD-221", declaration.OutputEvidenceIds);
        Assert.Contains(
            "native_single_item_deposit",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "economy.ship_items",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void MachineOutputAdmissionRequiresBoundedNativeMatrixAndEvd213Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("farm.collect_machine_outputs");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("farm.collect_machine_outputs"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("farm.collect_machine_outputs"));
        Assert.Equal(new[] { "EVD-213" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-213" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-213" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-213" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-213" }, declaration.OutputEvidenceIds);
        Assert.Contains("current_location_exact_ready_non_incubator", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("structured_skill_and_mastery", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.collect_machine_output", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("farm.process_machines", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void SupportedMachineInputAdmissionRequiresBoundedNativeEvd214Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "farm.load_supported_machine_input");

        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(
            CapabilityCompilerStatus.StepCompilerDeclared,
            declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler(
            "farm.load_supported_machine_input"));
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-214" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-214" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-214" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-214" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-214" }, declaration.OutputEvidenceIds);
        Assert.Contains(
            "exact_placement_bound_positive_deterministic",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "no_additional_consumption_unreserved",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "farm.load_supported_machine_input",
            OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(
            "farm.process_machines",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void MachineTaskDemandAdmissionRequiresBoundedNativeEvd216Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "farm.fulfill_machine_task_demand");

        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(
            CapabilityCompilerStatus.StepCompilerDeclared,
            declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler(
            "farm.fulfill_machine_task_demand"));
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-216" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-216" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-216" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-216" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-216" }, declaration.OutputEvidenceIds);
        Assert.Contains(
            "existing_machine_exact_zero_additional_consumption",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "ordinary_or_special_collection_receipt",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "quest.advance",
            OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(
            "farm.process_machines",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void SupportedMachineCapacityLifecycleAdmissionRequiresRollingNativeEvidence()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "farm.establish_supported_machine_capacity");

        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.Equal(
            CapabilityCompilerStatus.StepCompilerDeclared,
            declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler(
            "farm.establish_supported_machine_capacity"));
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-215", "EVD-217" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-215", "EVD-217" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-215", "EVD-217" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-215", "EVD-217" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-215", "EVD-217" }, declaration.OutputEvidenceIds);
        Assert.Contains(
            "craft_exact_placement_binding_deterministic_input_load",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "exact_ordinary_or_special_collection_task_capacity",
            declaration.TrainingEvidenceScope,
            StringComparison.Ordinal);
        Assert.Contains(
            "farm.establish_supported_machine_capacity",
            OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(
            "farm.process_machines",
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void GreenRainResourceClumpAdmissionRequiresExactNativeMatrixAndEvd212Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.clear_green_rain_bushes");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.clear_green_rain_bushes"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.clear_green_rain_bushes"));
        Assert.Equal(new[] { "EVD-212" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-212" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-212" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-212" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-212" }, declaration.OutputEvidenceIds);
        Assert.Contains("indexes_44_46", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("bounded_secret_note", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.break_current_location_resource_clump", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void SpawnedObjectAdmissionRequiresExactNativeMatrixAndEvd211Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.collect_spawned_objects");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.collect_spawned_objects"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.collect_spawned_objects"));
        Assert.Equal(new[] { "EVD-211" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-211" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-211" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-211" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-211" }, declaration.OutputEvidenceIds);
        Assert.Contains("ordinary_botanist_deterministic_gatherer", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("special_724519_and_farm_interior", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.collect_spawned_object", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void CrabPotAdmissionRequiresExactBaseNativeLifecycleAndEvd209Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("fishing.collect_crab_pots");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("fishing.collect_crab_pots"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("fishing.collect_crab_pots"));
        Assert.Equal(new[] { "EVD-209" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-209" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-209" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-209" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-209" }, declaration.OutputEvidenceIds);
        Assert.Contains("vanilla_current_location_exact_ready_base_crab_pot", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("book_double_inventory_receipt_fishing_xp_caught_fish", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("bait_and_ready_reset", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.collect_crab_pot", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void CatchFishAdmissionUsesOneDailyPlanChainAndEvd228Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("fishing.catch_fish");
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.catch_fish");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("fishing.catch_fish"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("fishing.catch_fish"));
        Assert.True(ActionQueueCompiler.HasStepCompiler("executor.catch_fish"));
        Assert.Equal(new[] { "EVD-228" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, declaration.OutputEvidenceIds);
        Assert.Contains("stochastic_distribution_bobber_bar_or_special_no_minigame", declaration.TrainingEvidenceScope, StringComparison.Ordinal);

        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, executor.TrainingEligibility);
        Assert.Equal(new[] { "EVD-228" }, executor.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, executor.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, executor.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, executor.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-228" }, executor.OutputEvidenceIds);
        Assert.DoesNotContain("executor.catch_fish", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void FishPondAdmissionRequiresBothNativeBranchesAndEvd210Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("fishing.service_fish_ponds");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("fishing.service_fish_ponds"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("fishing.service_fish_ponds"));
        Assert.Equal(new[] { "EVD-210" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-210" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-210" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-210" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-210" }, declaration.OutputEvidenceIds);
        Assert.Contains("native_output_collect", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("authorized_population_request", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("inventory_fishing_xp_gate_and_reset", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.collect_fish_pond_output", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("executor.complete_fish_pond_request", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void PanningAdmissionRequiresExactLiveProjectionAndEvd208Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.pan_ore_spot");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.pan_ore_spot"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.pan_ore_spot"));
        Assert.Equal(new[] { "EVD-208" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-208" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-208" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-208" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-208" }, declaration.OutputEvidenceIds);
        Assert.Contains("vanilla_current_location_exact_active_ore_spot", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("live_pan_reward_projection_copper_steel_lifecycle", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("receipt_xp_times_panned_and_respawn_observation", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("fixed_reward_table", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.pan_ore_spot", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void MineRewardChestAdmissionRequiresLoadedVanillaMatrixAndEvd122Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("mining.claim_reward_chests");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("mining.claim_reward_chests"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("mining.claim_reward_chests"));
        Assert.Equal(new[] { "EVD-122" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.OutputEvidenceIds);
        Assert.Contains("loaded_vanilla_mineshaft_exact_reward_chests", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("fixed_stardrop_forced_random", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("receipt_and_cleanup_matrix", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("skull_key", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("golden_scythe", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.claim_mine_reward_chest", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void GoldenScytheClosesFiveGatesButRemainsConfirmationOnlyTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("mining.acquire_golden_scythe");

        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, declaration.TrainingEligibility);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.ParameterCompilerDeclared, declaration.CompilerStatus);
        Assert.Equal(new[] { "EVD-231" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-231" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-231" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-231" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-231" }, declaration.OutputEvidenceIds);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
        Assert.Equal("not_admitted", declaration.TrainingEvidenceScope);
        Assert.Equal(
            new[] { TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired },
            declaration.TrainingExclusionReasons);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void BushAdmissionRequiresExactVanillaBranchMatrixAndEvd120Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.harvest_bushes");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.harvest_bushes"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.harvest_bushes"));
        Assert.Equal(new[] { "EVD-120" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.OutputEvidenceIds);
        Assert.Contains("vanilla_current_location_exact_bush", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("berry_standard_botanist_tea_leaf_golden_walnut", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("collected_walnut_and_cooldown_matrix", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("town", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.harvest_bush", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void GingerAdmissionRequiresExactVanillaCurrentLocationMatrixAndEvd119Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.harvest_ginger");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.harvest_ginger"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.harvest_ginger"));
        Assert.Equal(new[] { "EVD-119" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.OutputEvidenceIds);
        Assert.Contains("vanilla_current_location_exact_ginger", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("rain_efficient_full_inventory_debris", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("energy_xp_matrix", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.harvest_ginger", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void SkullKeyAdmissionIsBoundToOrdinaryMineFloor120AndEvd106Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("mining.obtain_skull_key");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-106" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.OutputEvidenceIds);
        Assert.Contains("ordinary_mines_floor_119_to_120", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("skull_cavern", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("golden_scythe", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void VolcanoAdmissionUsesItsOwnNativeRollingEvidenceAndRemainsMineFamilyIsolatedTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("volcano.reach_caldera");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.OutputEvidenceIds);
        Assert.Contains("volcano_generated_levels_0_to_9", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("typed_combat_intent_to_caldera", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("ordinary_mine", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("skull", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("golden_scythe", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("mining.obtain_skull_key", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void BookAdmissionRequiresAllVanillaBaseBranchesAndEvd124Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("skills.read_books");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("skills.read_books"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("skills.read_books"));
        Assert.Equal(new[] { "EVD-124" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.OutputEvidenceIds);
        Assert.Contains("all_six_vanilla_base_book_branch_families", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("exact_projection_native_use", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingGiftChainClosesItsBoundedRuntimeBoundaryWithoutPromotingRecoveryTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("social.gift_npc");
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
        Assert.NotEmpty(declaration.ReadEvidenceIds);
        Assert.NotEmpty(declaration.CandidateEvidenceIds);
        Assert.NotEmpty(declaration.CompilerEvidenceIds);
        Assert.NotEmpty(declaration.RuntimeEvidenceIds);
        Assert.NotEmpty(declaration.OutputEvidenceIds);
        Assert.Empty(declaration.TrainingExclusionReasons);
        Assert.Contains("social.gift_npc", OptionCapabilityRegistrySource.TrainingAllowlist);

        var recovery = OptionCapabilityRegistrySource.GetRequired("recovery.stabilize_day");
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, recovery.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, recovery.OutputTrainingGate);
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, recovery.TrainingEligibility);
        Assert.NotEmpty(recovery.RuntimeEvidenceIds);
        Assert.NotEmpty(recovery.OutputEvidenceIds);
        Assert.DoesNotContain(
            TrainingAdmissionExclusionReason.RuntimeEvidenceMissing,
            recovery.TrainingExclusionReasons);
        Assert.DoesNotContain(
            TrainingAdmissionExclusionReason.OutputEvidenceMissing,
            recovery.TrainingExclusionReasons);

        Assert.Contains(
            TrainingAdmissionExclusionReason.NotPolicyTrainingOption,
            recovery.TrainingExclusionReasons);
        Assert.DoesNotContain(
            TrainingAdmissionExclusionReason.NotPolicyTrainingOption,
            OptionCapabilityRegistrySource
                .GetRequired("social.gift_npc")
                .TrainingExclusionReasons);
    }

    [Fact]
    public void EveryExcludedOptionHasTypedTrainingAdmissionReasonsTests()
    {
        var excluded = OptionCapabilityRegistrySource.All
            .Where(row => !TrainingEligibilityPolicy.IsEligible(row))
            .ToArray();

        Assert.NotEmpty(excluded);
        Assert.All(excluded, row => Assert.NotEmpty(row.TrainingExclusionReasons));
        Assert.All(
            OptionCapabilityRegistrySource.All.Where(row =>
                row.OptionId.StartsWith("executor.", StringComparison.Ordinal) ||
                row.OptionId is "farm.maintain_crops" or "farm.process_machines" or "recovery.stabilize_day"),
            row => Assert.Contains(
                TrainingAdmissionExclusionReason.NotPolicyTrainingOption,
                row.TrainingExclusionReasons));
    }

    [Fact]
    public void NoDuplicateCapabilityIdTests()
    {
        Assert.Equal(
            OptionCapabilityRegistrySource.All.Count,
            OptionCapabilityRegistrySource.All
                .Select(row => row.OptionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            OptionCapabilityRegistrySource.DailyCandidates.Count,
            OptionCapabilityRegistrySource.DailyCandidates
                .Select(row => row.Kind)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
