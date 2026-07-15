using System.Text.Json.Serialization;

namespace StardewAI.TransparentBridge.Adapters;

public sealed record FishingAreaRead(string? Id, string? DisplayName);

public sealed record FishingTileReadRow
{
    [JsonPropertyName("tile_x")]
    public int TileX { get; init; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; init; }

    [JsonPropertyName("water_depth")]
    public int WaterDepth { get; init; }

    [JsonPropertyName("fish_area_id")]
    public string? FishAreaId { get; init; }

    [JsonPropertyName("fish_area_display_name")]
    public string? FishAreaDisplayName { get; init; }
}

public static class FishingTileScanner
{
    public static FishingTileReadRow[] Scan(
        int width,
        int height,
        Func<int, int, bool> isFishable,
        Func<int, int, int> waterDepth,
        Func<int, int, FishingAreaRead> fishArea)
    {
        if (width <= 0 || height <= 0)
        {
            return Array.Empty<FishingTileReadRow>();
        }

        var rows = new List<FishingTileReadRow>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!isFishable(x, y))
                {
                    continue;
                }

                var area = fishArea(x, y);
                rows.Add(new FishingTileReadRow
                {
                    TileX = x,
                    TileY = y,
                    WaterDepth = waterDepth(x, y),
                    FishAreaId = area.Id,
                    FishAreaDisplayName = area.DisplayName
                });
            }
        }

        return rows.ToArray();
    }
}
