using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class IslandGourmandSupplyScheduleProofTests
{
    [Fact]
    public void BindsNativeCropShopAndExistingActionChainWithoutClosingUnknownUnlockDays()
    {
        var proof = IslandGourmandSupplyScheduleProofBuilder.Build();

        Assert.Equal("island_gourmand_supply_schedule.v1", proof.SchemaVersion);
        Assert.Equal("IslandWest", proof.PlantingLocationId);
        Assert.True(proof.PlantingLocationIgnoresSeasons);
        Assert.Equal(224, proof.DeadlineTotalDayExclusive);
        Assert.Equal(140, proof.EarliestAllSeedStockTotalDay);
        Assert.Equal(12, proof.MaximumGrowthNights);
        Assert.Equal(211, proof.LatestWesternTurtleUnlockTotalDayInclusive);
        Assert.Equal(223, proof.LatestIslandHouseUnlockTotalDayInclusive);
        Assert.Equal(130, proof.StandardSeedShopTotalPrice);
        Assert.True(proof.FreshShopQuoteRequired);
        Assert.True(proof.SameDaySequentialDeliverySupported);
        Assert.True(proof.SupplyScheduleComplete);
        Assert.False(proof.DeadlineCompositionComplete);
        Assert.Null(proof.DeliveryTotalDay);
        Assert.Contains("economy.buy_supplies", proof.RequiredOptionIds);
        Assert.Contains("executor.till_soil", proof.RequiredOptionIds);
        Assert.Contains("executor.plant_seed", proof.RequiredOptionIds);
        Assert.Contains("executor.water_crop", proof.RequiredOptionIds);
        Assert.Contains("executor.sleep", proof.RequiredOptionIds);
        Assert.Contains("executor.traverse_connector", proof.RequiredOptionIds);
        Assert.Contains("executor.advance_gourmand_request", proof.RequiredOptionIds);

        Assert.Collection(
            proof.Crops,
            melon =>
            {
                Assert.Equal((0, "479", "254", 12),
                    (melon.RequestIndex, melon.SeedId, melon.HarvestItemId, melon.BaseGrowDays));
                Assert.Equal(new[] { 1, 2, 3, 3, 3 }, melon.DaysInPhase);
                Assert.Equal((140, 167, 80),
                    (melon.PurchaseWindowStartTotalDay, melon.PurchaseWindowEndTotalDayInclusive,
                        melon.StandardSeedShopUnitPrice));
            },
            wheat =>
            {
                Assert.Equal((1, "483", "262", 4),
                    (wheat.RequestIndex, wheat.SeedId, wheat.HarvestItemId, wheat.BaseGrowDays));
                Assert.Equal(new[] { "SEASON summer", "SEASON fall" }, wheat.SeedShopConditions);
                Assert.Equal((140, 195, 10),
                    (wheat.PurchaseWindowStartTotalDay, wheat.PurchaseWindowEndTotalDayInclusive,
                        wheat.StandardSeedShopUnitPrice));
            },
            garlic =>
            {
                Assert.Equal((2, "476", "248", 4),
                    (garlic.RequestIndex, garlic.SeedId, garlic.HarvestItemId, garlic.BaseGrowDays));
                Assert.Equal(new[] { "SEASON spring, YEAR 2" }, garlic.SeedShopConditions);
                Assert.Equal((112, 139, 40),
                    (garlic.PurchaseWindowStartTotalDay, garlic.PurchaseWindowEndTotalDayInclusive,
                        garlic.StandardSeedShopUnitPrice));
            });
        Assert.All(proof.Crops, crop => Assert.True(crop.NeedsWatering));
        Assert.All(proof.Crops, crop => Assert.True(crop.FreshShopQuoteRequired));
        Assert.All(proof.Crops, crop => Assert.True(crop.MustRemainStanding));
    }

    [Theory]
    [InlineData(211, 223, 223)]
    [InlineData(200, 210, 212)]
    [InlineData(100, 100, 152)]
    public void ComposesKnownUnlockDaysThroughConcurrentGrowth(
        int turtleUnlockDay,
        int houseUnlockDay,
        int expectedDeliveryDay)
    {
        var proof = IslandGourmandSupplyScheduleProofBuilder.Build(
            turtleUnlockDay,
            houseUnlockDay);

        Assert.Equal(expectedDeliveryDay, proof.DeliveryTotalDay);
        Assert.True(proof.DeadlineCompositionComplete);
        Assert.Equal("supply_and_deadline_composition_complete", proof.Status);
    }

    [Theory]
    [InlineData(212, 100)]
    [InlineData(100, 224)]
    public void RejectsLateUnlockComposition(int turtleUnlockDay, int houseUnlockDay)
    {
        var proof = IslandGourmandSupplyScheduleProofBuilder.Build(
            turtleUnlockDay,
            houseUnlockDay);

        Assert.False(proof.DeadlineCompositionComplete);
        Assert.True(proof.DeliveryTotalDay >= proof.DeadlineTotalDayExclusive);
    }

    [Fact]
    public void RejectsAuthorityDrift()
    {
        var canonical = IslandGourmandSupplyScheduleProofBuilder.Build();
        var tampered = canonical.Crops
            .Select(crop => crop.RequestIndex == 0
                ? crop with { BaseGrowDays = 11 }
                : crop)
            .ToArray();

        var error = Assert.Throws<InvalidOperationException>(() =>
            IslandGourmandSupplyScheduleProofBuilder.Build(tampered));

        Assert.Contains("authority drifted", error.Message, StringComparison.Ordinal);
    }
}
