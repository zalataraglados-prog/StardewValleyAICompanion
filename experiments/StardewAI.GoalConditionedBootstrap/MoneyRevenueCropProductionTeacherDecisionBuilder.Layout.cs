using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    private static LayoutEvidence[] ReadLayoutSnapshots(
        IEnumerable<string> paths,
        SnapshotEnvelope source)
    {
        var result = new List<LayoutEvidence>();
        foreach (var path in paths)
        {
            var fullPath = RequiredFile(
                path,
                "Money Revenue crop-production Teacher layout snapshot");
            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                fullPath,
                "Money Revenue crop-production Teacher layout snapshot");
            ValidateSnapshot(snapshot);
            Require(snapshot.SaveId.Value == source.SaveId.Value &&
                    snapshot.PlayerId.Value == source.PlayerId.Value &&
                    ReadStateInt(snapshot, "time", "year") ==
                        ReadStateInt(source, "time", "year") &&
                    ReadStateString(snapshot, "time", "season") ==
                        ReadStateString(source, "time", "season") &&
                    ReadStateInt(snapshot, "time", "day") ==
                        ReadStateInt(source, "time", "day") &&
                    ReadStateInt(snapshot, "time", "total_days") ==
                        ReadStateInt(source, "time", "total_days") &&
                    ReadStateInt(snapshot, "time", "time") ==
                        ReadStateInt(source, "time", "time") &&
                    ReadStateInt(snapshot, "player", "money") ==
                        ReadStateInt(source, "player", "money") &&
                    ReadStateString(snapshot, "player", "location_id") ==
                        ReadStateString(source, "player", "location_id") &&
                    BootstrapArtifactGuard.EqualJson(
                        ReadStateValue(snapshot, "player", "inventory"),
                        ReadStateValue(source, "player", "inventory")),
                "Money Revenue crop-production Teacher layout snapshot drifted from its source boundary.");

            var context = TryReadStateValue(
                    snapshot,
                    "farm",
                    "site_preparation_context") ??
                ReadStateValue(
                    snapshot,
                    "current_location",
                    "site_preparation_context");
            Require(ReadString(context, "schema_version") ==
                        "farm_site_preparation_context.v2" &&
                    ReadString(context, "projection_status") ==
                        "complete_bounded_current_farm_region" &&
                    ReadString(context, "location_id") == "Farm",
                "Money Revenue crop-production Teacher layout projection is incomplete.");
            var bounds = RequiredProperty(context, "bounds");
            var minX = ReadInt(bounds, "min_x");
            var minY = ReadInt(bounds, "min_y");
            var maxX = ReadInt(bounds, "max_x");
            var maxY = ReadInt(bounds, "max_y");
            var mapWidth = ReadInt(context, "map_width");
            var mapHeight = ReadInt(context, "map_height");
            Require(minX >= 0 && minY >= 0 && maxX >= minX &&
                    maxY >= minY && mapWidth is >= 1 and <= 512 &&
                    mapHeight is >= 1 and <= 512 &&
                    maxX < mapWidth && maxY < mapHeight &&
                    maxX - minX + 1 <= 32 && maxY - minY + 1 <= 32,
                "Money Revenue crop-production Teacher layout bounds are invalid.");
            var rows = RequiredProperty(context, "tiles");
            Require(rows.ValueKind == JsonValueKind.Array,
                "Money Revenue crop-production Teacher layout tiles are missing.");
            var tiles = rows.EnumerateArray().Select(value =>
                new LayoutTile(
                    ReadInt(value, "tile_x"),
                    ReadInt(value, "tile_y"),
                    ReadString(value, "status"),
                    ReadRequiredBool(value, "map_passable"),
                    ReadRequiredBool(
                        value,
                        "native_blocked_without_characters"),
                    ReadRequiredBool(
                        value,
                        "watering_can_refill_source"),
                    ReadRequiredBool(
                        value,
                        "watering_refill_approach_standable"))).ToArray();
            Require(tiles.All(value =>
                    value.WateringRefillApproachStandable ==
                    (value.MapPassable &&
                     !value.NativeBlockedWithoutCharacters)),
                "Money Revenue crop-production Teacher layout refill " +
                "approach facts are internally inconsistent.");
            var expectedCount = checked((maxX - minX + 1) *
                (maxY - minY + 1));
            Require(tiles.Length == expectedCount &&
                    tiles.All(value => value.TileX >= minX &&
                        value.TileX <= maxX && value.TileY >= minY &&
                        value.TileY <= maxY) &&
                    tiles.Select(value => (value.TileX, value.TileY))
                        .Distinct().Count() == tiles.Length,
                "Money Revenue crop-production Teacher layout tile coverage is incomplete.");
            result.Add(new LayoutEvidence(
                new MoneyRevenueCropTeacherLayoutSnapshotBinding(
                    fullPath,
                    CurrentTeacherFrontierSupport.HashFile(fullPath),
                    snapshot.StateHash,
                    minX,
                    minY,
                    maxX,
                    maxY,
                    mapWidth,
                    mapHeight,
                    tiles.Length),
                tiles));
        }
        return result.ToArray();
    }

    private static bool HasVerifiedWateringRefillRoute(
        IReadOnlyCollection<LayoutTile> tiles,
        IReadOnlyCollection<LayoutTile> viableTiles,
        int mapWidth,
        int mapHeight,
        int anchorX,
        int anchorY)
    {
        var start = viableTiles
            .Where(value => value.WateringRefillApproachStandable)
            .OrderBy(value => Manhattan(
                value.TileX,
                value.TileY,
                anchorX,
                anchorY))
            .ThenBy(value => value.TileY)
            .ThenBy(value => value.TileX)
            .FirstOrDefault();
        if (start is null)
            return false;

        var blocked = tiles
            .Where(value => !value.WateringRefillApproachStandable)
            .Select(value => RouteGridPathfinder.Key(
                value.TileX,
                value.TileY))
            .ToHashSet(StringComparer.Ordinal);
        var noExtraBlockedTiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in tiles.Where(value =>
                     value.WateringCanRefillSource))
        {
            foreach (var stand in RouteGridPathfinder.Neighbors(
                         source.TileX,
                         source.TileY))
            {
                if (!RouteGridPathfinder.InBounds(
                        stand.X,
                        stand.Y,
                        mapWidth,
                        mapHeight) ||
                    blocked.Contains(RouteGridPathfinder.Key(
                        stand.X,
                        stand.Y)))
                {
                    continue;
                }

                if (RouteGridPathfinder.ShortestPathLength(
                        start.TileX,
                        start.TileY,
                        stand.X,
                        stand.Y,
                        mapWidth,
                        mapHeight,
                        blocked,
                        noExtraBlockedTiles).HasValue)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ReadRequiredBool(JsonElement value, string name)
    {
        var result = ReadOptionalBool(value, name);
        Require(result.HasValue,
            "Money Revenue crop-production Teacher layout tile is missing " +
            name + ".");
        return result.GetValueOrDefault();
    }

    private static MoneyRevenueCropPlantingTile[] SelectCompactTiles(
        LayoutTile[] source,
        int count,
        int anchorX,
        int anchorY)
    {
        Require(source.Length >= count,
            "Money Revenue crop-production Teacher has insufficient viable tiles.");
        var remaining = source.ToList();
        var selected = new List<LayoutTile>();
        while (selected.Count < count)
        {
            var next = remaining
                .OrderBy(value => StatusRank(value.Status))
                .ThenBy(value => selected.Count == 0
                    ? Manhattan(value.TileX, value.TileY, anchorX, anchorY)
                    : selected.Min(other => Manhattan(
                        value.TileX,
                        value.TileY,
                        other.TileX,
                        other.TileY)))
                .ThenBy(value => Manhattan(
                    value.TileX,
                    value.TileY,
                    anchorX,
                    anchorY))
                .ThenBy(value => value.TileY)
                .ThenBy(value => value.TileX)
                .First();
            selected.Add(next);
            remaining.Remove(next);
        }
        return selected.OrderBy(value => value.TileY)
            .ThenBy(value => value.TileX)
            .Select(value => new MoneyRevenueCropPlantingTile(
                value.TileX,
                value.TileY))
            .ToArray();
    }

    private static (int TileX, int TileY) ReadFarmAnchor(
        SnapshotEnvelope snapshot,
        LayoutTile[] fallback)
    {
        var bins = TryReadStateValue(snapshot, "farm", "shipping_bins");
        if (bins is { ValueKind: JsonValueKind.Array })
        {
            var rows = bins.Value.EnumerateArray()
                .Where(value => ReadOptionalBool(value, "completed") != false)
                .OrderBy(value => ReadInt(value, "tile_y"))
                .ThenBy(value => ReadInt(value, "tile_x"))
                .ToArray();
            if (rows.Length > 0)
                return (ReadInt(rows[0], "tile_x"),
                    ReadInt(rows[0], "tile_y"));
        }
        return (
            (int)Math.Round(fallback.Average(value => value.TileX)),
            (int)Math.Round(fallback.Average(value => value.TileY)));
    }

    private static int StatusRank(string status) => status switch
    {
        "already_prepared" => 0,
        "ready_to_till" => 1,
        "requires_single_clearance_then_till" => 2,
        _ => 9
    };

    private static int Manhattan(int x1, int y1, int x2, int y2) =>
        Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    private sealed record LayoutEvidence(
        MoneyRevenueCropTeacherLayoutSnapshotBinding Binding,
        LayoutTile[] Tiles);

    private sealed record LayoutTile(
        int TileX,
        int TileY,
        string Status,
        bool MapPassable,
        bool NativeBlockedWithoutCharacters,
        bool WateringCanRefillSource,
        bool WateringRefillApproachStandable);
}
