using System;
using System.Collections.Generic;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;

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
        private static readonly HashSet<string> NoParameterOptionIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "farm.maintain_crops",
            "farm.process_machines",
            "recovery.stabilize_day",
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
                capability.HostOnly != (policy.HostPolicy == OptionHostPolicy.HostOnly))
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
            spec.RuntimeEvidenceId = $"runtime-evidence.pending:{spec.OptionId}";
            spec.RuntimeStatus = capability.RuntimeEvidenceStatus;
            spec.TrainingEligibility = capability.TrainingEligibility;
            spec.AutonomousCandidatePolicy = policy.AutonomousCandidatePolicy;
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
                P("farm.collect_animal_products", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("farm.care_for_pets", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("museum.donate_items", C, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("community_center.donate_bundle_items", C, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("joja.advance_development", C, R5, Route, ExplicitConfirm, Host, World, Explicit),
                P("housing.advance_farmhouse", C, R3, CrossDay, PolicyConfirm, Host, Farm, Policy),
                P("skills.read_books", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("economy.buy_supplies", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("economy.sell_items", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("economy.ship_items", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("social.talk_npc", C, R5, Relationship, PolicyConfirm, Actor, ActorState, Policy),
                P("social.gift_npc", C, R5, Relationship, PolicyConfirm, Actor, Mixed, Policy),
                P("quest.advance", Goal, R2, Consume, PolicyConfirm, Actor, Mixed, Policy),
                P("strategy.grandpa_progress", Goal, R1, None, NoConfirm, Actor, Mixed, Allowed),
                P("exploration.visit_location", C, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("fishing.catch_fish", C, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("fishing.collect_crab_pots", C, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("fishing.service_fish_ponds", C, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("foraging.collect_spawned_objects", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.harvest_ginger", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.harvest_bushes", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.clear_green_rain_bushes", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("foraging.pan_ore_spot", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("mining.reach_depth", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("mining.obtain_skull_key", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("mining.claim_reward_chests", C, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("mining.acquire_golden_scythe", C, R4, Asset, ExplicitConfirm, Actor, ActorState, Explicit),
                P("volcano.reach_caldera", C, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("recovery.stabilize_day", C, R0, None, NoConfirm, Actor, Mixed, Allowed),

                P("executor.move_to_tile", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.traverse_connector", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.face_direction", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.interact", Primitive, R1, None, NoConfirm, Actor, Mixed, Allowed),
                P("executor.buy_shop_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.sell_shop_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.choose_dialogue_response", Primitive, R5, Route, ExplicitConfirm, Actor, Mixed, Explicit),
                P("executor.sleep", Primitive, R3, CrossDay, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.close_menu", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.wait_ticks", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.claim_mine_reward_chest", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.mine_stone", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.break_container", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.break_resource_clump", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.combat_monster", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.shoot_monster", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.place_bomb", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.consume_food", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.descend_ladder", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.descend_shaft", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
                P("executor.exit_mine", Primitive, R0, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.cool_volcano_lava", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.break_volcano_stone", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.break_volcano_container", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.combat_volcano_monster", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.catch_fish", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.ship_inventory_item_to_bin", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.transfer_material", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.social_interact", Primitive, R5, Relationship, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.quest_npc_interact", Primitive, R2, Consume, PolicyConfirm, Actor, Mixed, Policy),
                P("executor.quest_drop_box_donate", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.clear_obstacle", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.break_farm_resource_clump", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.break_current_location_resource_clump", Primitive, R1, None, NoConfirm, Actor, World, Allowed),
                P("executor.plant_seed", Primitive, R3, CrossDay, PolicyConfirm, Actor, Farm, Policy),
                P("executor.till_soil", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.harvest_crop", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_giant_crop", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.pickup_debris", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_spawned_object", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_ginger", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.harvest_bush", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_crab_pot", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_fish_pond_output", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.complete_fish_pond_request", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.collect_animal_product", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.pet_interact", Primitive, R1, None, NoConfirm, Actor, ActorState, Allowed),
                P("executor.fill_pet_bowl", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.donate_museum_item", Primitive, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("executor.donate_community_center_item", Primitive, R4, Asset, ExplicitConfirm, Actor, World, Explicit),
                P("executor.purchase_joja_membership", Primitive, R5, Route, ExplicitConfirm, Host, World, Explicit),
                P("executor.purchase_joja_project", Primitive, R5, Route, ExplicitConfirm, Host, World, Explicit),
                P("executor.purchase_farmhouse_upgrade", Primitive, R3, CrossDay, PolicyConfirm, Host, Farm, Policy),
                P("executor.pan_ore_spot", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.collect_machine_output", Primitive, R1, None, NoConfirm, Actor, Inventory, Allowed),
                P("executor.load_machine_input", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.name_hatched_animal", Primitive, R1, None, NoConfirm, Actor, Farm, Allowed),
                P("executor.craft_machine_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.craft_storage_item", Primitive, R2, Consume, PolicyConfirm, Actor, Inventory, Policy),
                P("executor.place_machine", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.remove_machine", Primitive, R3, Route, PolicyConfirm, Actor, Farm, Policy),
                P("executor.place_storage", Primitive, R2, Consume, PolicyConfirm, Actor, Farm, Policy),
                P("executor.read_book", Primitive, R2, Consume, PolicyConfirm, Actor, ActorState, Policy),
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
            var hasStep = ActionQueueCompiler.HasStepCompiler(optionId);
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
            AutonomousCandidatePolicy autonomousCandidatePolicy)
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
                    AutonomousCandidatePolicy = autonomousCandidatePolicy
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
