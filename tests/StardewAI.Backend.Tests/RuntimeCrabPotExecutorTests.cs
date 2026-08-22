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

    [Fact]
    public void CrabPotBaitRequestCarriesOwnerAndExactUnitStateFields()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.load_crab_pot_bait",
            InventorySlotIndex = 2,
            ExpectedStackBefore = 2,
            QualifiedItemId = "(O)SpecificBait",
            ExpectedContainerBaitQualifiedItemId = "(O)SpecificBait",
            ExpectedContainerBaitUnitStateSha256 = new string('a', 64),
            ExpectedContainerOwnerPlayerIdBefore = 1234,
            ExpectedContainerOwnerPlayerIdAfter = 1234,
            BaitRuntimeType = "StardewValley.Object",
            BaitQuality = 0
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("executor.load_crab_pot_bait", roundTrip.OptionId);
        Assert.Equal("(O)SpecificBait", roundTrip.ExpectedContainerBaitQualifiedItemId);
        Assert.Equal(new string('a', 64), roundTrip.ExpectedContainerBaitUnitStateSha256);
        Assert.Equal(1234, roundTrip.ExpectedContainerOwnerPlayerIdBefore);
        Assert.Equal(1234, roundTrip.ExpectedContainerOwnerPlayerIdAfter);
        Assert.Equal("StardewValley.Object", roundTrip.BaitRuntimeType);
        Assert.Equal(0, roundTrip.BaitQuality);
    }

    [Fact]
    public void CrabPotBaitRuntimeUsesNativeCheckActionWithoutDirectProductionMutation()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("ExecuteLoadCrabPotBait", source);
        Assert.Contains("pot.performObjectDropInAction(bait, probe: true, Game1.player)", source);
        Assert.Contains("handled = location.checkAction(", source);
        Assert.Contains("native_reduceActiveItemByOne_consumed_exactly_one", source);
        Assert.DoesNotContain("pot.bait.Value = bait", source, StringComparison.Ordinal);
    }
}
