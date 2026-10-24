using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeStarterGiftExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeChestActionAndVerifiesInventoryQuestAndRemoval()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.StarterGift.cs");

        Assert.Contains("StartStarterGiftClaim(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("StarterGiftContentsMatch", source);
        Assert.Contains("StarterGiftOutputDeltaMatches", source);
        Assert.Contains("Game1.player.hasQuest", source);
        Assert.Contains("StarterGiftReceiptMatches", source);
        Assert.DoesNotContain("dumpContents(", source);
        Assert.DoesNotContain("location.objects.Remove", source);
        Assert.DoesNotContain("Game1.player.addItem", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("rewards.claim_starter_gift"));
    }
}
