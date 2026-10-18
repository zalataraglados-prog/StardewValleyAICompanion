using System;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public sealed record ShippingBinRouteProjection(
    int TileX,
    int TileY,
    int Width,
    int Height,
    int StandTileX,
    int StandTileY);

public static class ShippingBinRouteProjector
{
    public static ShippingBinRouteProjection? SelectFirstCompleted(
        SnapshotEnvelope snapshot)
    {
        var bins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
        if (!bins.HasValue || bins.Value.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var bin in bins.Value.EnumerateArray())
        {
            if (ReadInt(bin, "days_of_construction_left") > 0)
                continue;
            var standX = NullableReadInt(bin, "interaction_stand_tile_x");
            var standY = NullableReadInt(bin, "interaction_stand_tile_y");
            if (!standX.HasValue || !standY.HasValue)
                continue;
            var width = Math.Max(1, ReadInt(bin, "tile_width"));
            var height = Math.Max(1, ReadInt(bin, "tile_height"));
            return new ShippingBinRouteProjection(
                ReadInt(bin, "tile_x"),
                ReadInt(bin, "tile_y"),
                width,
                height,
                standX.Value,
                standY.Value);
        }
        return null;
    }
}
