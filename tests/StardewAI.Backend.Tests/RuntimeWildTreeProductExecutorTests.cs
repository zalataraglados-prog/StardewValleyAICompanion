namespace StardewAI.Backend.Tests;

public sealed class RuntimeWildTreeProductExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeInteractionAndVerifiesCompleteOutputDomain()
    {
        var source = RuntimeHarnessSources.All;
        Assert.Contains("StartWildTreeProductHarvest(pending);", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("CaptureWildTreeProductOutputs", source);
        Assert.Contains("CaptureWildTreeProductInventoryOutputs", source);
        Assert.Contains("TickWildTreeProductOutputCollection(active);", source);
        Assert.Contains("WildTreeProductInventoryReceiptVerified(active)", source);
        Assert.Contains("native_wild_tree_output_naturally_collected", source);
        Assert.Contains("complete_output_domain_verified_without_rng_prediction", source);
        Assert.Contains("Game1.player.CurrentToolIndex = active.SafeSlotIndex", source);
        Assert.Contains("Game1.player.CurrentToolIndex = active.RestoreSlotIndex", source);
        Assert.DoesNotContain("active.Tree.shake(", source);
        Assert.DoesNotContain("active.Tree.hasSeed.Value = false", source);
    }
}
