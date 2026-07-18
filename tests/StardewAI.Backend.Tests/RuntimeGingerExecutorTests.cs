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
        Assert.Contains("debrisAfter + inventoryAfter == tool.BeforeGingerDebrisCount + tool.BeforeGingerInventoryCount + 1", source);
        Assert.Contains("foragingAfter == tool.BeforeForagingExperience + 7", source);
        Assert.DoesNotContain("dirt.crop = null", source);
        Assert.DoesNotContain("experiencePoints[Farmer.foragingSkill] += 7", source);
    }
}
