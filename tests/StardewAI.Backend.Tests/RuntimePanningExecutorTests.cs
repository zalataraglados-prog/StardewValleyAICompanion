using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimePanningExecutorTests
{
    [Fact]
    public void PanningRequestCarriesDualExperienceAndStatProjection()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.pan_ore_spot", PanUpgradeLevel = 2, PanEnchantmentsJson = "[]",
            ExpectedTimesPannedBefore = 5, ExpectedTimesPannedAfter = 6,
            ExpectedMiningExperienceDelta = 12, ExpectedForagingExperienceDelta = 14,
            PostUseOrePanPointStatus = "runtime_rng_observed"
        };
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(12, result.ExpectedMiningExperienceDelta);
        Assert.Equal(14, result.ExpectedForagingExperienceDelta);
        Assert.Equal(6, result.ExpectedTimesPannedAfter);
    }

    [Fact]
    public void RuntimeUsesNativePanLifecycleAndExactMultisetVerification()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartPanOreSpot(pending);", source);
        Assert.Contains("Game1.player.BeginUsingTool();", source);
        Assert.Contains("pan.getPanItems(location, clone)", source);
        Assert.Contains("PanningOutputDeltaMatches", source);
        Assert.Contains("Game1.player.stats.Get(\"TimesPanned\")", source);
        Assert.Contains("receiptStatsMatch", source);
        Assert.DoesNotContain("Game1.player.experiencePoints[Farmer.miningSkill] =", source);
        Assert.DoesNotContain("active.Location.orePanPoint.Value = Point", source);
    }
}
