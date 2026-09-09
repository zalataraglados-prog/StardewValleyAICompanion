namespace StardewAI.Core.Tests;

public sealed class RouteActionBranchCoverageSourceGuardTests
{
    [Fact]
    public void BackwoodsPassThroughTriggerIsReadCoveredWithItsNativeSideEffectWindow()
    {
        var source = ShopAccessReadAdapterSources.All;

        Assert.Contains(
            "\"asdlfkjg\" => \"covered_for_read\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "from 19:20 through 20:19 in dry single-player after day 3",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "2.5 percent secret-mail and cosmetic-event branch",
            source,
            StringComparison.Ordinal);
    }
}
