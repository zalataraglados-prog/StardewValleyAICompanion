using StardewValley;
using StardewValley.Objects.Trinkets;
using System.Globalization;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object? ReadAnvilOutcomeRules(
        Trinket trinket,
        string outcomeKind)
    {
        return outcomeKind switch
        {
            "iridium_spur" => new
            {
                stat = "GeneralStat",
                distribution = "discrete_uniform",
                minimum_inclusive = 5,
                maximum_inclusive = 10
            },
            "parrot_egg" =>
                ReadParrotEggOutcomeRules(trinket),
            "frog_egg" => ReadFrogEggOutcomeRules(),
            "fairy_box" => ReadFairyBoxOutcomeRules(),
            "ice_rod" => ReadIceRodOutcomeRules(),
            "magic_quiver" =>
                ReadMagicQuiverOutcomeRules(),
            _ => null
        };
    }

    private static object? ReadParrotEggOutcomeRules(
        Trinket trinket)
    {
        var levelCount = ReadParrotEggLevelCount();
        var currentStat =
            ReadParrotEggCurrentGeneralStat(trinket);
        if (currentStat < 0 ||
            levelCount <= 1 && currentStat == 0)
        {
            return null;
        }

        return new
        {
            stat = "GeneralStat",
            distribution = "discrete_uniform",
            minimum_inclusive = 0,
            maximum_inclusive = levelCount - 1,
            level_count = levelCount,
            current_general_stat = currentStat,
            displayed_level = currentStat + 1,
            total_money_earned =
                Game1.player.totalMoneyEarned,
            output_callback_returns_item =
                levelCount > 1 || currentStat != 0,
            native_level_count_formula =
                "min(4, floor(1 + totalMoneyEarned / 750000))"
        };
    }

    private static object ReadFrogEggOutcomeRules()
    {
        return new
        {
            stat = "Variant",
            distribution = "categorical",
            probabilities = new[]
            {
                new { value = 0, probability = 0.2 + 0.8 * 0.8 / 3 },
                new { value = 1, probability = 0.8 * 0.8 / 3 },
                new { value = 2, probability = 0.8 * 0.8 / 3 },
                new { value = 3, probability = 0.8 * 0.2 * 0.8 / 3 },
                new { value = 4, probability = 0.8 * 0.2 * 0.8 / 3 },
                new { value = 5, probability = 0.8 * 0.2 * 0.8 / 3 },
                new { value = 6, probability = 0.8 * 0.2 * 0.2 / 2 },
                new { value = 7, probability = 0.8 * 0.2 * 0.2 / 2 }
            }
        };
    }

    private static object ReadFairyBoxOutcomeRules()
    {
        var tier4Probability =
            0.55 * 0.75 * 0.125;
        var tier5Probability =
            0.55 * 0.75 * 0.875 * 0.0675;
        var tier1Probability = 1 -
            0.45 -
            0.55 * 0.25 -
            tier4Probability -
            tier5Probability;
        return new
        {
            stat = "fairy_power_tier",
            distribution = "categorical",
            probabilities = new[]
            {
                ReadFairyBoxTier(1, tier1Probability),
                ReadFairyBoxTier(2, 0.45),
                ReadFairyBoxTier(3, 0.55 * 0.25),
                ReadFairyBoxTier(4, tier4Probability),
                ReadFairyBoxTier(5, tier5Probability)
            }
        };
    }

    private static object ReadFairyBoxTier(
        int tier,
        double probability)
    {
        return new
        {
            value = tier,
            probability,
            heal_delay_milliseconds =
                5000 - tier * 300,
            power = 0.7 + tier * 0.1
        };
    }

    private static object ReadIceRodOutcomeRules()
    {
        return new
        {
            distribution =
                "sequential_discrete_uniform_with_override",
            raw_projectile_delay_milliseconds = new
            {
                minimum_inclusive = 3000,
                maximum_inclusive = 5000
            },
            raw_freeze_time_milliseconds = new
            {
                minimum_inclusive = 2000,
                maximum_inclusive = 4000
            },
            perfect_override = new
            {
                probability = 0.05,
                projectile_delay_milliseconds = 3000,
                freeze_time_milliseconds = 4000
            }
        };
    }

    private static object ReadMagicQuiverOutcomeRules()
    {
        return new
        {
            distribution =
                "categorical_branch_then_discrete_uniform_stats",
            branches = new object[]
            {
                new
                {
                    branch = "perfect",
                    probability = 0.04,
                    min_damage = 30,
                    max_damage = 35,
                    projectile_delay_milliseconds = 900
                },
                ReadMagicQuiverRange(
                    "rapid",
                    0.96 * 0.1 * 0.5,
                    8,
                    12,
                    600,
                    700,
                    10),
                ReadMagicQuiverRange(
                    "heavy",
                    0.96 * 0.1 * 0.5,
                    23,
                    38,
                    1500,
                    2000,
                    100),
                ReadMagicQuiverRange(
                    "ordinary",
                    0.96 * 0.9,
                    13,
                    28,
                    1100,
                    2100,
                    100)
            }
        };
    }

    private static object ReadMagicQuiverRange(
        string branch,
        double probability,
        int minDamageMinimum,
        int minDamageMaximum,
        int delayMinimum,
        int delayMaximum,
        int delayStep)
    {
        return new
        {
            branch,
            probability,
            min_damage_minimum_inclusive =
                minDamageMinimum,
            min_damage_maximum_inclusive =
                minDamageMaximum,
            max_damage_offset = 5,
            projectile_delay_minimum_inclusive =
                delayMinimum,
            projectile_delay_maximum_inclusive =
                delayMaximum,
            projectile_delay_step = delayStep
        };
    }

    private static string ReadAnvilOutcomeKind(
        Trinket trinket)
    {
        if (trinket.ItemId == "IridiumSpur")
        {
            return "iridium_spur";
        }
        if (trinket.ItemId == "ParrotEgg")
        {
            return "parrot_egg";
        }

        var effectClass =
            trinket.GetTrinketData()
                ?.TrinketEffectClass ??
            string.Empty;
        if (effectClass.Contains(
                "CompanionTrinketEffect",
                StringComparison.Ordinal))
        {
            return "frog_egg";
        }
        if (effectClass.Contains(
                "FairyBoxTrinketEffect",
                StringComparison.Ordinal))
        {
            return "fairy_box";
        }
        if (effectClass.Contains(
                "IceOrbTrinketEffect",
                StringComparison.Ordinal))
        {
            return "ice_rod";
        }
        if (effectClass.Contains(
                "MagicQuiverTrinketEffect",
                StringComparison.Ordinal))
        {
            return "magic_quiver";
        }
        return string.Empty;
    }

    private static int ReadParrotEggLevelCount()
    {
        return Math.Min(
            4,
            (int)(1 +
                Game1.player.totalMoneyEarned /
                750000));
    }

    private static int ReadParrotEggCurrentGeneralStat(
        Trinket trinket)
    {
        var displayedLevel =
            ReadAnvilFirstIntegerDescriptionValue(
                trinket);
        return displayedLevel is >= 1 and <= 4
            ? displayedLevel - 1
            : -1;
    }

    private static int
        ReadAnvilFirstIntegerDescriptionValue(
            Trinket trinket)
    {
        var text = trinket
            .descriptionSubstitutionTemplates
            .FirstOrDefault();
        return int.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : -1;
    }
}
