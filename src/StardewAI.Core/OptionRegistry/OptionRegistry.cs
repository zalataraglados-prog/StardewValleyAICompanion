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
                new[] { "player.location_id", "player.energy", "farm.crops" },
                new[] { "crop obligations inspected", "manual crop work plan produced" },
                new[] { "block_unavailable_required_state", "block_unverified_movement" }));

            Register(Option("farm.process_machines", "farm", "Process machines",
                new[] { "player.location_id", "player.inventory", "farm.machines" },
                new[] { "machine queue inspected", "manual machine plan produced" },
                new[] { "never_sell_protected_items", "block_unavailable_required_state" }));

            Register(Option("economy.buy_supplies", "economy", "Buy supplies preview",
                new[] { "time.time", "player.money", "locations.shops", "menus.active_menu" },
                new[] { "purchase list verified", "budget impact previewed" },
                new[] { "never_spend_below_emergency_reserve", "block_unknown_ui_clicks" }));

            Register(Option("economy.sell_items", "economy", "Sell items preview",
                new[] { "player.inventory", "menus.active_menu" },
                new[] { "sell candidates previewed" },
                new[] { "never_sell_protected_items", "block_unknown_ui_clicks" }));

            Register(Option("social.gift_npc", "social", "Gift NPC preview",
                new[] { "player.inventory", "npcs.schedules", "npcs.friendships" },
                new[] { "gift target verified", "manual gift plan produced" },
                new[] { "never_sell_protected_items", "block_unavailable_required_state" }));

            Register(Option("quest.advance", "quest", "Advance quest preview",
                new[] { "quests.active_quests", "player.inventory", "time.time" },
                new[] { "quest step previewed" },
                new[] { "block_unavailable_required_state", "block_state_hash_mismatch" }));

            Register(Option("exploration.visit_location", "exploration", "Visit location preview",
                new[] { "locations.collision_grid", "player.energy", "time.time" },
                new[] { "route previewed" },
                new[] { "block_unverified_movement", "block_unavailable_required_state" }));

            Register(Option("recovery.stabilize_day", "recovery", "Stabilize current day",
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
            string[] requiredStateFactors,
            string[] expectedEffects,
            string[] safetyConstraints)
        {
            return new OptionSpec
            {
                OptionId = id,
                Domain = domain,
                Name = name,
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
