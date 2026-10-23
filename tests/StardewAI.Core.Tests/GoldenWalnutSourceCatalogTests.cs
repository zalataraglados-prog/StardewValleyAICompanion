using StardewAI.Contracts.State;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutSourceCatalogTests
{
    private static readonly string[] NativeTrackerAndLimitedKeys =
    {
        "Bush_IslandNorth_13_33", "Bush_IslandNorth_5_30", "Buried_IslandNorth_19_39",
        "Bush_IslandNorth_4_42", "Bush_IslandNorth_45_38", "Bush_IslandNorth_47_40",
        "IslandLeftPlantRestored", "IslandRightPlantRestored", "IslandBatRestored", "IslandFrogRestored",
        "IslandCenterSkeletonRestored", "IslandSnakeRestored", "Buried_IslandNorth_19_13",
        "Buried_IslandNorth_57_79", "Buried_IslandNorth_54_21", "Buried_IslandNorth_42_77",
        "Buried_IslandNorth_62_54", "Buried_IslandNorth_26_81", "Bush_IslandNorth_20_26",
        "Bush_IslandNorth_9_84", "Bush_IslandNorth_56_27", "Bush_IslandSouth_31_5", "TreeNut",
        "IslandWestCavePuzzle", "SandDuggy", "TigerSlimeNut", "Buried_IslandWest_21_81",
        "Buried_IslandWest_62_76", "Buried_IslandWest_39_24", "Buried_IslandWest_88_14",
        "Buried_IslandWest_43_74", "Buried_IslandWest_30_75", "MusselStone", "IslandFarming",
        "Bush_IslandWest_104_3", "Bush_IslandWest_31_24", "Bush_IslandWest_38_56",
        "Bush_IslandWest_75_29", "Bush_IslandWest_64_30", "Bush_IslandWest_54_18",
        "Bush_IslandWest_25_30", "Bush_IslandWest_15_3", "IslandFishing", "VolcanoNormalChest",
        "VolcanoRareChest", "VolcanoBarrel", "VolcanoMining", "VolcanoMonsterDrop",
        "Island_N_BuriedTreasureNut", "Island_W_BuriedTreasureNut", "Island_W_BuriedTreasureNut2",
        "Mermaid", "TreeNutShot", "Buried_IslandSouthEastCave_36_26",
        "Buried_IslandSouthEast_25_17", "StardropPool", "Bush_Caldera_28_36",
        "Bush_Caldera_9_34", "Bush_CaptainRoom_2_4", "BananaShrine", "Bush_IslandEast_17_37",
        "Darts", "IslandGourmand1", "IslandGourmand2", "IslandGourmand3",
        "IslandShrinePuzzle", "Bush_IslandShrine_23_34"
    };

    [Fact]
    public void CatalogMatchesLockedVanillaDenominator()
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources;

        Assert.Equal(69, sources.Count);
        Assert.Equal(130, sources.Sum(source => source.NativeQuantity));
        Assert.Equal(69, sources.Select(source => source.SourceKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            NativeTrackerAndLimitedKeys.OrderBy(key => key, StringComparer.Ordinal),
            sources
                .Where(source => source.TrackingKind is "tracker" or "limited_drop")
                .Select(source => source.SourceKey)
                .OrderBy(key => key, StringComparer.Ordinal));
        Assert.Contains(sources, source => source.SourceKey == "GoldenCoconutCracked" && source.NativeQuantity == 1);
        Assert.Contains(sources, source => source.SourceKey == "gotBirdieReward" && source.NativeQuantity == 5);
    }

    [Fact]
    public void ResortOnlyAndOptionalBridgeSourcesCannotPolluteFiftyOneWalnutProof()
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources;

        Assert.Equal(11, sources.Where(source => source.ResortOnly).Sum(source => source.NativeQuantity));
        Assert.All(sources.Where(source => source.ResortOnly), source =>
            Assert.Equal("Island_Resort", source.RequiredMailKey));
        Assert.Equal(15, sources
            .Where(source => source.OptionalUnlockWalnutCost == 10)
            .Sum(source => source.NativeQuantity));
        Assert.All(sources.Where(source => source.OptionalUnlockWalnutCost > 0), source =>
            Assert.Equal("Island_UpgradeBridge", source.RequiredMailKey));
        Assert.Contains(sources, source =>
            source.SourceKey == "Bush_IslandSouth_31_5" &&
            source.LocationId == "IslandSouth" &&
            source.TileX == 31 &&
            source.TileY == 5 &&
            !source.ResortOnly &&
            source.OptionalUnlockWalnutCost == 0);
    }

    [Fact]
    public void FixedBuriedNutsReuseTheSingleNativeTillExecutor()
    {
        var buried = GoldenWalnutNativeSourceCatalog.Sources
            .Where(source => source.SourceKey.StartsWith("Buried_", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(15, buried.Length);
        Assert.All(buried, source => Assert.Equal("executor.till_soil", source.AcquisitionOptionId));
        Assert.Equal(12, buried.Count(source =>
            !source.ResortOnly && source.OptionalUnlockWalnutCost == 0));
    }

    [Fact]
    public void BoundPreResortSourcesProvideFiftyOneWalnutsWithoutOptionalBridge()
    {
        var boundActions = new HashSet<string>(StringComparer.Ordinal)
        {
            "foraging.harvest_bushes",
            "fishing.catch_fish",
            "executor.clear_obstacle",
            "farm.maintain_crops",
            "executor.till_soil"
        };
        var sources = GoldenWalnutNativeSourceCatalog.Sources
            .Where(source =>
                !source.ResortOnly &&
                source.OptionalUnlockWalnutCost == 0 &&
                boundActions.Contains(source.AcquisitionOptionId))
            .ToArray();

        Assert.Equal(51, sources.Sum(source => source.NativeQuantity));
        Assert.Contains(sources, source =>
            source.SourceKey == "Island_W_BuriedTreasureNut" &&
            source.TileX == 18 &&
            source.TileY == 42 &&
            source.RequiredSecretNoteId == 1004);
        Assert.Contains(sources, source =>
            source.SourceKey == "Island_W_BuriedTreasureNut2" &&
            source.TileX == 104 &&
            source.TileY == 74 &&
            source.RequiredSecretNoteId == 1006);
    }
}
