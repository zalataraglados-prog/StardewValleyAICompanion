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

    [Fact]
    public void RuntimeTransportAndWhitelistPreserveStrictRewardFacts()
    {
        var contract = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.Contracts", "Training", "TrainingExecutionContracts.cs");
        var transport = RuntimeHarnessSources.RepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs");
        var whitelist = RuntimeHarnessSources.File(
            "ModEntry.Shipping.Utilities.cs");
        var executor = RuntimeHarnessSources.File(
            "ModEntry.MineRewardChests.cs");

        Assert.Contains("[JsonPropertyName(\"reward_branch\")]", contract);
        Assert.Contains("[JsonPropertyName(\"native_gain_experience_call_amount\")]", contract);
        Assert.Contains("executionRequest.RewardBranch = rewardBranch;", transport);
        Assert.Contains(
            "executionRequest.NativeGainExperienceCallAmount = nativeGainExperienceCallAmount;",
            transport);
        Assert.Contains(
            "request.OptionId != \"executor.claim_mine_reward_chest\"",
            whitelist);
        Assert.Contains(
            "request.NativeGainExperienceCallAmount != 25 + mine.mineLevel",
            executor);
        Assert.Contains(
            "string.Equals(request.RewardBranch, rewardBranch",
            executor);
    }

    [Fact]
    public void InventoryReceiptProjectionPreservesToolTickerAndSmokeCoversNativeBranches()
    {
        var bridgeProjection = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters",
            "CurrentLocationReadAdapter.ObjectClearance.cs");
        var bridgeReward = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters",
            "MiningReadAdapter.RewardChests.cs");
        var runtimeProjection = RuntimeHarnessSources.File(
            "ModEntry.MovementSleep.ObstacleClearance.cs");
        var smoke = RuntimeHarnessSources.RepositoryFile(
            "scripts", "Invoke-RuntimeMineRewardChestSmoke.ps1");

        Assert.Contains(
            "unitTool.swingTicker = sourceTool.swingTicker;",
            bridgeProjection);
        Assert.Contains(
            "ClearanceOutputItemProjection.FromInventoryReceipt(item)",
            bridgeReward);
        Assert.Contains(
            "unitTool.swingTicker = sourceTool.swingTicker;",
            runtimeProjection);
        Assert.Contains(
            "ClearanceOutputItemKey.FromInventoryReceipt(item)",
            RuntimeHarnessSources.File("ModEntry.MineRewardChests.cs"));
        Assert.Contains("ordinary_floor_20", smoke);
        Assert.Contains("ordinary_floor_100_stardrop", smoke);
        Assert.Contains("skull_cavern_forced_multi", smoke);
        Assert.Contains(
            "STARDEWAI_RESET_MINE_REWARD_CHEST_FIXTURE",
            smoke);
        Assert.Contains(
            "STARDEWAI_SKIP_SKULL_CAVERN_SHAFT_FIXTURE",
            smoke);
        Assert.Contains("Write-MineRewardSnapshotEvidence", smoke);
        var fixture = RuntimeHarnessSources.File(
            "ModEntry.Mining.Fixtures.cs");
        Assert.Contains(
            "Game1.player.mailReceived.Remove(\"CF_Mines\")",
            fixture);
        Assert.Contains(
            "Game1.player.maxStamina.Value - 34",
            fixture);
    }
}
