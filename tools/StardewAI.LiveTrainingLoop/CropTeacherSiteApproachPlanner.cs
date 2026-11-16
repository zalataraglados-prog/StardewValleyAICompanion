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
        var result = StardewAI.Core.Training.CropSiteApproachSelection.Select(playerTileX, playerTileY,
            preparationTiles, contextTiles.Select(tile => new StardewAI.Core.Training.CropSiteApproachTile(
                tile.TileX, tile.TileY, tile.MapPassable, tile.NativeBlockedWithoutCharacters, tile.NativeApproachStandable)).ToArray());
        return result is null ? null : contextTiles.First(tile => tile.TileX == result.TileX && tile.TileY == result.TileY &&
            tile.MapPassable && !tile.NativeBlockedWithoutCharacters && tile.NativeApproachStandable);
    }
}
