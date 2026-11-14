using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherSiteApproachPlannerTests
{
    [Fact]
    public void SelectsNearestSafeNonPreparationApproachFromFarAway()
    {
        var result = CropTeacherSiteApproachPlanner.SelectApproach(
            40,
            64,
            new[] { (72, 25) },
            new[]
            {
                Tile(72, 25),
                Tile(71, 25),
                Tile(72, 26),
                Tile(73, 25)
            });

        Assert.NotNull(result);
        Assert.Equal((71, 25), (result!.TileX, result.TileY));
    }

    [Fact]
    public void DoesNotRepositionWhenPlayerAlreadyOccupiesAnySafeApproach()
    {
        var result = CropTeacherSiteApproachPlanner.SelectApproach(
            71,
            25,
            new[] { (72, 25) },
            new[]
            {
                Tile(71, 25),
                Tile(72, 26)
            });

        Assert.Null(result);
    }

    [Fact]
    public void FallsBackToPreparationTileWhenItIsTheOnlySafeApproach()
    {
        var result = CropTeacherSiteApproachPlanner.SelectApproach(
            40,
            64,
            new[] { (72, 25), (72, 24) },
            new[]
            {
                Tile(72, 25),
                Tile(71, 25, blocked: true)
            });

        Assert.NotNull(result);
        Assert.Equal((72, 25), (result!.TileX, result.TileY));
    }

    [Fact]
    public void ReturnsNoApproachWhenEveryAdjacentTileIsUnsafe()
    {
        var result = CropTeacherSiteApproachPlanner.SelectApproach(
            40,
            64,
            new[] { (72, 25) },
            new[]
            {
                Tile(71, 25, blocked: true),
                Tile(72, 26, passable: false),
                Tile(73, 25, standable: false)
            });

        Assert.Null(result);
    }

    [Fact]
    public void PreservesOriginalYThenXTieBreak()
    {
        var result = CropTeacherSiteApproachPlanner.SelectApproach(2, 2, new[] { (0, 0), (1, 0) },
            new[] { Tile(1, 1), Tile(2, 0), Tile(0, 1) });
        Assert.Equal((2, 0), (result!.TileX, result.TileY));
    }

    [Fact]
    public void PrefersOutsidePreparationEvenWhenAnInsideStandIsNearer()
    {
        var result = CropTeacherSiteApproachPlanner.SelectApproach(1, 3, new[] { (1, 1), (1, 2) },
            new[] { Tile(1, 2), Tile(2, 1) });
        Assert.Equal((2, 1), (result!.TileX, result.TileY));
    }

    [Fact]
    public void PreservesFirstValidOriginalTileIdentityForDuplicateCoordinates()
    {
        var original = Tile(1, 0);
        var result = CropTeacherSiteApproachPlanner.SelectApproach(8, 8, new[] { (0, 0) },
            new[] { Tile(1, 0, blocked: true), original, Tile(1, 0) });
        Assert.Same(original, result);
    }

    private static CropTeacherSiteApproachTile Tile(
        int x,
        int y,
        bool passable = true,
        bool blocked = false,
        bool standable = true) => new(
            x,
            y,
            passable,
            blocked,
            standable);
}
