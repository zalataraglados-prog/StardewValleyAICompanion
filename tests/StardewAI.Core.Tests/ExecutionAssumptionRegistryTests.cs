using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ExecutionAssumptionRegistryTests
{
    [Fact]
    public void RegistryHasUniqueDomainsAndPerfectHumanProfile()
    {
        var assumptions = new ExecutionAssumptionRegistry().All.ToArray();

        Assert.Equal(assumptions.Length, assumptions.Select(item => item.DomainId).Distinct().Count());
        Assert.All(assumptions, item => Assert.Equal("perfect_human_player", item.Profile));
        Assert.All(assumptions, item => Assert.NotEmpty(item.DecompiledAnchors));
    }

    [Fact]
    public void MiningAndFishingExcludeLowLevelExecutionFailureFromPreferencePenalty()
    {
        var registry = new ExecutionAssumptionRegistry();

        var mining = registry.GetRequired("mining_and_combat");
        var fishing = registry.GetRequired("fishing");

        Assert.Contains("bad_dodging", mining.PreferencePenaltyExclusions);
        Assert.Contains("poor_path_micro", mining.PreferencePenaltyExclusions);
        Assert.Contains("missed_bite", fishing.PreferencePenaltyExclusions);
        Assert.Contains("bad_bobber_control", fishing.PreferencePenaltyExclusions);
        Assert.Contains("ladder_discovery", mining.CalibrationFactors);
        Assert.Contains("fish_difficulty", fishing.CalibrationFactors);
    }
}
