namespace StardewAI.Backend.Tests;

public sealed class RuntimeFruitTreeExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeFruitTreeInteractionAndGroupedOutputReceipts()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartFruitTreeHarvest(pending);", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("ProjectFruitTreeHarvest", source);
        Assert.Contains("CountFruitTreeOutput", source);
        Assert.Contains("harvest_fruit_tree_native_contract_mismatch", source);
        Assert.Contains("active.Tree.fruit.Count == 0", source);
        Assert.DoesNotContain("active.Tree.fruit.Clear()", source);
        Assert.DoesNotContain("active.Tree.shake(", source);
    }
}
