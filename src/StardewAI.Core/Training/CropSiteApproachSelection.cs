using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Training;

public sealed record CropSiteApproachTile(int TileX, int TileY, bool MapPassable,
    bool NativeBlockedWithoutCharacters, bool NativeApproachStandable);

public static class CropSiteApproachSelection
{
    public static CropSiteApproachTile? Select(int playerTileX, int playerTileY,
        IReadOnlyCollection<(int X, int Y)> preparationTiles, IReadOnlyCollection<CropSiteApproachTile> contextTiles)
    {
        if (preparationTiles is null) throw new ArgumentNullException(nameof(preparationTiles));
        if (contextTiles is null) throw new ArgumentNullException(nameof(contextTiles));
        if (preparationTiles.Count == 0)
            throw new ArgumentException("At least one preparation tile is required.", nameof(preparationTiles));
        var preparation = preparationTiles.ToHashSet();
        var standable = contextTiles.Where(tile => tile.MapPassable && !tile.NativeBlockedWithoutCharacters &&
                tile.NativeApproachStandable && preparation.Any(target => Manhattan(tile.TileX, tile.TileY, target.X, target.Y) == 1))
            .GroupBy(tile => (tile.TileX, tile.TileY)).Select(group => group.First()).ToArray();
        if (standable.Any(tile => tile.TileX == playerTileX && tile.TileY == playerTileY)) return null;
        var preferred = standable.Where(tile => !preparation.Contains((tile.TileX, tile.TileY))).ToArray();
        return (preferred.Length > 0 ? preferred : standable)
            .OrderBy(tile => Manhattan(playerTileX, playerTileY, tile.TileX, tile.TileY))
            .ThenBy(tile => tile.TileY).ThenBy(tile => tile.TileX).FirstOrDefault();
    }

    private static int Manhattan(int ax, int ay, int bx, int by) => Math.Abs(ax - bx) + Math.Abs(ay - by);
}
