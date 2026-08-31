using System;
using System.Collections.Generic;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.OptionRegistry
{
    internal sealed class OptionGovernancePolicy
    {
        public OptionSemanticKind SemanticKind { get; init; }
        public OptionRiskClass RiskClass { get; init; }
        public OptionIrreversibility Irreversibility { get; init; }
        public OptionConfirmationPolicy ConfirmationPolicy { get; init; }
        public OptionHostPolicy HostPolicy { get; init; }
        public OptionOwnershipPolicy OwnershipPolicy { get; init; }
        public AutonomousCandidatePolicy AutonomousCandidatePolicy { get; init; }
        public OptionInvocationPolicy InvocationPolicy { get; init; }
    }

    internal static class OptionGovernanceCatalog
    {
        private static readonly OptionSemanticKind Goal = OptionSemanticKind.GoalTemplate;
        private static readonly OptionSemanticKind C = OptionSemanticKind.CompositeOptionSpec;
        private static readonly OptionSemanticKind Primitive = OptionSemanticKind.PrimitiveOptionSpec;
        private static readonly OptionRiskClass R0 = OptionRiskClass.R0PureRecovery;
        private static readonly OptionRiskClass R1 = OptionRiskClass.R1ReversibleInteraction;
        private static readonly OptionRiskClass R2 = OptionRiskClass.R2Consumptive;
        private static readonly OptionRiskClass R3 = OptionRiskClass.R3CrossDayCommitment;
        private static readonly OptionRiskClass R4 = OptionRiskClass.R4IrreversibleAssetChange;
        private static readonly OptionRiskClass R5 = OptionRiskClass.R5RelationshipOrRouteChoice;
        private static readonly OptionIrreversibility None = OptionIrreversibility.None;
        private static readonly OptionIrreversibility Consume = OptionIrreversibility.Consumptive;
        private static readonly OptionIrreversibility CrossDay = OptionIrreversibility.CrossDayCommitment;
        private static readonly OptionIrreversibility Asset = OptionIrreversibility.IrreversibleAssetChange;
        private static readonly OptionIrreversibility Relationship = OptionIrreversibility.RelationshipOrRouteChoice;
        private static readonly OptionIrreversibility Route = OptionIrreversibility.RelationshipOrRouteChoice;
        private static readonly OptionConfirmationPolicy NoConfirm = OptionConfirmationPolicy.NotRequired;
        private static readonly OptionConfirmationPolicy PolicyConfirm = OptionConfirmationPolicy.PolicyAuthorizationRequired;
        private static readonly OptionConfirmationPolicy ExplicitConfirm = OptionConfirmationPolicy.ExplicitUserConfirmationRequired;
        private static readonly OptionHostPolicy Actor = OptionHostPolicy.ControllingActorAllowed;
        private static readonly OptionHostPolicy Host = OptionHostPolicy.HostOnly;
        private static readonly OptionOwnershipPolicy ActorState = OptionOwnershipPolicy.ActorState;
        private static readonly OptionOwnershipPolicy Inventory = OptionOwnershipPolicy.ActorInventory;
        private static readonly OptionOwnershipPolicy Farm = OptionOwnershipPolicy.SharedFarmState;
        private static readonly OptionOwnershipPolicy World = OptionOwnershipPolicy.SharedWorldState;
        private static readonly OptionOwnershipPolicy Mixed = OptionOwnershipPolicy.Mixed;
        private static readonly AutonomousCandidatePolicy Allowed = AutonomousCandidatePolicy.Allowed;
        private static readonly AutonomousCandidatePolicy Policy = AutonomousCandidatePolicy.PolicyAuthorizationRequired;
        private static readonly AutonomousCandidatePolicy Explicit = AutonomousCandidatePolicy.ExplicitUserConfirmationRequired;
        private static readonly OptionInvocationPolicy PlayerCommand = OptionInvocationPolicy.PlayerCommandOnly;
        private static readonly HashSet<string> NoParameterOptionIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "farm.maintain_crops",
            "farm.process_machines",
            "recovery.stabilize_day",
            "recovery.escape_object_trap",
            "rewards.claim_pot_of_gold",
            "rewards.claim_adventure_guild_reward",
            "rewards.claim_prize_ticket",
            "skills.claim_mastery",
            "rewards.claim_statue_blessing",
            "executor.close_menu"
        };

        private static readonly IReadOnlyDictionary<string, OptionGovernancePolicy> Policies =
            BuildPolicies();

        public static int Count => Policies.Count;

        public static OptionSpec Apply(OptionSpec spec)
        {
            if (!Policies.TryGetValue(spec.OptionId, out var policy))
            {
                throw new InvalidOperationException(
                    $"Option '{spec.OptionId}' has no explicit option_spec.v2 governance policy.");
            }

            var capability = OptionCapabilityRegistrySource.GetRequired(spec.OptionId);
            ValidateCompilerDeclaration(spec.OptionId, capability.CompilerStatus);
            if (capability.AutonomousCandidateEnabled !=
                    (policy.AutonomousCandidatePolicy == AutonomousCandidatePolicy.Allowed) ||
                capability.PlayerConfirmationRequired !=
                    (policy.ConfirmationPolicy == OptionConfirmationPolicy.ExplicitUserConfirmationRequired) ||
                capability.HostOnly != (policy.HostPolicy == OptionHostPolicy.HostOnly) ||
                capability.InvocationPolicy != policy.InvocationPolicy)
            {
                throw new InvalidOperationException(
                    $"Option '{spec.OptionId}' capability safety flags do not match its governance policy.");
            }

            spec.SemanticKind = policy.SemanticKind;
            spec.ParameterSchema = NoParameterOptionIds.Contains(spec.OptionId)
                ? ParameterSchemaPolicy.NoParameters
                : policy.SemanticKind == OptionSemanticKind.GoalTemplate
                    ? ParameterSchemaPolicy.GoalParameters
                    : policy.SemanticKind == OptionSemanticKind.CompositeOptionSpec
                        ? ParameterSchemaPolicy.CandidateBoundParameters
                        : ParameterSchemaPolicy.PrimitiveActionParameters;
            spec.RequiredFactPolicy = CreateRequiredFactPolicy();
            spec.RegistrationStatus = capability.RegistrationStatus;
            spec.ReadStatus = capability.ReadStatus;
            spec.CandidateStatus = capability.CandidateStatus;
            spec.CompilerStatus = capability.CompilerStatus;
            spec.HarnessDispatchSupported = capability.HarnessDispatchSupported;
            spec.ProductExecutorSupported = capability.ProductExecutorSupported;
            spec.InternalExecutionPipelineSupported = capability.InternalExecutionPipelineSupported;
            spec.BeforeVerifierStatus = capability.BeforeVerifierStatus;
            spec.AfterVerifierStatus = capability.AfterVerifierStatus;
            spec.ProductIntegrationStatus = capability.ProductIntegrationStatus;
            spec.RiskClass = policy.RiskClass;
            spec.Irreversibility = policy.Irreversibility;
            spec.ConfirmationPolicy = policy.ConfirmationPolicy;
            spec.HostPolicy = policy.HostPolicy;
            spec.OwnershipPolicy = policy.OwnershipPolicy;
            spec.ModAdapterPolicy = OptionModAdapterPolicy.VanillaNativeOnly;
            spec.CompilerBinding = ActionQueueCompiler.HasStepCompiler(spec.OptionId)
                ? "StardewAI.Core.Execution.ActionQueueCompiler.step"
                : ActionQueueCompiler.HasParameterCompiler(spec.OptionId)
                    ? "StardewAI.Core.Execution.ActionQueueCompiler.parameter"
                    : "unbound";
            spec.BeforeVerifierBinding = "StardewAI.Core.Verifier.Verifier";
            spec.AfterVerifierBinding = "runtime-native-postcondition.pending-e3";
            spec.RuntimeEvidenceId = capability.RuntimeEvidenceIds.Length > 0
                ? string.Join(",", capability.RuntimeEvidenceIds)
                : $"runtime-evidence.pending:{spec.OptionId}";
            spec.RuntimeStatus = capability.RuntimeEvidenceStatus;
            spec.TrainingEligibility = capability.TrainingEligibility;
            spec.PolicyTrainingCandidate = capability.PolicyTrainingCandidate;
            spec.ReadTrainingGate = capability.ReadTrainingGate;
            spec.CandidateTrainingGate = capability.CandidateTrainingGate;
            spec.CompilerTrainingGate = capability.CompilerTrainingGate;
            spec.RuntimeTrainingGate = capability.RuntimeTrainingGate;
            spec.OutputTrainingGate = capability.OutputTrainingGate;
            spec.ReadEvidenceIds = capability.ReadEvidenceIds;
            spec.CandidateEvidenceIds = capability.CandidateEvidenceIds;
            spec.CompilerEvidenceIds = capability.CompilerEvidenceIds;
            spec.RuntimeEvidenceIds = capability.RuntimeEvidenceIds;
            spec.OutputEvidenceIds = capability.OutputEvidenceIds;
            spec.TrainingExclusionReasons = capability.TrainingExclusionReasons;
            spec.TrainingEvidenceScope = capability.TrainingEvidenceScope;
            spec.AutonomousCandidatePolicy = policy.AutonomousCandidatePolicy;
            spec.InvocationPolicy = policy.InvocationPolicy;
            spec.ProductStatus = OptionProductStatus.Registered;
            spec.IrreversibleEffects = policy.Irreversibility == OptionIrreversibility.None
                ? Array.Empty<string>()
                : new[] { ToWireValue(policy.Irreversibility) };
            spec.RiskLevel = ToWireValue(policy.RiskClass);
            spec.Recoverability = policy.Irreversibility == OptionIrreversibility.None
                ? "recoverable"
                : "not_fully_recoverable";
            Validate(spec);
            return spec;
        }

        public static void Validate(OptionSpec spec)
        {
            if (spec.SchemaVersion != "option_spec.v2" ||
                spec.SemanticKind == OptionSemanticKind.Unknown ||
                spec.ParameterSchema == ParameterSchemaPolicy.Unknown ||
                spec.RequiredFactPolicy.Mode == RequiredFactPolicyMode.Unknown ||
                spec.RequiredFactPolicy.DefaultRule.AllowedStatuses.Length == 0 ||
                spec.RequiredFactPolicy.DefaultRule.RequiredProvenanceKinds.Length == 0 ||
                spec.RequiredFactPolicy.DefaultRule.AllowedAdapterIds.Length == 0 ||
                spec.RequiredFactPolicy.DefaultRule.MinimumConfidence <= 0 ||
                spec.RequiredFactPolicy.DefaultRule.MaximumAgeTicks <= 0 ||
                spec.RegistrationStatus == CapabilityRegistrationStatus.Unknown ||
                spec.ReadStatus == CapabilityReadStatus.Unknown ||
                spec.CandidateStatus == CapabilityCandidateStatus.Unknown ||
                spec.CompilerStatus == CapabilityCompilerStatus.Unknown ||
                spec.BeforeVerifierStatus == CapabilityVerifierStatus.Unknown ||
                spec.AfterVerifierStatus == CapabilityVerifierStatus.Unknown ||
                spec.ProductIntegrationStatus == CapabilityProductIntegrationStatus.Unknown ||
                spec.RiskClass == OptionRiskClass.Unknown ||
                spec.Irreversibility == OptionIrreversibility.Unknown ||
                spec.ConfirmationPolicy == OptionConfirmationPolicy.Unknown ||
                spec.HostPolicy == OptionHostPolicy.Unknown ||
                spec.OwnershipPolicy == OptionOwnershipPolicy.Unknown ||
                spec.ModAdapterPolicy == OptionModAdapterPolicy.Unknown ||
                spec.RuntimeStatus == OptionRuntimeStatus.Unknown ||
                spec.TrainingEligibility == OptionTrainingEligibility.Unknown ||
                spec.AutonomousCandidatePolicy == AutonomousCandidatePolicy.Unknown ||
                spec.ProductStatus == OptionProductStatus.Unknown ||
                string.IsNullOrWhiteSpace(spec.CompilerBinding) ||
                string.IsNullOrWhiteSpace(spec.BeforeVerifierBinding) ||
                string.IsNullOrWhiteSpace(spec.AfterVerifierBinding) ||
                string.IsNullOrWhiteSpace(spec.RuntimeEvidenceId))
            {
                throw new InvalidOperationException(
                    $"Option '{spec.OptionId}' has an incomplete option_spec.v2 governance contract.");
            }

            if (spec.Irreversibility != OptionIrreversibility.None &&
                spec.ConfirmationPolicy == OptionConfirmationPolicy.NotRequired)
            {
                throw new InvalidOperationException(
                    $"Option '{spec.OptionId}' is irreversible but has no confirmation policy.");
            }

            var factOverrides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in spec.RequiredFactPolicy.FactOverrides)
            {
                if (!ValidFactRule(rule) ||
                    !Array.Exists(spec.RequiredStateFactors, factor =>
                        string.Equals(factor, rule.StateFactor, StringComparison.Ordinal)) ||
                    !factOverrides.Add(rule.StateFactor))
                {
                    throw new InvalidOperationException(
                        $"Option '{spec.OptionId}' has an invalid required-fact override for '{rule.StateFactor}'.");
                }
            }
        }

        private static IReadOnlyDictionary<string, OptionGovernancePolicy> BuildPolicies()
        {
            var rows = new[]
            {
                P("farm.maintain_crops", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("farm.process_machines", C, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("farm.collect_machine_outputs", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("farm.load_supported_machine_input", C, R2, Consume, PolicyConfirm, Actor, Inventory, Allowed),
                P("farm.establish_supported_machine_capacity", C, R2, Consume, PolicyConfirm, Actor, Farm, Allowed),
                P("farm.fulfill_machine_task_demand", C, R2, Consume, PolicyConfirm, Actor, Inventory, Allowed),
                P("farm.collect_animal_products", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("animals.purchase", C, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("animals.manage_animal", C, R2, Asset, PolicyConfirm, Actor, Farm, Policy),
                P("crafting.cook_recipe", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("crafting.forge_item", C, R2, Asset, PolicyConfirm, Actor, Inventory, Policy),
                P("buildings.construct", C, R3, CrossDay, PolicyConfirm, Host, Farm, Policy),
                P("buildings.change_skin", C, R2, Asset, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("buildings.paint", C, R2, Asset, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("farm.care_for_pets", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("museum.donate_items", C, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("island.field_office_donate", C, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("island.field_office_survey", C, R1, None, NoConfirm, Actor, World, Allowed),
                P("festival.manage_grange_display", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("festival.play_fishing_game", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("festival.play_slingshot_game", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("festival.play_strength_game", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("festival.spin_wheel", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("minigame.play_calico_jack", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("minigame.play_slots", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("minigame.play_crane_game", C, R2, Consume, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("minigame.play_darts", C, R1, None, NoConfirm, Actor, World, Allowed),
                P("minigame.play_prairie_king", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("community_center.donate_bundle_items", C, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("joja.advance_development", C, R5, Route, ExplicitConfirm, Host, World, Explicit),
                P("housing.advance_farmhouse", C, R3, CrossDay, ExplicitConfirm, Host, Farm, Explicit),
                P("housing.renovate", C, R4, Asset, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("skills.read_books", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("economy.buy_supplies", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("economy.sell_items", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("economy.ship_items", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("inventory.transfer_item", C, R1, None, NoConfirm, Actor, Inventory, Policy),
                P("social.talk_npc", C, R5, Relationship, PolicyConfirm, Actor, ActorState, Policy),
                P("social.gift_npc", C, R5, Relationship, PolicyConfirm, Actor, Mixed, Policy),
                P("social.advance_partnership", C, R5, Relationship, ExplicitConfirm, Actor, Mixed, Explicit),
                P("social.emote", C, R1, None, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("social.watch_movie", C, R2, Consume, PolicyConfirm, Actor, Mixed, Allowed),
                P("story.advance_event", C, R4, Asset, PolicyConfirm, Actor, Mixed, Allowed),
                P("story.advance_event_minigame", C, R4, Asset, PolicyConfirm, Actor, Mixed, Allowed),
                P("quest.advance", Goal, R2, Consume, PolicyConfirm, Actor, Mixed, Policy),
                P("quest.accept_daily", C, R3, CrossDay, PolicyConfirm, Actor, ActorState, Policy),
                P("quest.accept_special_order", C, R3, CrossDay, PolicyConfirm, Actor, Mixed, Policy),
                P("quest.claim_reward", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("quest.cancel", C, R4, Asset, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("rewards.claim_adventure_guild_reward", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("rewards.claim_prize_ticket", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("skills.claim_mastery", C, R1, None, NoConfirm, Actor, Mixed, Allowed),
                P("mail.process_letter", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("skills.choose_profession", C, R4, Asset, PolicyConfirm, Actor, ActorState, Policy),
                P("strategy.grandpa_progress", Goal, R1, None, NoConfirm, Actor, Mixed, Allowed),
                P("exploration.visit_location", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("fishing.catch_fish", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("fishing.collect_crab_pots", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("fishing.service_fish_ponds", C, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("fishing.manage_fish_pond", C, R4, Asset, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("foraging.collect_spawned_objects", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.harvest_ginger", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.harvest_bushes", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.harvest_fruit_tree", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.harvest_tree_product", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.rummage_garbage", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.clear_green_rain_bushes", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.pan_ore_spot", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("mining.reach_depth", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("mining.use_elevator", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("mining.obtain_skull_key", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("mining.claim_reward_chests", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("mining.activate_calico_statue", C, R1, None, NoConfirm, Host, World, Allowed),
                P("multiplayer.manage_wallet", C, R2, Asset, ExplicitConfirm, Actor, Mixed, Explicit, PlayerCommand),
                P("multiplayer.send_chat", C, R2, None, ExplicitConfirm, Actor, Mixed, Explicit, PlayerCommand),
                P("player.choose_bobber", C, R1, None, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("player.choose_jukebox_track", C, R1, None, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("player.customize", C, R2, Asset, ExplicitConfirm, Actor, Mixed, Explicit, PlayerCommand),
                P("processing.crack_geode", C, R2, Consume, PolicyConfirm, Actor, Inventory, Allowed),
                P("rewards.claim_pot_of_gold", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("mining.choose_dwarf_statue_power", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("rewards.claim_statue_blessing", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("world.rotate_house_plant", Primitive, R1, None, ExplicitConfirm, Actor, World, Explicit, PlayerCommand),
                P("world.play_singing_stone", Primitive, R1, None, ExplicitConfirm, Actor, World, Explicit, PlayerCommand),
                P("world.tune_flute_block", Primitive, R1, None, ExplicitConfirm, Actor, World, Explicit, PlayerCommand),
                P("world.tune_drum_block", Primitive, R1, None, ExplicitConfirm, Actor, World, Explicit, PlayerCommand),
                P("farming.read_farm_computer_report", Primitive, R1, None, ExplicitConfirm, Actor, World, Explicit, PlayerCommand),
                P("farming.collect_slime_ball", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("animals.withdraw_feed_hopper_hay", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("animals.collect_auto_grabber_contents", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("movement.use_mini_obelisk", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("mining.acquire_golden_scythe", C, R4, Asset, ExplicitConfirm, Actor, ActorState, Explicit),
                P("volcano.reach_caldera", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("recovery.stabilize_day", C, R0, None, NoConfirm, Actor, Mixed, Allowed),
                P("recovery.sleep_in_tent", C, R3, CrossDay, PolicyConfirm, Actor, World, Policy),
                P("recovery.escape_object_trap", C, R3, Asset, PolicyConfirm, Actor, World, Policy),

                P("executor.move_to_tile", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.traverse_connector", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.face_direction", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.interact", Primitive, R1, None, NoConfirm, Actor, Mixed, Allowed),
                P("executor.accept_daily_quest", Primitive, R3, CrossDay, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.accept_special_order", Primitive, R3, CrossDay, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.claim_quest_reward", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.cancel_quest", Primitive, R4, Asset, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("executor.claim_adventure_guild_reward", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.claim_prize_ticket", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.claim_mastery", Primitive, R1, None, NoConfirm, Actor, Mixed, Allowed),
                P("executor.buy_shop_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.sell_shop_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.choose_dialogue_response", Primitive, R5, Route, ExplicitConfirm, Actor, Mixed, Explicit),
                P("executor.answer_field_office_survey", Primitive, R1, None, NoConfirm, Actor, World, Allowed),
                P("executor.choose_animal_purchase_response", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.purchase_animal", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.manage_animal", Primitive, R2, Asset, PolicyConfirm, Actor, Farm, Policy),
                P("executor.cook_recipe", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.forge_item", Primitive, R2, Asset, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.sleep", Primitive, R3, CrossDay, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.close_menu", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.wait_ticks", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.claim_mine_reward_chest", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.activate_calico_statue", Primitive, R1, None, NoConfirm, Host, World, Allowed),
                P("executor.manage_multiplayer_wallet", Primitive, R2, Asset, ExplicitConfirm, Actor, Mixed, Explicit, PlayerCommand),
                P("executor.send_multiplayer_chat", Primitive, R2, None, ExplicitConfirm, Actor, Mixed, Explicit, PlayerCommand),
                P("executor.choose_bobber_style", Primitive, R1, None, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("executor.choose_jukebox_track", Primitive, R1, None, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("executor.customize_player", Primitive, R2, Asset, ExplicitConfirm, Actor, Mixed, Explicit, PlayerCommand),
                P("executor.crack_geode", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Allowed),
                P("executor.mine_stone", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.break_container", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.break_resource_clump", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.combat_monster", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.shoot_monster", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.place_bomb", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.place_staircase", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.consume_food", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.descend_ladder", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.descend_shaft", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.exit_mine", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.cool_volcano_lava", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.break_volcano_stone", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.break_volcano_container", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.combat_volcano_monster", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.catch_fish", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.play_junimo_kart", Primitive, R1, None, NoConfirm, Actor, ActorState, Policy),
                P("executor.play_prairie_king", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.ship_inventory_item_to_bin", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.transfer_material", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.social_interact", Primitive, R5, Relationship, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.watch_movie", Primitive, R2, Consume, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.advance_story_event", Primitive, R4, Asset, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.advance_story_event_minigame", Primitive, R4, Asset, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.perform_emote", Primitive, R1, None, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("executor.quest_npc_interact", Primitive, R2, Consume, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.quest_drop_box_donate", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.clear_obstacle", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.break_farm_resource_clump", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.break_current_location_resource_clump", Primitive, R1, None, NoConfirm, Actor, World, Allowed),
                P("executor.water_crop", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.apply_fertilizer", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.apply_tree_treatment", Primitive, R3, CrossDay, PolicyConfirm, Actor, World, Policy),
                P("executor.plant_seed", Primitive, R3, CrossDay, PolicyConfirm, Actor, Farm, Policy),
                P("executor.till_soil", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.harvest_crop", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_giant_crop", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.pickup_debris", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_spawned_object", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_ginger", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_bush", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_fruit_tree", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_tree_product", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.rummage_garbage", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_crab_pot", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_fish_pond_output", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.complete_fish_pond_request", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.collect_animal_product", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.pet_interact", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.fill_pet_bowl", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.donate_museum_item", Primitive, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("executor.donate_field_office_piece", Primitive, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("executor.manage_grange_display", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.play_fair_fishing_game", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.play_fair_slingshot_game", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.play_fair_strength_game", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.spin_fair_wheel", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.play_calico_jack", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.play_slots", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.play_crane_game", Primitive, R2, Consume, ExplicitConfirm, Actor, ActorState, Explicit, PlayerCommand),
                P("executor.play_darts", Primitive, R1, None, NoConfirm, Actor, World, Allowed),
                P("executor.donate_community_center_item", Primitive, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("executor.purchase_joja_membership", Primitive, R5, Route, ExplicitConfirm, Host, World, Explicit),
                P("executor.purchase_joja_project", Primitive, R5, Route, ExplicitConfirm, Host, World, Explicit),
                P("executor.purchase_farmhouse_upgrade", Primitive, R3, CrossDay, ExplicitConfirm, Host, Farm, Explicit),
                P("executor.renovate_home", Primitive, R4, Asset, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("executor.construct_building", Primitive, R3, CrossDay, ExplicitConfirm, Host, Farm, Explicit),
                P("executor.change_building_skin", Primitive, R2, Asset, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("executor.pan_ore_spot", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_machine_output", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.load_machine_input", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.name_hatched_animal", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.craft_machine_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.craft_storage_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.craft_quest_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.place_machine", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.remove_machine", Primitive, R3, Route, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_storage", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_cookout_kit", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_tent", Primitive, R2, Consume, PolicyConfirm, Actor, World, Policy),
                P("executor.place_crab_pot", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_fence", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_flooring", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.plant_grass", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_furniture", Primitive, R2, Consume, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("executor.place_sign", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                // The source is preserved, but native replacement discards the previous, non-returned display copy.
                P("executor.set_sign_display_item", Primitive, R2, Consume, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("executor.edit_text_sign", Primitive, R1, None, ExplicitConfirm, Actor, Farm, Explicit, PlayerCommand),
                P("executor.load_crab_pot_bait", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.read_book", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.read_secret_note", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.use_firework", Primitive, R2, Consume, ExplicitConfirm, Actor, World, Explicit, PlayerCommand),
                P("executor.use_horse_flute", Primitive, R1, None, NoConfirm, Actor, World, Policy),
                P("executor.use_monster_musk", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.use_rain_totem", Primitive, R2, Consume, PolicyConfirm, Actor, World, Policy),
                P("executor.use_return_scepter", Primitive, R1, None, NoConfirm, Actor, World, Policy),
                P("executor.use_treasure_totem", Primitive, R2, Consume, PolicyConfirm, Actor, World, Policy),
                P("executor.use_warp_totem", Primitive, R2, Consume, PolicyConfirm, Actor, World, Policy),
                P("executor.select_safe_item_slot", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed)
            };

            var result = new Dictionary<string, OptionGovernancePolicy>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (!result.TryAdd(row.Key, row.Value))
                {
                    throw new InvalidOperationException(
                        $"Duplicate option governance policy for '{row.Key}'.");
                }
            }

            return result;
        }

        private static RequiredFactPolicy CreateRequiredFactPolicy()
        {
            return new RequiredFactPolicy
            {
                Mode = RequiredFactPolicyMode.AllRequiredFailClosed,
                DefaultRule = new RequiredFactRule
                {
                    StateFactor = "*",
                    AllowedStatuses = new[] { "available" },
                    MinimumConfidence = 1.0,
                    MaximumAgeTicks = 120,
                    RequiredProvenanceKinds = new[] { "game_object", "test" },
                    AllowedAdapterIds = new[]
                    {
                        "test",
                        "vanilla_1_6",
                        "vanilla_1_6_farm",
                        "vanilla_1_6_menu",
                        "vanilla_1_6_route",
                        "vanilla_1_6_npc",
                        "vanilla_1_6_shops",
                        "vanilla_1_6_pet",
                        "vanilla_1_6_pet_bowl",
                        "vanilla_1_6_crop_data",
                        "vanilla_1_6_farm_and_animal_houses",
                        "vanilla_1_6_animal_purchase",
                        "vanilla_1_6_material_inventory_graph",
                        "mining_read_adapter",
                        "volcano_read_adapter",
                        "transparent_bridge_main_thread_cache",
                        "smapi_mod_registry",
                        "smapi_save_data",
                        "smapi_mod_state",
                        "smapi_constants",
                        "process_environment",
                        "bridge_manifest",
                        "bridge_transport"
                    },
                    AllowedDerivationIds = Array.Empty<string>()
                }
            };
        }

        private static bool ValidFactRule(RequiredFactRule rule)
        {
            return !string.IsNullOrWhiteSpace(rule.StateFactor) &&
                rule.AllowedStatuses.Length > 0 &&
                rule.MinimumConfidence > 0 &&
                rule.MaximumAgeTicks > 0 &&
                rule.RequiredProvenanceKinds.Length > 0 &&
                rule.AllowedAdapterIds.Length > 0;
        }

        private static void ValidateCompilerDeclaration(
            string optionId,
            CapabilityCompilerStatus declaredStatus)
        {
            var hasStep = ActionQueueCompiler.HasStepCompiler(optionId) ||
                DailyPlanCompiler.HasOptionCompiler(optionId);
            var hasParameter = ActionQueueCompiler.HasParameterCompiler(optionId);
            var actualStatus = hasStep && hasParameter
                ? CapabilityCompilerStatus.StepAndParameterCompilerDeclared
                : hasStep
                    ? CapabilityCompilerStatus.StepCompilerDeclared
                    : hasParameter
                        ? CapabilityCompilerStatus.ParameterCompilerDeclared
                        : CapabilityCompilerStatus.Unbound;
            if (declaredStatus != actualStatus)
            {
                throw new InvalidOperationException(
                    $"Option '{optionId}' compiler declaration mismatch: declared={declaredStatus}; actual={actualStatus}.");
            }
        }

        private static KeyValuePair<string, OptionGovernancePolicy> P(
            string id,
            OptionSemanticKind semanticKind,
            OptionRiskClass riskClass,
            OptionIrreversibility irreversibility,
            OptionConfirmationPolicy confirmationPolicy,
            OptionHostPolicy hostPolicy,
            OptionOwnershipPolicy ownershipPolicy,
            AutonomousCandidatePolicy autonomousCandidatePolicy,
            OptionInvocationPolicy invocationPolicy = OptionInvocationPolicy.PolicyOrAutonomous)
        {
            return new KeyValuePair<string, OptionGovernancePolicy>(
                id,
                new OptionGovernancePolicy
                {
                    SemanticKind = semanticKind,
                    RiskClass = riskClass,
                    Irreversibility = irreversibility,
                    ConfirmationPolicy = confirmationPolicy,
                    HostPolicy = hostPolicy,
                    OwnershipPolicy = ownershipPolicy,
                    AutonomousCandidatePolicy = autonomousCandidatePolicy,
                    InvocationPolicy = invocationPolicy
                });
        }

        private static string ToWireValue<T>(T value) where T : struct, Enum
        {
            var name = value.ToString();
            var chars = new List<char>(name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) &&
                    (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                {
                    chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(name[i]));
            }

            return new string(chars.ToArray());
        }

    }
}
