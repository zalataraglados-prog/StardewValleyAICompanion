using StardewValley;
using StardewValley.Objects.Trinkets;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object ReadAnvilCurrentOutcomeState(
        Trinket trinket,
        string outcomeKind)
    {
        if (outcomeKind == "parrot_egg")
        {
            var currentStat =
                ReadParrotEggCurrentGeneralStat(
                    trinket);
            return new
            {
                stat = "GeneralStat",
                value = currentStat,
                displayed_level =
                    currentStat < 0
                        ? (int?)null
                        : currentStat + 1,
                source =
                    "live_description_substitution_template"
            };
        }

        var random = Utility.CreateRandom(
            trinket.generationSeed.Value);
        return outcomeKind switch
        {
            "iridium_spur" => new
            {
                stat = "GeneralStat",
                value = random.Next(5, 11)
            },
            "frog_egg" =>
                ReadCurrentFrogEggOutcome(random),
            "fairy_box" =>
                ReadCurrentFairyBoxOutcome(random),
            "ice_rod" =>
                ReadCurrentIceRodOutcome(random),
            "magic_quiver" =>
                ReadCurrentMagicQuiverOutcome(random),
            _ => new
            {
                status =
                    "blocked_unvetted_trinket_effect"
            }
        };
    }

    private static object ReadCurrentFrogEggOutcome(
        Random random)
    {
        int variant;
        if (random.NextDouble() < 0.2)
        {
            variant = 0;
        }
        else if (random.NextDouble() < 0.8)
        {
            variant = random.Next(3);
        }
        else if (random.NextDouble() < 0.8)
        {
            variant = random.Next(3) + 3;
        }
        else
        {
            variant = random.Next(2) + 6;
        }
        return new
        {
            stat = "Variant",
            value = variant
        };
    }

    private static object ReadCurrentFairyBoxOutcome(
        Random random)
    {
        var tier = 1;
        if (random.NextDouble() < 0.45)
        {
            tier = 2;
        }
        else if (random.NextDouble() < 0.25)
        {
            tier = 3;
        }
        else if (random.NextDouble() < 0.125)
        {
            tier = 4;
        }
        else if (random.NextDouble() < 0.0675)
        {
            tier = 5;
        }
        return ReadFairyBoxTier(
            tier,
            probability: 1);
    }

    private static object ReadCurrentIceRodOutcome(
        Random random)
    {
        var projectileDelay =
            random.Next(3000, 5001);
        var freezeTime =
            random.Next(2000, 4001);
        var perfect =
            random.NextDouble() < 0.05;
        if (perfect)
        {
            projectileDelay = 3000;
            freezeTime = 4000;
        }
        return new
        {
            projectile_delay_milliseconds =
                projectileDelay,
            freeze_time_milliseconds =
                freezeTime,
            perfect_override = perfect
        };
    }

    private static object ReadCurrentMagicQuiverOutcome(
        Random random)
    {
        if (random.NextDouble() < 0.04)
        {
            return new
            {
                branch = "perfect",
                min_damage = 30,
                max_damage = 35,
                projectile_delay_milliseconds =
                    900
            };
        }

        if (random.NextDouble() < 0.1)
        {
            if (random.NextDouble() < 0.5)
            {
                var minDamage =
                    random.Next(10, 15) - 2;
                return new
                {
                    branch = "rapid",
                    min_damage = minDamage,
                    max_damage = minDamage + 5,
                    projectile_delay_milliseconds =
                        600 + random.Next(11) * 10
                };
            }

            var heavyMinDamage =
                random.Next(25, 41) - 2;
            return new
            {
                branch = "heavy",
                min_damage = heavyMinDamage,
                max_damage = heavyMinDamage + 5,
                projectile_delay_milliseconds =
                    1500 + random.Next(6) * 100
            };
        }

        var ordinaryMinDamage =
            random.Next(15, 31) - 2;
        return new
        {
            branch = "ordinary",
            min_damage = ordinaryMinDamage,
            max_damage = ordinaryMinDamage + 5,
            projectile_delay_milliseconds =
                1100 + random.Next(11) * 100
        };
    }
}
