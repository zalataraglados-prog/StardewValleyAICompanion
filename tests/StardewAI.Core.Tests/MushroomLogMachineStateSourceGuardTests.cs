using StardewAI.TransparentBridge.Adapters;
using System.Reflection;

namespace StardewAI.Core.Tests;

public sealed class MushroomLogMachineStateSourceGuardTests
{
    [Fact]
    public void MushroomLogPreservesNativeTreeAndRandomBoundaries()
    {
        var stateSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.MushroomLogState.cs"));
        var distributionSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.MushroomLogDistribution.cs"));
        var dispatchSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.SpecialMachinePrediction.cs"));
        var combinedSource =
            stateSource + distributionSource;

        Assert.Contains(
            "mushroom_log_nearby_tree_distribution.v1",
            stateSource);
        Assert.Contains("(BC)MushroomLog", stateSource);
        Assert.Contains(
            ": OutputMushroomLog",
            stateSource);
        Assert.Contains(
            "machine.GetType() != typeof(StardewValley.Object)",
            stateSource);
        Assert.Contains(
            "LocationWeather.TryGetValue(",
            stateSource);
        Assert.Contains(
            "initial_days_until_morning = 3",
            stateSource);
        Assert.Contains(
            "formula_exact_event_time_not_reconstructed_from_post_event_snapshot",
            stateSource);
        Assert.DoesNotContain(
            "rainAccelerationMinutes",
            stateSource);
        Assert.Contains(
            "clears_previous_contents_overnight",
            stateSource);
        Assert.Contains(
            "complete_marginals_for_current_nearby_tree_snapshot",
            stateSource);
        Assert.Contains(
            "blocked_shared_Game1_random_state_not_read",
            stateSource);
        Assert.Contains(
            "existing_held_item_origin_status",
            stateSource);

        Assert.Contains(
            "MushroomLogTreeRadius = 3",
            stateSource);
        Assert.Contains(
            "tree.growthStage.Value >= 5",
            distributionSource);
        Assert.Contains(
            "(int)(allTreeCount * 0.75f)",
            stateSource);
        Assert.Contains(
            "matureMossTreeCount * 0.025f",
            stateSource);
        Assert.Contains(
            "allTreeCount * 0.025f",
            stateSource);
        Assert.Contains(
            "entryCount * 0.1425",
            distributionSource);
        Assert.Contains(
            "entryCount * 0.8075",
            distributionSource);
        Assert.Contains(
            "multiplier <= 2",
            distributionSource);
        Assert.Contains(
            "Math.Clamp(",
            distributionSource);
        Assert.Contains(
            "successChance * successChance *",
            distributionSource);

        Assert.DoesNotContain(
            "Game1.random.",
            combinedSource);
        Assert.DoesNotContain(
            "OutputMushroomLog(",
            combinedSource);
        Assert.DoesNotContain(
            "GetWeatherForLocation(",
            combinedSource);

        Assert.Contains(
            "ReadMushroomLogSpecialState(",
            dispatchSource);
        Assert.Contains(
            "IsVettedMushroomLogOutputMethod(",
            dispatchSource);
    }

    [Fact]
    public void MushroomLogAmountAndQualityMarginalsConserveMass()
    {
        var amountRows = InvokeDistribution(
            "ReadMushroomLogAmountDistribution",
            6);
        Assert.Equal(
            new Dictionary<int, double>
            {
                [3] = 0.5,
                [5] = 0.5
            },
            ReadProbabilityRows(amountRows, "Amount"));

        var emptyTreeRows = InvokeDistribution(
            "ReadMushroomLogAmountDistribution",
            0);
        Assert.Equal(
            new Dictionary<int, double>
            {
                [1] = 1
            },
            ReadProbabilityRows(emptyTreeRows, "Amount"));

        var qualityRows = InvokeDistribution(
            "ReadMushroomLogQualityDistribution",
            0.25);
        var qualities = ReadProbabilityRows(
            qualityRows,
            "Quality");
        Assert.Equal(0.75, qualities[0], 12);
        Assert.Equal(0.1875, qualities[1], 12);
        Assert.Equal(0.046875, qualities[2], 12);
        Assert.Equal(0.015625, qualities[4], 12);
        Assert.Equal(1, qualities.Values.Sum(), 12);
    }

    [Fact]
    public void MushroomLogGenericPoolEntryMatchesSequentialBranches()
    {
        var weights = new Dictionary<string, double>
        {
            ["(O)257"] = 0,
            ["(O)281"] = 0,
            ["(O)404"] = 0,
            ["(O)420"] = 0,
            ["(O)422"] = 0
        };
        var method = typeof(FarmReadAdapter).GetMethod(
            "AddMushroomLogPoolEntryWeights",
            BindingFlags.NonPublic |
            BindingFlags.Static);

        Assert.NotNull(method);
        method.Invoke(
            null,
            new object?[]
            {
                weights,
                null,
                1
            });

        Assert.Equal(0.8075, weights["(O)404"], 12);
        Assert.Equal(0.1425, weights["(O)420"], 12);
        Assert.Equal(0.05, weights["(O)422"], 12);
        Assert.Equal(1, weights.Values.Sum(), 12);
    }

    private static Array InvokeDistribution(
        string methodName,
        object argument)
    {
        var method = typeof(FarmReadAdapter).GetMethod(
            methodName,
            BindingFlags.NonPublic |
            BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Array>(
            method.Invoke(null, new[] { argument }));
    }

    private static Dictionary<int, double>
        ReadProbabilityRows(
            Array rows,
            string keyProperty)
    {
        return rows.Cast<object>().ToDictionary(
            row => (int)(
                row.GetType()
                    .GetProperty(keyProperty)!
                    .GetValue(row) ?? -1),
            row => (double)(
                row.GetType()
                    .GetProperty("Probability")!
                    .GetValue(row) ?? -1.0));
    }

    private static string FindRepositoryFile(
        params string[] segments)
    {
        var current = new DirectoryInfo(
            AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                new[] { current.FullName }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
