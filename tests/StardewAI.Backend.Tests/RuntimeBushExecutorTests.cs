namespace StardewAI.Backend.Tests;

public sealed class RuntimeBushExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeBushInteractionAndObservedBranchDeltas()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartBushHarvest(pending);", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("ProjectBushHarvest", source);
        Assert.Contains("CountBushOutput", source);
        Assert.Contains("collectedNutTracker.Contains(active.NutKey)", source);
        Assert.DoesNotContain("active.Bush.tileSheetOffset.Value = 0", source);
        Assert.DoesNotContain("MarkCollectedNut(active.NutKey)", source);
    }
}
