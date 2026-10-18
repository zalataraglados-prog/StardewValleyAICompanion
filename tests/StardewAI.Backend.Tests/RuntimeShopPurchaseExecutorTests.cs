namespace StardewAI.Backend.Tests;

public sealed class RuntimeShopPurchaseExecutorTests
{
    [Fact]
    public void PurchaseFreezesExpectedStackBeforeNativeInventoryInsertion()
    {
        var source = RuntimeHarnessSources.File("ModEntry.Shop.cs");
        var expectedStack = source.IndexOf(
            "var expectedItemIncrease = itemToAdd.Stack;",
            StringComparison.Ordinal);
        var nativeInsertion = source.IndexOf(
            "Game1.player.addItemToInventoryBool(itemToAdd)",
            StringComparison.Ordinal);

        Assert.True(expectedStack >= 0);
        Assert.True(nativeInsertion > expectedStack);
        Assert.Equal(1, source.Split(
            "var expectedItemIncrease = itemToAdd.Stack;",
            StringSplitOptions.None).Length - 1);
    }
}
