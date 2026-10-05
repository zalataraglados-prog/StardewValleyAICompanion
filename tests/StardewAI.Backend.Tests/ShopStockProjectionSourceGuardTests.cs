namespace StardewAI.Backend.Tests;

public sealed class ShopStockProjectionSourceGuardTests
{
    [Fact]
    public void ShopProjectionIsolatesGameplayRandomAndRestoresIt()
    {
        var source = ShopAccessReadAdapterSources.All;

        Assert.Contains("var liveRandom = Game1.random", source, StringComparison.Ordinal);
        Assert.Contains("Game1.random = Utility.CreateDaySaveRandom()", source, StringComparison.Ordinal);
        Assert.Contains("finally", source, StringComparison.Ordinal);
        Assert.Contains("Game1.random = liveRandom", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RandomStockRulesCannotBecomeExactPurchaseCandidates()
    {
        var source = ShopAccessReadAdapterSources.All;

        Assert.Contains("HasRandomStateQuery(item.Condition)", source, StringComparison.Ordinal);
        Assert.Contains("stochastic_projection = stochasticProjection", source, StringComparison.Ordinal);
        Assert.Contains(
            "stochastic_stock_requires_native_menu_recheck",
            source,
            StringComparison.Ordinal);
        Assert.Contains("stochastic_item_rule_ids = stochasticRuleIds", source, StringComparison.Ordinal);
    }
}
