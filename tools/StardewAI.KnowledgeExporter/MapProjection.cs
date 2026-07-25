using xTile;
using xTile.ObjectModel;
using xTile.Tiles;

namespace StardewAI.KnowledgeExporter;

internal static class MapProjection
{
    public static RuntimeMapAssetProjection Build(string assetName, object value)
    {
        if (value is not Map map)
        {
            return new(
                assetName,
                value.GetType().FullName ?? value.GetType().Name,
                "non_map_asset",
                null);
        }

        return new(
            assetName,
            map.GetType().FullName ?? map.GetType().Name,
            "x_tile_map",
            ProjectMap(assetName, map));
    }

    private static RuntimeMapProjection ProjectMap(string assetName, Map map) => new(
        assetName,
        map.Id,
        map.Description,
        map.DisplayWidth,
        map.DisplayHeight,
        Properties(map.Properties),
        map.TileSheets.Select(sheet => new RuntimeMapTileSheet(
            sheet.Id,
            sheet.Description,
            sheet.ImageSource,
            sheet.SheetWidth,
            sheet.SheetHeight,
            sheet.TileWidth,
            sheet.TileHeight,
            sheet.MarginWidth,
            sheet.MarginHeight,
            sheet.SpacingWidth,
            sheet.SpacingHeight,
            Properties(sheet.Properties))).ToArray(),
        map.Layers.Select(layer =>
        {
            var tiles = new List<RuntimeMapTile>();
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    var tile = layer.Tiles[x, y];
                    if (tile is null)
                        continue;
                    tiles.Add(ProjectTile(x, y, tile));
                }
            }

            return new RuntimeMapLayer(
                layer.Id,
                layer.Description,
                layer.LayerWidth,
                layer.LayerHeight,
                layer.TileWidth,
                layer.TileHeight,
                layer.Visible,
                Properties(layer.Properties),
                tiles);
        }).ToArray());

    private static RuntimeMapTile ProjectTile(int x, int y, Tile tile)
    {
        if (tile is AnimatedTile animated)
        {
            var frames = animated.TileFrames.Select(frame => new RuntimeMapTileFrame(
                frame.TileSheet.Id,
                frame.TileIndex,
                frame.BlendMode.ToString(),
                Properties(frame.TileIndexProperties))).ToArray();
            return new(
                x,
                y,
                "animated",
                null,
                null,
                null,
                animated.FrameInterval,
                frames,
                Properties(tile.Properties),
                Array.Empty<RuntimeMapProperty>());
        }

        return new(
            x,
            y,
            "static",
            tile.TileSheet.Id,
            tile.TileIndex,
            tile.BlendMode.ToString(),
            null,
            Array.Empty<RuntimeMapTileFrame>(),
            Properties(tile.Properties),
            Properties(tile.TileIndexProperties));
    }

    private static IReadOnlyList<RuntimeMapProperty> Properties(IPropertyCollection properties) =>
        properties.OrderBy(row => row.Key, StringComparer.Ordinal)
            .Select(row => new RuntimeMapProperty(
                row.Key,
                row.Value.Type.FullName ?? row.Value.Type.Name,
                row.Value.ToString()))
            .ToArray();
}

internal sealed record RuntimeMapAssetProjection(
    string AssetName,
    string RuntimeType,
    string AssetKind,
    RuntimeMapProjection? Map);

internal sealed record RuntimeMapProjection(
    string AssetName,
    string Id,
    string Description,
    int DisplayWidth,
    int DisplayHeight,
    IReadOnlyList<RuntimeMapProperty> Properties,
    IReadOnlyList<RuntimeMapTileSheet> TileSheets,
    IReadOnlyList<RuntimeMapLayer> Layers);

internal sealed record RuntimeMapTileSheet(
    string Id,
    string Description,
    string ImageSource,
    int SheetWidth,
    int SheetHeight,
    int TileWidth,
    int TileHeight,
    int MarginWidth,
    int MarginHeight,
    int SpacingWidth,
    int SpacingHeight,
    IReadOnlyList<RuntimeMapProperty> Properties);

internal sealed record RuntimeMapLayer(
    string Id,
    string Description,
    int Width,
    int Height,
    int TileWidth,
    int TileHeight,
    bool Visible,
    IReadOnlyList<RuntimeMapProperty> Properties,
    IReadOnlyList<RuntimeMapTile> Tiles);

internal sealed record RuntimeMapTile(
    int X,
    int Y,
    string Kind,
    string? TileSheetId,
    int? TileIndex,
    string? BlendMode,
    long? FrameInterval,
    IReadOnlyList<RuntimeMapTileFrame> Frames,
    IReadOnlyList<RuntimeMapProperty> Properties,
    IReadOnlyList<RuntimeMapProperty> TileIndexProperties);

internal sealed record RuntimeMapTileFrame(
    string TileSheetId,
    int TileIndex,
    string BlendMode,
    IReadOnlyList<RuntimeMapProperty> TileIndexProperties);

internal sealed record RuntimeMapProperty(string Name, string Type, string Value);
