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
