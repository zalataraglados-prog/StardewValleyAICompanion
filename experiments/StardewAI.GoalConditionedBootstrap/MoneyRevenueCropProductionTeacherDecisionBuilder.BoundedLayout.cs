namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    private static MoneyRevenueCropPlantingTile[] SelectBoundedCompactTiles(
        LayoutTile[] source,
        int count,
        int anchorX,
        int anchorY,
        LayoutEvidence[] layouts,
        MoneyRevenueCropPlantingTile[] refillPreparationTiles)
    {
        var original = SelectCompactTiles(source, count, anchorX, anchorY);
        if (FitsFarmLayoutProjection(original.Concat(refillPreparationTiles)
                .ToArray()))
            return original;

        // Preserve every already-valid legacy choice. Only an oversized choice
        // falls back to one of the exact bounded regions already read.
        var fallback = layouts
            .Where(layout => refillPreparationTiles.All(tile =>
                Contains(layout.Binding, tile.TileX, tile.TileY)))
            .Select(layout => new
            {
                layout.Binding,
                Tiles = source.Where(tile => Contains(layout.Binding,
                    tile.TileX, tile.TileY)).ToArray()
            })
            .Where(region => region.Tiles.Length >= count)
            .Select(region => new
            {
                region.Binding,
                Tiles = SelectCompactTiles(region.Tiles, count, anchorX, anchorY),
                ByCoordinate = region.Tiles.ToDictionary(tile =>
                    (tile.TileX, tile.TileY))
            })
            .OrderBy(region => region.Tiles.Sum(tile => StatusRank(
                region.ByCoordinate[(tile.TileX, tile.TileY)].Status)))
            .ThenBy(region => region.Tiles.Sum(tile => (long)Manhattan(
                tile.TileX, tile.TileY, anchorX, anchorY)))
            .ThenBy(region => region.Binding.MinY)
            .ThenBy(region => region.Binding.MinX)
            .FirstOrDefault();
        Require(fallback is not null,
            "Money Revenue crop-production Teacher found no single bounded Farm region for the selected crop and refill preparation layout.");
        return fallback!.Tiles;
    }

    private static bool Contains(
        MoneyRevenueCropTeacherLayoutSnapshotBinding bounds, int x, int y) =>
        x >= bounds.MinX && x <= bounds.MaxX &&
        y >= bounds.MinY && y <= bounds.MaxY;
}
