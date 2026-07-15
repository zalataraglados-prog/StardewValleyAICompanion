using StardewAI.TransparentBridge.Adapters;

namespace StardewAI.Core.Tests;

public sealed class FishingSpawnRuleEvaluatorTests
{
    [Fact]
    public void EvaluateFiltersAreaRectangleAndWaterDepthWithoutRandomRolls()
    {
        var result = FishingSpawnRuleEvaluator.Evaluate(
            new FishingRuleEligibilitySpec
            {
                Season = "Summer",
                FishAreaId = "river",
                BobberPosition = new FishingRectangleRead { X = 1, Y = 1, Width = 3, Height = 2 },
                PlayerPosition = new FishingRectangleRead { X = 4, Y = 4, Width = 2, Height = 2 },
                MinFishingLevel = 3,
                MinDistanceFromShore = 2,
                MaxDistanceFromShore = 4,
                ConditionMet = true
            },
            new FishingRuleEligibilityContext
            {
                Season = "Summer",
                PlayerTileX = 5,
                PlayerTileY = 4,
                FishingLevel = 4
            },
            new[]
            {
                Tile(1, 1, 1, "river"),
                Tile(2, 1, 2, "river"),
                Tile(3, 2, 4, "river"),
                Tile(4, 2, 3, "river"),
                Tile(2, 2, 3, "ocean")
            });

        Assert.True(result.EligibleBeforeRandomRolls);
        Assert.Empty(result.BlockingReasons);
        Assert.Collection(
            result.EligibleTiles,
            tile => Assert.Equal((2, 1), (tile.TileX, tile.TileY)),
            tile => Assert.Equal((3, 2), (tile.TileX, tile.TileY)));
    }

    [Fact]
    public void EvaluateReportsEveryCurrentContextBlocker()
    {
        var result = FishingSpawnRuleEvaluator.Evaluate(
            new FishingRuleEligibilitySpec
            {
                Season = "Winter",
                PlayerPosition = new FishingRectangleRead { X = 10, Y = 10, Width = 1, Height = 1 },
                MinFishingLevel = 8,
                RequireMagicBait = true,
                ConditionMet = false,
                MinDistanceFromShore = 5
            },
            new FishingRuleEligibilityContext
            {
                Season = "Spring",
                PlayerTileX = 2,
                PlayerTileY = 2,
                FishingLevel = 1,
                HasMagicBait = false
            },
            new[] { Tile(1, 1, 2, null) });

        Assert.False(result.EligibleBeforeRandomRolls);
        Assert.Equal(
            new[]
            {
                "season_mismatch",
                "player_position_mismatch",
                "fishing_level_too_low",
                "magic_bait_required",
                "game_state_query_false",
                "no_matching_fishable_tile"
            },
            result.BlockingReasons);
    }

    [Fact]
    public void MagicBaitBypassesSeasonButNotExplicitMagicRequirementOrGeometry()
    {
        var result = FishingSpawnRuleEvaluator.Evaluate(
            new FishingRuleEligibilitySpec
            {
                Season = "Winter",
                RequireMagicBait = true,
                FishAreaId = "ocean",
                ConditionMet = true
            },
            new FishingRuleEligibilityContext
            {
                Season = "Summer",
                FishingLevel = 0,
                HasMagicBait = true
            },
            new[] { Tile(0, 0, 0, "ocean") });

        Assert.True(result.EligibleBeforeRandomRolls);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void DataFishParserAndEvaluatorApplyTimeWeatherLevelAndTrainingRodRules()
    {
        var requirements = FishingDataFishRuleParser.Parse(DataFishRow(
            difficulty: "55",
            timeWindows: "600 1200 1800 2600",
            weather: "rainy",
            minLevel: "4",
            tutorial: "true"));

        var result = FishingDataFishRuleParser.Evaluate(
            requirements,
            new FishingDataFishEligibilityContext
            {
                TimeOfDay = 1300,
                IsRaining = false,
                FishingLevel = 2,
                UsesTrainingRod = true,
                IsTutorialCatch = true
            },
            canUseTrainingRod: null,
            ignoreFishDataRequirements: false);

        Assert.Equal("parsed", requirements.ParseStatus);
        Assert.Equal(2, requirements.TimeWindows.Length);
        Assert.False(result.EligibleBeforeRandomRoll);
        Assert.Equal(
            new[]
            {
                "training_rod_difficulty_limit",
                "outside_data_fish_time_window",
                "rain_required",
                "data_fish_level_too_low"
            },
            result.BlockingReasons);
    }

    [Fact]
    public void MagicBaitBypassesDataFishTimeAndWeatherButNotLevel()
    {
        var requirements = FishingDataFishRuleParser.Parse(DataFishRow(
            difficulty: "20",
            timeWindows: "600 700",
            weather: "rainy",
            minLevel: "5",
            tutorial: "false"));

        var result = FishingDataFishRuleParser.Evaluate(
            requirements,
            new FishingDataFishEligibilityContext
            {
                TimeOfDay = 1200,
                IsRaining = false,
                FishingLevel = 3,
                HasMagicBait = true
            },
            canUseTrainingRod: null,
            ignoreFishDataRequirements: false);

        Assert.Equal(new[] { "data_fish_level_too_low" }, result.BlockingReasons);
    }

    [Fact]
    public void EmptyDataFishTimeWindowRejectsWithoutMagicBait()
    {
        var requirements = FishingDataFishRuleParser.Parse(DataFishRow(
            difficulty: "20",
            timeWindows: "",
            weather: "both",
            minLevel: "0",
            tutorial: "false"));

        var result = FishingDataFishRuleParser.Evaluate(
            requirements,
            new FishingDataFishEligibilityContext
            {
                TimeOfDay = 900,
                FishingLevel = 0
            },
            canUseTrainingRod: null,
            ignoreFishDataRequirements: false);

        Assert.Equal(new[] { "outside_data_fish_time_window" }, result.BlockingReasons);
    }

    [Fact]
    public void DataFishParserMarksMalformedEligibilityFieldsAsError()
    {
        var requirements = FishingDataFishRuleParser.Parse(DataFishRow(
            difficulty: "hard",
            timeWindows: "600",
            weather: "sunny",
            minLevel: "none",
            tutorial: "sometimes"));

        Assert.Equal("error", requirements.ParseStatus);
        Assert.Contains("difficulty_invalid", requirements.ParseErrors);
        Assert.Contains("time_windows_unpaired", requirements.ParseErrors);
        Assert.Contains("min_fishing_level_invalid", requirements.ParseErrors);
        Assert.Contains("tutorial_fish_invalid", requirements.ParseErrors);
    }

    private static FishingTileReadRow Tile(int x, int y, int depth, string? area)
    {
        return new FishingTileReadRow
        {
            TileX = x,
            TileY = y,
            WaterDepth = depth,
            FishAreaId = area
        };
    }

    private static string DataFishRow(
        string difficulty,
        string timeWindows,
        string weather,
        string minLevel,
        string tutorial)
    {
        return string.Join("/", new[]
        {
            "Fish",
            difficulty,
            "mixed",
            "10",
            "20",
            timeWindows,
            "spring",
            weather,
            "0",
            "5",
            "0.35",
            "0.1",
            minLevel,
            tutorial
        });
    }
}
