namespace StardewAI.Backend.Tests;

public sealed class RuntimeMineRewardChestExecutorTests
{
    [Fact]
    public void RuntimeUsesOneNativeOpenAndObservedRewardDeltas()
    {
        var source = RuntimeHarnessSources.All;
        var executorSource = RuntimeHarnessSources.File("ModEntry.MineRewardChests.cs");

        Assert.Contains("StartMineRewardChest(pending);", source);
        Assert.Contains("active.Mine.checkAction(", executorSource);
        Assert.Contains("MineRewardPostconditionsMet", executorSource);
        Assert.Contains("request.ExpectedSkillExperienceDelta != 0", executorSource);
        Assert.DoesNotContain("Game1.player.gainExperience(Farmer.luckSkill", executorSource);
        Assert.DoesNotContain("Game1.player.maxStamina.Value +=", executorSource);
        Assert.DoesNotContain("Game1.player.addItem", executorSource);
    }
}
