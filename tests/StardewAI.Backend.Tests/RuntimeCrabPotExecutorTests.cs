using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeCrabPotExecutorTests
{
    [Fact]
    public void CrabPotRequestCarriesExactProjectionFields()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.collect_crab_pot",
            ExpectedOutputItemsJson = "[{\"RuntimeType\":\"StardewValley.Object\",\"QualifiedItemId\":\"(O)372\",\"Quality\":0,\"UnitStateSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Quantity\":2}]",
            ExpectedSkillId = "fishing",
            ExpectedSkillExperienceDelta = 5,
            ExpectedContainerBaitQualifiedItemId = "(O)685",
            ExpectedFishCollectionEligible = 1,
            ExpectedFishCaughtCountBefore = 2,
            ExpectedFishCaughtCountAfter = 4,
            ExpectedFishCaughtMaxSizeBefore = 9,
            ExpectedCatchSizeMin = 1,
            ExpectedCatchSizeMax = 10,
            CatchSizeProjectionStatus = "runtime_rng_observed"
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("executor.collect_crab_pot", roundTrip.OptionId);
        Assert.Contains("UnitStateSha256", roundTrip.ExpectedOutputItemsJson);
        Assert.Equal("fishing", roundTrip.ExpectedSkillId);
        Assert.Equal(5, roundTrip.ExpectedSkillExperienceDelta);
        Assert.Equal(4, roundTrip.ExpectedFishCaughtCountAfter);
        Assert.Equal("runtime_rng_observed", roundTrip.CatchSizeProjectionStatus);
    }

    [Fact]
    public void CrabPotRuntimeUsesNativeActionAndVerifiesAllSideEffects()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartCrabPotCollect(pending);", source);
        Assert.Contains("typeof(CrabPot)", source);
        Assert.Contains("TryParseClearanceOutputItems(request.ExpectedOutputItemsJson", source);
        Assert.Contains("ClearanceOutputItemKey.From(inventoryOutput)", source);
        Assert.Contains("Utility.CreateDaySaveRandom", source);
        Assert.Contains("Game1.player.stats.Get(\"Book_Crabbing\")", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("CrabPotCaughtFishPatch", source);
        Assert.Contains("fishingExperienceAfter - active.FishingExperienceBefore", source);
        Assert.Contains("active.Pot.bait.Value is null", source);
        Assert.Contains("active.Pot.tileIndexToShow == 710", source);
        Assert.DoesNotContain("active.Pot.heldObject.Value = null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("active.Pot.bait.Value = null", source, StringComparison.Ordinal);
    }
}
