namespace StardewAI.Backend.Tests;

public sealed class RuntimeMineRewardChestExecutorTests
{
    [Fact]
    public void RuntimeUsesOneNativeOpenAndObservedRewardDeltas()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartMineRewardChest(pending);", source);
        Assert.Contains("active.Mine.checkAction(", source);
        Assert.Contains("MineRewardPostconditionsMet", source);
        Assert.Contains("request.ExpectedSkillExperienceDelta != 0", source);
        Assert.DoesNotContain("Game1.player.gainExperience(Farmer.luckSkill", source);
        Assert.DoesNotContain("Game1.player.maxStamina.Value +=", source);
        Assert.DoesNotContain("Game1.player.addItem", source);
    }
}
