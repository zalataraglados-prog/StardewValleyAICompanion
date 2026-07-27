using StardewAI.TransparentBridge.Adapters;
using System.Reflection;

namespace StardewAI.Core.Tests;

public sealed class
    RandomSpecialMachinePredictionSourceGuardTests
{
    [Fact]
    public void GeodeCrusherUsesOnlyVettedDeterministicReplay()
    {
        var source = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.GeodeCrusherPrediction.cs");
        var dispatch = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.SpecialMachinePrediction.cs");
        var machines = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.Machines.cs");

        Assert.Contains(
            "geode_crusher_day_save_counter_rng.v1",
            source);
        Assert.Contains("(BC)182", source);
        Assert.Contains(
            ": OutputGeodeCrusher",
            source);
        Assert.Contains("(O)535", source);
        Assert.Contains("(O)536", source);
        Assert.Contains("(O)537", source);
        Assert.Contains("(O)749", source);
        Assert.Contains(
            "Utility.IsGeode(",
            source);
        Assert.Contains(
            "Utility.getTreasureFromGeode(inputItem)",
            source);
        Assert.Contains(
            "Game1.stats.GeodesCracked",
            source);
        Assert.Contains(
            "ExactMachinePredictionStatus",
            source);
        Assert.Contains(
            "exclude_MysteryBox_mail_mutation_branch",
            source);
        Assert.DoesNotContain(
            "OutputGeodeCrusher(",
            source);
        Assert.DoesNotContain(
            "Game1.random.",
            source);

        Assert.Contains(
            "TryReadGeodeCrusherPrediction(",
            dispatch);
        Assert.Contains(
            "IsVettedGeodeCrusherInputSupported(",
            dispatch);
        Assert.Contains(
            "VettedSpecialMachineInputPassesCallbackPreconditions(",
            machines);
    }

    [Fact]
    public void AnvilExposesRulesWithoutReadingSharedRng()
    {
        var prediction = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.AnvilPrediction.cs");
        var distribution = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.AnvilDistribution.cs");
        var currentState = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.AnvilCurrentState.cs");
        var trinketState = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.TrinketState.cs");
        var itemReflection = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.ItemReflection.cs");
        var combined = prediction +
            distribution +
            currentState +
            trinketState;

        Assert.Contains(
            "anvil_trinket_reforge_distribution.v1",
            prediction);
        Assert.Contains("(BC)Anvil", prediction);
        Assert.Contains(": OutputAnvil", prediction);
        Assert.Contains("(O)337", prediction);
        Assert.Contains("required_count = 3", prediction);
        Assert.Contains(
            "effective_minutes_until_ready = 10",
            prediction);
        Assert.Contains(
            "complete_vanilla_generative_rules",
            prediction);
        Assert.Contains(
            "blocked_shared_Game1_random_Next_9999999",
            prediction);
        Assert.Contains(
            "displayedLevel - 1",
            distribution);
        Assert.Contains(
            "levelCount <= 1 && currentStat == 0",
            distribution);
        Assert.Contains(
            "totalMoneyEarned /",
            distribution);
        Assert.Contains(
            "750000",
            distribution);
        Assert.Contains(
            "minimum_inclusive = 5",
            distribution);
        Assert.Contains(
            "maximum_inclusive = 10",
            distribution);
        Assert.Contains(
            "probability = 0.05",
            distribution);
        Assert.Contains(
            "probability = 0.04",
            distribution);
        Assert.Contains(
            "Utility.CreateRandom(",
            currentState);
        Assert.Contains(
            "trinket_item_state.v1",
            trinketState);
        Assert.Contains(
            "ReadAnvilCurrentOutcomeState(",
            trinketState);
        Assert.Contains(
            "ReadItemSpecialState(item)",
            itemReflection);
        Assert.DoesNotContain("OutputAnvil(", combined);
        Assert.DoesNotContain("RerollStats(", combined);
        Assert.DoesNotContain("Game1.random.", combined);
    }

    [Fact]
    public void AnvilCategoricalBranchesConserveProbability()
    {
        Assert.Equal(
            1,
            ReadProbabilitySum(
                InvokePrivate(
                    "ReadFrogEggOutcomeRules"),
                "probabilities"),
            12);
        Assert.Equal(
            1,
            ReadProbabilitySum(
                InvokePrivate(
                    "ReadFairyBoxOutcomeRules"),
                "probabilities"),
            12);
        Assert.Equal(
            1,
            ReadProbabilitySum(
                InvokePrivate(
                    "ReadMagicQuiverOutcomeRules"),
                "branches"),
            12);
    }

    private static object InvokePrivate(
        string methodName)
    {
        var method = typeof(FarmReadAdapter).GetMethod(
            methodName,
            BindingFlags.NonPublic |
            BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<object>(
            method.Invoke(null, null));
    }

    private static double ReadProbabilitySum(
        object container,
        string collectionProperty)
    {
        var rows = Assert.IsAssignableFrom<
            System.Collections.IEnumerable>(
            container.GetType()
                .GetProperty(collectionProperty)!
                .GetValue(container));
        return rows.Cast<object>()
            .Sum(row => Convert.ToDouble(
                row.GetType()
                    .GetProperty("probability")!
                    .GetValue(row)));
    }

    private static string ReadRepositoryFile(
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
                return File.ReadAllText(candidate);
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
