using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects.Trinkets;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool TryReadAnvilReforgeFeedback(
        TrainingExecutionRequest request,
        Item item,
        out AnvilReforgeFeedback feedback,
        out string reason)
    {
        feedback = default;
        reason = string.Empty;
        if (item is not Trinket trinket)
        {
            reason = "anvil_reforge_item_not_trinket";
            return false;
        }

        var effect = trinket.GetEffect();
        var kind =
            request.MachineOutputDistributionOutcomeKind;
        double utility;
        object outcome;
        string metric;
        switch (kind)
        {
            case "iridium_spur":
                if (trinket.ItemId != "IridiumSpur" ||
                    effect is null ||
                    effect.GeneralStat is < 5 or > 10)
                {
                    reason =
                        "anvil_reforge_iridium_spur_state_invalid";
                    return false;
                }
                metric =
                    "critical_hit_speed_buff_duration";
                utility =
                    Normalize(
                        effect.GeneralStat,
                        5,
                        10);
                outcome = new
                {
                    stat = "GeneralStat",
                    value = effect.GeneralStat,
                    duration_milliseconds =
                        effect.GeneralStat * 1000
                };
                break;
            case "parrot_egg":
                if (trinket.ItemId != "ParrotEgg")
                {
                    reason =
                        "anvil_reforge_parrot_egg_item_invalid";
                    return false;
                }
                var levelCount = Math.Min(
                    4,
                    (int)(1 +
                        Game1.player.totalMoneyEarned /
                        750000));
                if (effect is null ||
                    levelCount <= 1 ||
                    effect.GeneralStat < 0 ||
                    effect.GeneralStat >= levelCount)
                {
                    reason =
                        "anvil_reforge_parrot_egg_state_invalid";
                    return false;
                }
                metric =
                    "kill_gold_coin_probability_level";
                utility = Normalize(
                    effect.GeneralStat,
                    0,
                    levelCount - 1);
                outcome = new
                {
                    stat = "GeneralStat",
                    value = effect.GeneralStat,
                    displayed_level =
                        effect.GeneralStat + 1,
                    gold_coin_trigger_probability =
                        (effect.GeneralStat + 1) *
                        0.1
                };
                break;
            case "frog_egg":
                if (effect is not
                    CompanionTrinketEffect frog ||
                    frog.Variant is < 0 or > 7)
                {
                    reason =
                        "anvil_reforge_frog_state_invalid";
                    return false;
                }
                metric =
                    "frog_variant_mechanically_equivalent";
                utility = 0;
                outcome = new
                {
                    stat = "Variant",
                    value = frog.Variant,
                    mechanical_ordering =
                        "unordered_cosmetic_variant"
                };
                break;
            case "fairy_box":
                if (effect is not
                    FairyBoxTrinketEffect fairy ||
                    fairy.HealDelay is < 3500 or > 4700 ||
                    fairy.Power is < 0.8f or > 1.2f)
                {
                    reason =
                        "anvil_reforge_fairy_state_invalid";
                    return false;
                }
                var fairyTier = (int)Math.Round(
                    (fairy.Power - 0.7f) / 0.1f);
                if (fairyTier is < 1 or > 5)
                {
                    reason =
                        "anvil_reforge_fairy_tier_invalid";
                    return false;
                }
                metric =
                    "healing_power_and_interval_tier";
                utility = Normalize(
                    fairyTier,
                    1,
                    5);
                outcome = new
                {
                    value = fairyTier,
                    heal_delay_milliseconds =
                        fairy.HealDelay,
                    power = fairy.Power
                };
                break;
            case "ice_rod":
                if (effect is not
                    IceOrbTrinketEffect ice ||
                    ice.ProjectileDelay is < 3000 or > 5000 ||
                    ice.FreezeTime is < 2000 or > 4000)
                {
                    reason =
                        "anvil_reforge_ice_rod_state_invalid";
                    return false;
                }
                metric =
                    "freeze_duration_per_projectile_interval";
                utility = Normalize(
                    (double)ice.FreezeTime /
                    ice.ProjectileDelay,
                    2000d / 5000d,
                    4000d / 3000d);
                outcome = new
                {
                    projectile_delay_milliseconds =
                        ice.ProjectileDelay,
                    freeze_time_milliseconds =
                        ice.FreezeTime,
                    control_uptime_proxy =
                        (double)ice.FreezeTime /
                        ice.ProjectileDelay
                };
                break;
            case "magic_quiver":
                if (effect is not
                    MagicQuiverTrinketEffect quiver ||
                    quiver.MinDamage <= 0 ||
                    quiver.MaxDamage <
                        quiver.MinDamage ||
                    quiver.ProjectileDelay <= 0)
                {
                    reason =
                        "anvil_reforge_magic_quiver_state_invalid";
                    return false;
                }
                metric =
                    "expected_projectile_damage_per_second";
                var damageRate =
                    (quiver.MinDamage +
                     quiver.MaxDamage) /
                    2d * 1000d /
                    quiver.ProjectileDelay;
                utility = Normalize(
                    damageRate,
                    15.5d * 1000d / 2100d,
                    32.5d * 1000d / 900d);
                outcome = new
                {
                    min_damage = quiver.MinDamage,
                    max_damage = quiver.MaxDamage,
                    projectile_delay_milliseconds =
                        quiver.ProjectileDelay,
                    expected_projectile_damage_per_second =
                        damageRate
                };
                break;
            default:
                reason =
                    "anvil_reforge_outcome_kind_unrecognized";
                return false;
        }

        feedback = new AnvilReforgeFeedback(
            metric,
            utility,
            JsonSerializer.Serialize(
                outcome,
                JsonOptions));
        return true;
    }

    private static bool AnvilUtilityMatches(
        double? expected,
        double actual)
    {
        return expected.HasValue &&
            double.IsFinite(expected.Value) &&
            Math.Abs(expected.Value - actual) <=
                0.00000001;
    }

    private static bool AnvilDistributionRequestIsValid(
        TrainingExecutionRequest request)
    {
        return
            request.AnvilReforgeCurrentUtility
                is >= 0 and <= 1 &&
            request.AnvilReforgeExpectedUtility
                is >= 0 and <= 1 &&
            request.AnvilReforgeExpectedUtilityDelta
                is >= -1 and <= 1 &&
            request.AnvilReforgeImprovementProbability
                is >= 0 and <= 1 &&
            double.IsFinite(
                request.AnvilReforgeCurrentUtility.Value) &&
            double.IsFinite(
                request.AnvilReforgeExpectedUtility.Value) &&
            double.IsFinite(
                request.AnvilReforgeExpectedUtilityDelta.Value) &&
            double.IsFinite(
                request.AnvilReforgeImprovementProbability.Value) &&
            Math.Abs(
                request.AnvilReforgeExpectedUtility.Value -
                request.AnvilReforgeCurrentUtility.Value -
                request.AnvilReforgeExpectedUtilityDelta.Value) <=
                0.00000001;
    }

    private static double Normalize(
        double value,
        double minimum,
        double maximum)
    {
        return Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);
    }

    private readonly record struct AnvilReforgeFeedback(
        string Metric,
        double Utility,
        string OutcomeJson)
    {
        internal string UtilityText =>
            Math.Round(Utility, 8)
                .ToString(
                    "0.########",
                    CultureInfo.InvariantCulture);
    }
}
