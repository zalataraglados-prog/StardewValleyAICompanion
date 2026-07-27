using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

internal static class AnvilReforgeUtilityProjection
{
    internal const string Status =
        "exact_decompiled_mechanical_utility_distribution";

    internal static AnvilReforgeUtility Read(
        JsonElement predictedOutput)
    {
        if (!predictedOutput.TryGetProperty(
                "input",
                out var input) ||
            input.ValueKind != JsonValueKind.Object ||
            !input.TryGetProperty(
                "current_outcome",
                out var current) ||
            current.ValueKind != JsonValueKind.Object ||
            !predictedOutput.TryGetProperty(
                "outcome_rules",
                out var rules) ||
            rules.ValueKind != JsonValueKind.Object)
        {
            return AnvilReforgeUtility.Blocked;
        }

        var outcomeKind = ReadString(
            predictedOutput,
            "outcome_kind");
        return outcomeKind switch
        {
            "iridium_spur" => MonotonicInteger(
                "critical_hit_speed_buff_duration",
                ReadInt(current, "value"),
                ReadInt(rules, "minimum_inclusive"),
                ReadInt(rules, "maximum_inclusive")),
            "parrot_egg" => MonotonicInteger(
                "kill_gold_coin_probability_level",
                ReadInt(current, "value"),
                ReadInt(rules, "minimum_inclusive"),
                ReadInt(rules, "maximum_inclusive")),
            "frog_egg" => MechanicalEquivalence(
                current),
            "fairy_box" => FairyBox(
                current,
                rules),
            "ice_rod" => IceRod(
                current,
                rules),
            "magic_quiver" => MagicQuiver(
                current,
                rules),
            _ => AnvilReforgeUtility.Blocked
        };
    }

    internal static string Format(double value)
    {
        return Math.Round(value, 8)
            .ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static AnvilReforgeUtility
        MonotonicInteger(
            string metricId,
            int current,
            int minimum,
            int maximum)
    {
        if (minimum > maximum ||
            current < minimum ||
            current > maximum)
        {
            return AnvilReforgeUtility.Blocked;
        }

        var count = maximum - minimum + 1;
        var samples = Enumerable
            .Range(minimum, count)
            .Select(value => new UtilitySample(
                Normalize(
                    value,
                    minimum,
                    maximum),
                1d / count));
        return Build(
            metricId,
            Normalize(current, minimum, maximum),
            samples,
            "higher_is_better");
    }

    private static AnvilReforgeUtility
        MechanicalEquivalence(
            JsonElement current)
    {
        var variant = ReadInt(current, "value");
        if (variant is < 0 or > 7)
        {
            return AnvilReforgeUtility.Blocked;
        }

        return new AnvilReforgeUtility(
            true,
            Status,
            "frog_variant_mechanically_equivalent",
            "unordered_cosmetic_variant",
            0,
            0,
            0,
            1,
            0,
            0,
            "mechanically_neutral");
    }

    private static AnvilReforgeUtility FairyBox(
        JsonElement current,
        JsonElement rules)
    {
        var tier = ReadInt(current, "value");
        if (tier is < 1 or > 5 ||
            !rules.TryGetProperty(
                "probabilities",
                out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return AnvilReforgeUtility.Blocked;
        }

        var samples = new List<UtilitySample>();
        foreach (var entry in entries.EnumerateArray())
        {
            var value = ReadInt(entry, "value");
            var probability = ReadDouble(
                entry,
                "probability");
            if (value is < 1 or > 5 ||
                !IsFinite(probability) ||
                probability < 0)
            {
                return AnvilReforgeUtility.Blocked;
            }
            samples.Add(new UtilitySample(
                Normalize(value, 1, 5),
                probability));
        }

        return Build(
            "healing_power_and_interval_tier",
            Normalize(tier, 1, 5),
            samples,
            "higher_tier_increases_power_and_reduces_delay");
    }

    private static AnvilReforgeUtility IceRod(
        JsonElement current,
        JsonElement rules)
    {
        var currentDelay = ReadInt(
            current,
            "projectile_delay_milliseconds");
        var currentFreeze = ReadInt(
            current,
            "freeze_time_milliseconds");
        if (currentDelay is < 3000 or > 5000 ||
            currentFreeze is < 2000 or > 4000 ||
            !rules.TryGetProperty(
                "perfect_override",
                out var perfect))
        {
            return AnvilReforgeUtility.Blocked;
        }

        const int delayMinimum = 3000;
        const int delayMaximum = 5000;
        const int freezeMinimum = 2000;
        const int freezeMaximum = 4000;
        var perfectProbability = ReadDouble(
            perfect,
            "probability");
        var perfectDelay = ReadInt(
            perfect,
            "projectile_delay_milliseconds");
        var perfectFreeze = ReadInt(
            perfect,
            "freeze_time_milliseconds");
        if (!IsFinite(perfectProbability) ||
            perfectProbability is < 0 or > 1 ||
            perfectDelay <= 0 ||
            perfectFreeze <= 0)
        {
            return AnvilReforgeUtility.Blocked;
        }

        var rawCount = checked(
            (delayMaximum - delayMinimum + 1) *
            (freezeMaximum - freezeMinimum + 1));
        long better = 0;
        long equal = 0;
        for (var delay = delayMinimum;
             delay <= delayMaximum;
             delay++)
        {
            var threshold =
                (long)currentFreeze * delay;
            var firstBetter =
                (int)(threshold / currentDelay) + 1;
            better += Math.Max(
                0,
                freezeMaximum -
                Math.Max(freezeMinimum, firstBetter) +
                1);
            if (threshold % currentDelay == 0)
            {
                var equalFreeze =
                    threshold / currentDelay;
                if (equalFreeze is >= freezeMinimum
                    and <= freezeMaximum)
                {
                    equal++;
                }
            }
        }

        var worse = rawCount - better - equal;
        var rawProbability =
            1 - perfectProbability;
        var betterProbability =
            rawProbability * better / rawCount;
        var equalProbability =
            rawProbability * equal / rawCount;
        var worseProbability =
            rawProbability * worse / rawCount;
        var perfectComparison =
            (long)perfectFreeze * currentDelay -
            (long)currentFreeze * perfectDelay;
        if (perfectComparison > 0)
        {
            betterProbability += perfectProbability;
        }
        else if (perfectComparison == 0)
        {
            equalProbability += perfectProbability;
        }
        else
        {
            worseProbability += perfectProbability;
        }

        var reciprocalDelayMean = Enumerable
            .Range(
                delayMinimum,
                delayMaximum -
                delayMinimum + 1)
            .Average(delay => 1d / delay);
        var rawRatio =
            (freezeMinimum + freezeMaximum) /
            2d * reciprocalDelayMean;
        var perfectRatio =
            (double)perfectFreeze / perfectDelay;
        var expectedRatio =
            rawProbability * rawRatio +
            perfectProbability * perfectRatio;
        var minimumRatio =
            (double)freezeMinimum / delayMaximum;
        var maximumRatio =
            (double)freezeMaximum / delayMinimum;
        var currentUtility = Normalize(
            (double)currentFreeze / currentDelay,
            minimumRatio,
            maximumRatio);
        var expectedUtility = Normalize(
            expectedRatio,
            minimumRatio,
            maximumRatio);
        return BuildKnownProbabilities(
            "freeze_duration_per_projectile_interval",
            currentUtility,
            expectedUtility,
            betterProbability,
            equalProbability,
            worseProbability,
            "higher_control_uptime_proxy_is_better");
    }

    private static AnvilReforgeUtility MagicQuiver(
        JsonElement current,
        JsonElement rules)
    {
        var currentMinimumDamage =
            ReadInt(current, "min_damage");
        var currentMaximumDamage =
            ReadInt(current, "max_damage");
        var currentDelay = ReadInt(
            current,
            "projectile_delay_milliseconds");
        if (currentMinimumDamage <= 0 ||
            currentMaximumDamage <
                currentMinimumDamage ||
            currentDelay <= 0 ||
            !rules.TryGetProperty(
                "branches",
                out var branches) ||
            branches.ValueKind != JsonValueKind.Array)
        {
            return AnvilReforgeUtility.Blocked;
        }

        const double minimumRate =
            15.5d * 1000d / 2100d;
        const double maximumRate =
            32.5d * 1000d / 900d;
        var currentRate =
            (currentMinimumDamage +
             currentMaximumDamage) /
            2d * 1000d / currentDelay;
        var samples = new List<UtilitySample>();
        foreach (var branch in
                 branches.EnumerateArray())
        {
            var probability = ReadDouble(
                branch,
                "probability");
            if (!IsFinite(probability) ||
                probability < 0)
            {
                return AnvilReforgeUtility.Blocked;
            }

            if (branch.TryGetProperty(
                    "min_damage",
                    out _))
            {
                var minimumDamage = ReadInt(
                    branch,
                    "min_damage");
                var maximumDamage = ReadInt(
                    branch,
                    "max_damage");
                var delay = ReadInt(
                    branch,
                    "projectile_delay_milliseconds");
                if (minimumDamage <= 0 ||
                    maximumDamage < minimumDamage ||
                    delay <= 0)
                {
                    return AnvilReforgeUtility.Blocked;
                }
                samples.Add(new UtilitySample(
                    Normalize(
                        (minimumDamage +
                         maximumDamage) /
                        2d * 1000d / delay,
                        minimumRate,
                        maximumRate),
                    probability));
                continue;
            }

            var damageMinimum = ReadInt(
                branch,
                "min_damage_minimum_inclusive");
            var damageMaximum = ReadInt(
                branch,
                "min_damage_maximum_inclusive");
            var damageOffset = ReadInt(
                branch,
                "max_damage_offset");
            var delayMinimum = ReadInt(
                branch,
                "projectile_delay_minimum_inclusive");
            var delayMaximum = ReadInt(
                branch,
                "projectile_delay_maximum_inclusive");
            var delayStep = ReadInt(
                branch,
                "projectile_delay_step");
            if (damageMinimum > damageMaximum ||
                damageOffset < 0 ||
                delayMinimum <= 0 ||
                delayMinimum > delayMaximum ||
                delayStep <= 0 ||
                (delayMaximum - delayMinimum) %
                    delayStep != 0)
            {
                return AnvilReforgeUtility.Blocked;
            }

            var damageCount =
                damageMaximum -
                damageMinimum + 1;
            var delayCount =
                (delayMaximum -
                 delayMinimum) /
                delayStep + 1;
            var sampleProbability =
                probability /
                damageCount /
                delayCount;
            for (var minimumDamage =
                     damageMinimum;
                 minimumDamage <= damageMaximum;
                 minimumDamage++)
            {
                for (var delay = delayMinimum;
                     delay <= delayMaximum;
                     delay += delayStep)
                {
                    var maximumDamage =
                        minimumDamage +
                        damageOffset;
                    samples.Add(new UtilitySample(
                        Normalize(
                            (minimumDamage +
                             maximumDamage) /
                            2d * 1000d / delay,
                            minimumRate,
                            maximumRate),
                        sampleProbability));
                }
            }
        }

        return Build(
            "expected_projectile_damage_per_second",
            Normalize(
                currentRate,
                minimumRate,
                maximumRate),
            samples,
            "higher_direct_projectile_damage_rate_is_better");
    }

    private static AnvilReforgeUtility Build(
        string metricId,
        double currentUtility,
        IEnumerable<UtilitySample> samples,
        string ordering)
    {
        var rows = samples.ToArray();
        var probabilitySum =
            rows.Sum(row => row.Probability);
        if (rows.Length == 0 ||
            !IsFinite(probabilitySum) ||
            Math.Abs(probabilitySum - 1) > 0.000001 ||
            rows.Any(row =>
                !IsFinite(row.Probability) ||
                !IsFinite(row.Utility) ||
                row.Probability < 0 ||
                row.Utility is < 0 or > 1))
        {
            return AnvilReforgeUtility.Blocked;
        }

        var expected = rows.Sum(row =>
            row.Utility * row.Probability);
        const double epsilon = 0.000000001;
        return BuildKnownProbabilities(
            metricId,
            currentUtility,
            expected,
            rows.Where(row =>
                    row.Utility >
                    currentUtility + epsilon)
                .Sum(row => row.Probability),
            rows.Where(row =>
                    Math.Abs(
                        row.Utility -
                        currentUtility) <= epsilon)
                .Sum(row => row.Probability),
            rows.Where(row =>
                    row.Utility <
                    currentUtility - epsilon)
                .Sum(row => row.Probability),
            ordering);
    }

    private static AnvilReforgeUtility
        BuildKnownProbabilities(
            string metricId,
            double currentUtility,
            double expectedUtility,
            double improvementProbability,
            double equalProbability,
            double degradationProbability,
            string ordering)
    {
        var probabilitySum =
            improvementProbability +
            equalProbability +
            degradationProbability;
        if (!IsFinite(currentUtility) ||
            !IsFinite(expectedUtility) ||
            !IsFinite(improvementProbability) ||
            !IsFinite(equalProbability) ||
            !IsFinite(degradationProbability) ||
            currentUtility is < 0 or > 1 ||
            expectedUtility is < 0 or > 1 ||
            improvementProbability is < 0 or > 1 ||
            equalProbability is < 0 or > 1 ||
            degradationProbability is < 0 or > 1 ||
            Math.Abs(probabilitySum - 1) > 0.000001)
        {
            return AnvilReforgeUtility.Blocked;
        }

        var delta =
            expectedUtility - currentUtility;
        return new AnvilReforgeUtility(
            true,
            Status,
            metricId,
            ordering,
            currentUtility,
            expectedUtility,
            improvementProbability,
            equalProbability,
            degradationProbability,
            delta,
            Math.Abs(delta) <= 0.000000001
                ? "expected_neutral"
                : delta > 0
                    ? "expected_improvement"
                    : "expected_degradation");
    }

    private static double Normalize(
        double value,
        double minimum,
        double maximum)
    {
        if (maximum <= minimum)
        {
            return 0;
        }
        return Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) &&
            !double.IsInfinity(value);
    }

    private static string ReadString(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                    property,
                    out var value) &&
                value.ValueKind ==
                    JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                    property,
                    out var value) &&
                value.TryGetInt32(out var parsed)
            ? parsed
            : int.MinValue;
    }

    private static double ReadDouble(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                    property,
                    out var value) &&
                value.TryGetDouble(out var parsed)
            ? parsed
            : double.NaN;
    }

    private readonly record struct UtilitySample(
        double Utility,
        double Probability);
}

internal readonly record struct AnvilReforgeUtility(
    bool Supported,
    string Status,
    string MetricId,
    string Ordering,
    double CurrentUtility,
    double ExpectedUtility,
    double ImprovementProbability,
    double EqualProbability,
    double DegradationProbability,
    double ExpectedDelta,
    string DecisionClass)
{
    internal static AnvilReforgeUtility Blocked =>
        new(
            false,
            "blocked",
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            0,
            "blocked");
}
