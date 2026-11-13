using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSiteApproachObservation
{
    private static CropSiteApproachTile[] ReadContext(SnapshotEnvelope accepted, SnapshotEnvelope observed,
        MoneyRevenueCropPlantingTile[] preparation)
    {
        try { return ReadContextCore(accepted, observed, preparation); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException)
        { throw new InvalidDataException("Site approach observation fields are missing or mistyped.", error); }
    }

    private static CropSiteApproachTile[] ReadContextCore(SnapshotEnvelope accepted, SnapshotEnvelope observed,
        MoneyRevenueCropPlantingTile[] preparation)
    {
        var field = observed.State["current_location"].GetProperty("site_preparation_context");
        var context = Value(observed, "current_location", "site_preparation_context");
        Require(field.TryGetProperty("read_at_tick", out var tick) && tick.ValueKind == JsonValueKind.Number &&
                tick.TryGetInt64(out var readTick) && readTick == observed.GameTick &&
                context.GetProperty("schema_version").GetString() == "farm_site_preparation_context.v2" &&
                context.GetProperty("projection_status").GetString() == "complete_bounded_current_farm_region" &&
                context.GetProperty("location_id").GetString() == "Farm" && context.GetProperty("is_farm").ValueKind == JsonValueKind.True,
            "Site approach lacks its original same-frame bounded Farm observation.");
        var map = Value(accepted, "current_location", "map");
        var observedMap = Value(observed, "current_location", "map");
        var width = Int(context.GetProperty("map_width")); var height = Int(context.GetProperty("map_height"));
        // The native map ID may be empty; player/context carry the logical location.
        Require(map.GetProperty("id").ValueKind == JsonValueKind.String && EqualJson(map.GetProperty("id"), observedMap.GetProperty("id")) &&
                Int(map.GetProperty("width")) == width && Int(map.GetProperty("height")) == height &&
                Int(observedMap.GetProperty("width")) == width && Int(observedMap.GetProperty("height")) == height,
            "Site approach map geometry differs from its accepted source.");
        var bounds = context.GetProperty("bounds");
        var minX = Int(bounds.GetProperty("min_x")); var maxX = Int(bounds.GetProperty("max_x"));
        var minY = Int(bounds.GetProperty("min_y")); var maxY = Int(bounds.GetProperty("max_y"));
        Require(minX >= 0 && minY >= 0 && minX <= maxX && minY <= maxY && maxX < width && maxY < height &&
                preparation.All(tile => tile.TileX >= minX && tile.TileX <= maxX && tile.TileY >= minY && tile.TileY <= maxY),
            "Site approach observation does not cover its original preparation layout.");
        var rows = context.GetProperty("tiles");
        Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == Int(context.GetProperty("tile_count")) &&
                rows.GetArrayLength() == ((long)maxX - minX + 1) * ((long)maxY - minY + 1),
            "Site approach bounded observation is incomplete.");
        bool Bool(JsonElement row, string name)
        {
            var value = row.GetProperty(name);
            Require(value.ValueKind is JsonValueKind.True or JsonValueKind.False, "Site approach standability is missing or mistyped.");
            return value.GetBoolean();
        }
        var tiles = rows.EnumerateArray().Select(row => new CropSiteApproachTile(Int(row.GetProperty("tile_x")),
            Int(row.GetProperty("tile_y")), Bool(row, "map_passable"), Bool(row, "native_blocked_without_characters"),
            Bool(row, "watering_refill_approach_standable"))).ToArray();
        Require(tiles.Select(tile => (tile.TileX, tile.TileY)).Distinct().Count() == tiles.Length &&
                tiles.All(tile => tile.TileX >= minX && tile.TileX <= maxX && tile.TileY >= minY && tile.TileY <= maxY),
            "Site approach observation repeats a tile or leaves its declared bounds.");
        return tiles;
    }
}
