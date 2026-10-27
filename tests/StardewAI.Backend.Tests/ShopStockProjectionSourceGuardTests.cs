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

    [Fact]
    public void AuditedBambooPoleExceptionIsSharedAcrossReadAndExecutionLayers()
    {
        var policy = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.Contracts",
            "Execution",
            "AuditedShopPurchasePolicy.cs"));
        var menu = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MenuReadAdapter.cs"));
        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Shop.cs"));

        Assert.Contains("(T)BambooPole", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("(T)IridiumRod", policy, StringComparison.Ordinal);
        Assert.Contains(
            "AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase",
            ShopAccessReadAdapterSources.All,
            StringComparison.Ordinal);
        Assert.Contains(
            "AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase",
            menu,
            StringComparison.Ordinal);
        Assert.Contains(
            "AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase",
            runtime,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
