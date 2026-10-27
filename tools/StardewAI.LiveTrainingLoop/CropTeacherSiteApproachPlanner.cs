namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSiteApproachTile(
    int TileX,
    int TileY,
    bool MapPassable,
    bool NativeBlockedWithoutCharacters,
    bool NativeApproachStandable);

public static class CropTeacherSiteApproachPlanner
{
    public static CropTeacherSiteApproachTile? SelectApproach(
        int playerTileX,
        int playerTileY,
        IReadOnlyCollection<(int X, int Y)> preparationTiles,
        IReadOnlyCollection<CropTeacherSiteApproachTile> contextTiles)
    {
        ArgumentNullException.ThrowIfNull(preparationTiles);
        ArgumentNullException.ThrowIfNull(contextTiles);
        if (preparationTiles.Count == 0)
        {
            throw new ArgumentException(
                "At least one preparation tile is required.",
                nameof(preparationTiles));
        }

        var preparation = preparationTiles.ToHashSet();
        var standable = contextTiles
            .Where(tile =>
                tile.MapPassable &&
                !tile.NativeBlockedWithoutCharacters &&
                tile.NativeApproachStandable &&
                preparation.Any(target =>
                    Manhattan(tile.TileX, tile.TileY, target.X, target.Y) == 1))
            .DistinctBy(tile => (tile.TileX, tile.TileY))
            .ToArray();
        if (standable.Any(tile =>
                tile.TileX == playerTileX &&
                tile.TileY == playerTileY))
        {
            return null;
        }

        var preferred = standable
            .Where(tile => !preparation.Contains((tile.TileX, tile.TileY)))
            .ToArray();
        return (preferred.Length > 0 ? preferred : standable)
            .OrderBy(tile => Manhattan(
                playerTileX,
                playerTileY,
                tile.TileX,
                tile.TileY))
            .ThenBy(tile => tile.TileY)
            .ThenBy(tile => tile.TileX)
            .FirstOrDefault();
    }

    private static int Manhattan(int ax, int ay, int bx, int by) =>
        Math.Abs(ax - bx) + Math.Abs(ay - by);
}
