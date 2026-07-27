using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Infrastructure;

internal static class
    AnvilReforgeStrategicDemandProjection
{
    internal static AnvilReforgeStrategicDemand Read(
        string expectedEffect,
        string goalId)
    {
        var capability = ReadValue(
            expectedEffect,
            "anvil_reforge_capability_class");
        if (string.IsNullOrWhiteSpace(capability))
        {
            return AnvilReforgeStrategicDemand.Blocked;
        }

        var goalFamily = GoalFamily(goalId);
        var status = goalFamily switch
        {
            "broad_strategic_unresolved" =>
                "broad_goal_requires_downstream_subgoal",
            "unsupported" =>
                "unsupported_goal_family_neutral",
            _ =>
                "explicit_rule_based_goal_capability_projection"
        };
        var affinity = status ==
            "explicit_rule_based_goal_capability_projection"
                ? Affinity(goalFamily, capability)
                : 0;
        var loadoutAdjustment =
            LoadoutAdjustment(expectedEffect);
        var effectiveDemand = status ==
            "explicit_rule_based_goal_capability_projection"
                ? Math.Clamp(
                    affinity + loadoutAdjustment,
                    -1,
                    1)
                : 0;
        return new AnvilReforgeStrategicDemand(
            true,
            status,
            goalFamily,
            capability,
            affinity,
            loadoutAdjustment,
            effectiveDemand,
            goalFamily ==
                "loot_preserving_combat" &&
            capability ==
                "enemy_removal_no_kill_or_loot_credit"
                ? "frog_removal_conflicts_with_loot_kill_credit_and_infestation_completion"
                : effectiveDemand > 0
                    ? "capability_supports_explicit_goal"
                    : effectiveDemand < 0
                        ? "capability_conflicts_with_explicit_goal"
                        : "goal_does_not_distinguish_capability");
    }

    internal static string ExpectedEffectSuffix(
        AnvilReforgeStrategicDemand demand)
    {
        return demand.Supported
            ? ";anvil_reforge_goal_demand_status=" +
              demand.Status +
              ";anvil_reforge_goal_family=" +
              demand.GoalFamily +
              ";anvil_reforge_goal_capability_affinity=" +
              Format(demand.CapabilityAffinity) +
              ";anvil_reforge_loadout_adjustment=" +
              Format(demand.LoadoutAdjustment) +
              ";anvil_reforge_effective_demand_score=" +
              Format(demand.EffectiveDemandScore) +
              ";anvil_reforge_goal_demand_reason=" +
              demand.Reason
            : string.Empty;
    }

    internal static SmallModelActionParameter[]
        Parameters(
            AnvilReforgeStrategicDemand demand)
    {
        if (!demand.Supported)
        {
            return Array.Empty<
                SmallModelActionParameter>();
        }

        return new[]
        {
            Parameter(
                "anvil_reforge_goal_demand_status",
                demand.Status),
            Parameter(
                "anvil_reforge_goal_family",
                demand.GoalFamily),
            Parameter(
                "anvil_reforge_goal_capability_affinity",
                Format(
                    demand.CapabilityAffinity)),
            Parameter(
                "anvil_reforge_loadout_adjustment",
                Format(
                    demand.LoadoutAdjustment)),
            Parameter(
                "anvil_reforge_effective_demand_score",
                Format(
                    demand.EffectiveDemandScore)),
            Parameter(
                "anvil_reforge_goal_demand_reason",
                demand.Reason)
        };
    }

    private static string GoalFamily(string goalId)
    {
        var goal = (goalId ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(goal) ||
            goal.Contains("grandpa") ||
            goal is "daily.closed_loop" or
                "goal.autonomous.singleplayer")
        {
            return "broad_strategic_unresolved";
        }
        if (ContainsAny(
                goal,
                "loot",
                "drop",
                "kill_credit",
                "eradication",
                "infested",
                "quest_kill"))
        {
            return "loot_preserving_combat";
        }
        if (ContainsAny(
                goal,
                "economy",
                "profit",
                "money",
                "wealth",
                "currency",
                "gold"))
        {
            return "economy";
        }
        if (ContainsAny(
                goal,
                "survival",
                "heal",
                "health",
                "sustain"))
        {
            return "survival";
        }
        if (ContainsAny(
                goal,
                "control",
                "freeze",
                "crowd"))
        {
            return "control";
        }
        if (ContainsAny(
                goal,
                "speed",
                "mobility",
                "travel_time"))
        {
            return "mobility";
        }
        if (ContainsAny(
                goal,
                "combat",
                "mine",
                "mining",
                "depth",
                "skull",
                "volcano",
                "monster",
                "danger"))
        {
            return "combat_progress";
        }
        return "unsupported";
    }

    private static double Affinity(
        string goalFamily,
        string capability)
    {
        return goalFamily switch
        {
            "economy" => capability switch
            {
                "kill_triggered_currency" => 1,
                _ => 0
            },
            "survival" => capability switch
            {
                "reactive_healing" => 1,
                "ranged_freeze_control" => 0.8,
                "enemy_removal_no_kill_or_loot_credit" =>
                    0.65,
                "ranged_direct_damage" => 0.5,
                "critical_hit_mobility" => 0.35,
                "kill_triggered_currency" => 0.1,
                _ => 0
            },
            "control" => capability switch
            {
                "ranged_freeze_control" => 1,
                "enemy_removal_no_kill_or_loot_credit" =>
                    0.8,
                "reactive_healing" => 0.35,
                "ranged_direct_damage" => 0.3,
                _ => 0
            },
            "mobility" => capability switch
            {
                "critical_hit_mobility" => 1,
                _ => 0
            },
            "loot_preserving_combat" => capability switch
            {
                "enemy_removal_no_kill_or_loot_credit" =>
                    -1,
                "ranged_direct_damage" => 1,
                "kill_triggered_currency" => 0.85,
                "ranged_freeze_control" => 0.7,
                "reactive_healing" => 0.55,
                "critical_hit_mobility" => 0.5,
                _ => 0
            },
            "combat_progress" => capability switch
            {
                "ranged_direct_damage" => 1,
                "ranged_freeze_control" => 0.85,
                "reactive_healing" => 0.8,
                "critical_hit_mobility" => 0.65,
                "enemy_removal_no_kill_or_loot_credit" =>
                    0.35,
                "kill_triggered_currency" => 0.25,
                _ => 0
            },
            _ => 0
        };
    }

    private static double LoadoutAdjustment(
        string expectedEffect)
    {
        var unlocked = ReadInt(
            expectedEffect,
            "anvil_reforge_unlocked_slot_count");
        var empty = ReadInt(
            expectedEffect,
            "anvil_reforge_empty_unlocked_slot_count");
        var sameType = ReadInt(
            expectedEffect,
            "anvil_reforge_same_type_equipped_count");
        if (!unlocked.HasValue ||
            !empty.HasValue ||
            !sameType.HasValue)
        {
            return 0;
        }
        if (unlocked.Value <= 0)
        {
            return -0.2;
        }
        if (empty.Value > 0)
        {
            return 0.05;
        }
        return sameType.Value > 0
            ? -0.02
            : -0.05;
    }

    private static bool ContainsAny(
        string value,
        params string[] needles)
    {
        return Array.Exists(
            needles,
            needle => value.Contains(
                needle,
                StringComparison.Ordinal));
    }

    private static string ReadValue(
        string expectedEffect,
        string key)
    {
        var prefix = key + "=";
        return (expectedEffect ?? string.Empty)
            .Split(';')
            .FirstOrDefault(segment =>
                segment.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            ?[prefix.Length..] ?? string.Empty;
    }

    private static int? ReadInt(
        string expectedEffect,
        string key)
    {
        return int.TryParse(
            ReadValue(expectedEffect, key),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : null;
    }

    private static string Format(double value)
    {
        return Math.Round(value, 8)
            .ToString(
                "0.########",
                CultureInfo.InvariantCulture);
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value)
    {
        return new SmallModelActionParameter
        {
            Name = name,
            Value = value
        };
    }
}

internal readonly record struct
    AnvilReforgeStrategicDemand(
        bool Supported,
        string Status,
        string GoalFamily,
        string CapabilityClass,
        double CapabilityAffinity,
        double LoadoutAdjustment,
        double EffectiveDemandScore,
        string Reason)
{
    internal static AnvilReforgeStrategicDemand
        Blocked =>
            new(
                false,
                "blocked_capability_context_unavailable",
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                string.Empty);
}
