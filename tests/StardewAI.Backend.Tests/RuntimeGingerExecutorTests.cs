namespace StardewAI.Backend.Tests;

public sealed class RuntimeGingerExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeHoeLifecycleAndExactGingerFeedback()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartHarvestGinger(pending);", source);
        Assert.Contains("ActiveNativeTool.Ginger", source);
        Assert.Contains("Game1.player.BeginUsingTool();", source);
        Assert.Contains("Game1.player.EndUsingTool();", source);
        Assert.Contains("GingerNativePostconditionsMet(tool)", source);
        Assert.Contains("TickGingerOutputCollection(tool);", source);
        Assert.Contains("DebrisChunkTile(candidate)", source);
        Assert.Contains("inventoryAfter == tool.BeforeGingerInventoryCount + 1", source);
        Assert.Contains("native_ginger_output_naturally_collected", source);
        Assert.Contains("foragingAfter == tool.BeforeForagingExperience + 7", source);
        Assert.DoesNotContain("dirt.crop = null", source);
        Assert.DoesNotContain("experiencePoints[Farmer.foragingSkill] += 7", source);
    }
}
