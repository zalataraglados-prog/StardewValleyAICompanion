using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StardewAI.Contracts.Mining;

public sealed class CalicoStatueEffectDefinition
{
    public CalicoStatueEffectDefinition(
        int effectId,
        string effectKey,
        string strategyPolarity,
        bool canStack,
        int calicoEggReward,
        string exactEffect)
    {
        EffectId = effectId;
        EffectKey = effectKey;
        StrategyPolarity = strategyPolarity;
        CanStack = canStack;
        CalicoEggReward = calicoEggReward;
        ExactEffect = exactEffect;
    }

    public int EffectId { get; }
    public string EffectKey { get; }
    public string StrategyPolarity { get; }
    public bool CanStack { get; }
    public int CalicoEggReward { get; }
    public string ExactEffect { get; }
}

public static class CalicoStatueEffectModel
{
    private static readonly IReadOnlyDictionary<int, CalicoStatueEffectDefinition> Definitions =
        new ReadOnlyDictionary<int, CalicoStatueEffectDefinition>(new[]
        {
            D(0, "ghost_invasion", "negative", false, 0, "adds_carbon_ghost_invasion_spawn_branch"),
            D(1, "serpent_invasion", "negative", false, 0, "adds_serpent_invasion_spawn_branch"),
            D(2, "skeleton_invasion", "negative", false, 0, "adds_dangerous_skeleton_or_bat_invasion_spawn_branch"),
            D(3, "bat_invasion", "negative", false, 0, "adds_bat_invasion_spawn_branch"),
            D(4, "assassin_bugs", "negative", false, 0, "replaces_skull_cavern_bug_spawns_with_assassin_bugs"),
            D(5, "thin_shells", "negative", false, 0, "raises_calico_egg_death_loss_fraction_from_0.2_to_0.5"),
            D(6, "meager_meals", "negative", false, 0, "halves_food_health_and_energy_recovery_minimum_1"),
            D(7, "monster_surge", "negative", true, 0, "multiplies_monster_spawn_chance_by_1_plus_0.2_per_stack"),
            D(8, "sharp_teeth", "negative", true, 0, "multiplies_incoming_damage_by_1_plus_0.25_per_stack"),
            D(9, "mummy_curse", "negative", false, 0, "mummies_gain_dangerous_difficulty_2_and_double_speed"),
            D(10, "speed_boost", "positive", false, 0, "applies_calico_statue_speed_plus_1_in_skull_cavern"),
            D(11, "refresh", "positive", true, 0, "restores_health_and_stamina_to_current_maximum"),
            D(12, "fifty_egg_treasure", "positive", true, 50, "grants_50_calico_eggs_to_inventory_or_debris"),
            D(13, "no_effect", "neutral", true, 0, "no_additional_run_modifier"),
            D(14, "tooth_file", "positive", true, 0, "multiplies_incoming_damage_by_1_minus_0.25_per_stack_before_minimum_1"),
            D(15, "twenty_five_egg_treasure", "positive", true, 25, "grants_25_calico_eggs_to_inventory_or_debris"),
            D(16, "ten_egg_treasure", "positive", true, 10, "grants_10_calico_eggs_to_inventory_or_debris"),
            D(17, "one_hundred_egg_treasure", "positive", true, 100, "grants_100_calico_eggs_to_inventory_or_debris")
        }.ToDictionary(row => row.EffectId));

    public static IReadOnlyCollection<CalicoStatueEffectDefinition> All =>
        Definitions.Values.OrderBy(row => row.EffectId).ToArray();

    public static CalicoStatueEffectDefinition GetRequired(int effectId) =>
        Definitions.TryGetValue(effectId, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(effectId), effectId, "Unknown Calico Statue effect.");

    public static int SelectEffect(
        Random random,
        double averageDailyLuck,
        IReadOnlyDictionary<int, int> currentEffects)
    {
        if (Roll(random, 0.51d + averageDailyLuck))
        {
            return TrySelect(random, currentEffects,
                (0.15d, 10, false),
                (0.01d, 17, true),
                (0.05d, 12, true),
                (0.10d, 15, true),
                (0.20d, 16, true),
                (0.10d, 14, true),
                (0.50d, 11, true)) ?? 13;
        }

        if (Roll(random, 0.20d))
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var invasion = random.Next(4);
                if (!currentEffects.ContainsKey(invasion))
                    return invasion;
            }
        }

        return TrySelect(random, currentEffects,
            (0.10d, 4, false),
            (0.10d, 9, false),
            (0.10d, 5, false),
            (0.10d, 6, false),
            (0.20d, 7, true),
            (0.20d, 8, true)) ?? 13;
    }

    private static int? TrySelect(
        Random random,
        IReadOnlyDictionary<int, int> currentEffects,
        params (double Chance, int EffectId, bool CanStack)[] rows)
    {
        foreach (var row in rows)
        {
            if (Roll(random, row.Chance) && (row.CanStack || !currentEffects.ContainsKey(row.EffectId)))
                return row.EffectId;
        }
        return null;
    }

    private static bool Roll(Random random, double chance) =>
        chance >= 1d || random.NextDouble() < chance;

    private static CalicoStatueEffectDefinition D(
        int id,
        string key,
        string polarity,
        bool canStack,
        int eggReward,
        string exactEffect) =>
        new(id, key, polarity, canStack, eggReward, exactEffect);
}
