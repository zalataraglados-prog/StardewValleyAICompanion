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
                new[] { "player.location_id", "player.energy", "farm.crops" },
                new[] { "crop obligations inspected", "crop maintenance action steps produced" },
                new[] { "block_unavailable_required_state", "block_unverified_movement" }));

            Register(Option("farm.process_machines", "farm", "Process machines",
                OptionBehaviorCategories.Mechanical,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "player.location_id", "player.inventory", "farm.machines" },
                new[] { "machine queue inspected", "machine action steps produced" },
                new[] { "never_sell_protected_items", "block_unavailable_required_state" }));

            Register(Option("economy.buy_supplies", "economy", "Buy supplies preview",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "time.time", "player.money", "locations.shops", "menus.active_menu" },
                new[] { "purchase list verified", "budget impact previewed" },
                new[] { "never_spend_below_emergency_reserve", "block_unknown_ui_clicks" }));

            Register(Option("economy.sell_items", "economy", "Sell items preview",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.inventory", "menus.active_menu" },
                new[] { "sell candidates previewed" },
                new[] { "never_sell_protected_items", "block_unknown_ui_clicks" }));

            Register(Option("social.gift_npc", "social", "Gift NPC preview",
                OptionBehaviorCategories.SocialStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "player.inventory", "npcs.schedules", "npcs.friendships" },
                new[] { "gift target verified", "manual gift plan produced" },
                new[] { "never_sell_protected_items", "block_unavailable_required_state" }));

            Register(Option("quest.advance", "quest", "Advance quest preview",
                OptionBehaviorCategories.EconomicStrategic,
                CompilerResponsibilities.PlanValidation,
                TrainingRoles.StrategyValue,
                new[] { "quests.active_quests", "player.inventory", "time.time" },
                new[] { "quest step previewed" },
                new[] { "block_unavailable_required_state", "block_state_hash_mismatch" }));

            Register(Option("exploration.visit_location", "exploration", "Visit location preview",
                OptionBehaviorCategories.ParameterizedMechanical,
                CompilerResponsibilities.ParameterExpansion,
                TrainingRoles.Mixed,
                new[] { "locations.collision_grid", "player.energy", "time.time" },
                new[] { "route previewed" },
                new[] { "block_unverified_movement", "block_unavailable_required_state" }));

            Register(Option("recovery.stabilize_day", "recovery", "Stabilize current day",
                OptionBehaviorCategories.Recovery,
                CompilerResponsibilities.FullActionExpansion,
                TrainingRoles.ExecutorCalibration,
                new[] { "time.time", "player.energy", "menus.active_menu" },
                new[] { "urgent risks inspected", "safe stopping plan produced" },
                new[] { "block_state_hash_mismatch", "block_mutation_in_observer_or_planner_mode" }));
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
