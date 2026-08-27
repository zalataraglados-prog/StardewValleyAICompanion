using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeSlimeBallExecutorTests
{
    [Fact]
    public void ProductionRuntimeUsesOneNativeActionAndConservedOutputReceipts()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.SlimeBall.cs");

        Assert.Contains("StartSlimeBallCollection(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("IsDestructiveObjectTrap(active.Location, active.Stand)", source);
        Assert.Contains("CountConservedItem(active.Location, SlimeQualifiedItemId)", source);
        Assert.Contains("remaining_debris_deferred_to_shared_pickup_executor", source);
        Assert.DoesNotContain("objects.Remove", source, StringComparison.Ordinal);
        Assert.DoesNotContain("createMultipleObjectDebris", source, StringComparison.Ordinal);
        Assert.DoesNotContain("checkForAction(", source, StringComparison.Ordinal);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("farming.collect_slime_ball"));
    }
}
