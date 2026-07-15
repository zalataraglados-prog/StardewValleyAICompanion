using StardewAI.TransparentBridge.Adapters;

namespace StardewAI.Core.Tests;

public sealed class FishingTileScannerTests
{
    [Fact]
    public void ScanEnumeratesEveryFishableTileInStableMapOrder()
    {
        var fishable = new HashSet<(int X, int Y)>
        {
            (2, 0),
            (0, 1),
            (2, 1)
        };

        var rows = FishingTileScanner.Scan(
            width: 3,
            height: 2,
            (x, y) => fishable.Contains((x, y)),
            (x, y) => x + y + 1,
            (x, y) => new FishingAreaRead(y == 0 ? "river" : "ocean", y == 0 ? "River" : "Ocean"));

        Assert.Equal(3, rows.Length);
        Assert.Collection(rows,
            row => AssertRow(row, 2, 0, 3, "river"),
            row => AssertRow(row, 0, 1, 2, "ocean"),
            row => AssertRow(row, 2, 1, 4, "ocean"));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    public void ScanReturnsNoRowsForInvalidMapDimensions(int width, int height)
    {
        var rows = FishingTileScanner.Scan(
            width,
            height,
            (_, _) => throw new InvalidOperationException("scanner should not probe an invalid map"),
            (_, _) => 0,
            (_, _) => new FishingAreaRead(null, null));

        Assert.Empty(rows);
    }

    private static void AssertRow(FishingTileReadRow row, int x, int y, int depth, string areaId)
    {
        Assert.Equal(x, row.TileX);
        Assert.Equal(y, row.TileY);
        Assert.Equal(depth, row.WaterDepth);
        Assert.Equal(areaId, row.FishAreaId);
    }
}
