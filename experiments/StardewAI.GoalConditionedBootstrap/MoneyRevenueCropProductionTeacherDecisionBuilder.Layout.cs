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
            var tiles = rows.EnumerateArray()
                .Select(ReadLayoutTile)
                .ToArray();
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

    private static LayoutTile ReadLayoutTile(JsonElement value)
    {
        var status = ReadString(value, "status");
        var mapPassable = ReadRequiredBool(value, "map_passable");
        var nativeBlocked = ReadRequiredBool(
            value,
            "native_blocked_without_characters");
        var projectedStandable = ReadRequiredBool(
            value,
            "watering_refill_approach_standable");
        var walkingCollisionBlocked = ReadOptionalBool(
            value,
            "native_walking_collision_blocked");
        var legacyPreparedSoil =
            !walkingCollisionBlocked.HasValue &&
            status == "already_prepared" &&
            mapPassable;
        var effectiveStandable = walkingCollisionBlocked.HasValue
            ? mapPassable && !walkingCollisionBlocked.Value
            : projectedStandable || legacyPreparedSoil;
        Require(
            !walkingCollisionBlocked.HasValue ||
            projectedStandable == effectiveStandable,
            "Money Revenue crop-production Teacher layout refill " +
            "walking-collision facts are internally inconsistent.");

        return new LayoutTile(
            ReadInt(value, "tile_x"),
            ReadInt(value, "tile_y"),
            status,
            mapPassable,
            nativeBlocked,
            ReadRequiredBool(value, "watering_can_refill_source"),
            effectiveStandable);
    }

    public const int MaxWateringRefillPreparationTileCount = 4;

    private static WateringRefillRouteProof BuildWateringRefillRouteProof(
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
            return WateringRefillRouteProof.Unverified;

        var byCoordinate = tiles.ToDictionary(
            value => (value.TileX, value.TileY));
        var goals = tiles
            .Where(value => value.WateringCanRefillSource)
            .SelectMany(value => RouteGridPathfinder.Neighbors(
                value.TileX,
                value.TileY))
            .Where(value =>
                RouteGridPathfinder.InBounds(
                    value.X,
                    value.Y,
                    mapWidth,
                    mapHeight) &&
                byCoordinate.TryGetValue((value.X, value.Y), out var tile) &&
                (tile.WateringRefillApproachStandable ||
                    tile.Status == "requires_single_clearance_then_till"))
            .Select(value => (value.X, value.Y))
            .ToHashSet();
        if (goals.Count == 0)
            return WateringRefillRouteProof.Unverified;

        var startCoordinate = (start.TileX, start.TileY);
        var costs = new Dictionary<(int X, int Y), RouteCost>
        {
            [startCoordinate] = new RouteCost(0, 0)
        };
        var previous = new Dictionary<(int X, int Y), (int X, int Y)>();
        var frontier = new PriorityQueue<
            (int X, int Y),
            (int ClearanceCount, int PathLength, int Y, int X)>();
        frontier.Enqueue(
            startCoordinate,
            (0, 0, start.TileY, start.TileX));
        (int X, int Y)? reached = null;
        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (!costs.TryGetValue(current, out var currentCost) ||
                currentCost.ClearanceCount != priority.ClearanceCount ||
                currentCost.PathLength != priority.PathLength)
            {
                continue;
            }
            if (goals.Contains(current))
            {
                reached = current;
                break;
            }

            foreach (var next in RouteGridPathfinder.Neighbors(
                         current.X,
                         current.Y))
            {
                if (!RouteGridPathfinder.InBounds(
                        next.X,
                        next.Y,
                        mapWidth,
                        mapHeight) ||
                    !byCoordinate.TryGetValue((next.X, next.Y), out var tile))
                {
                    continue;
                }
                var requiresClearance = tile.Status ==
                    "requires_single_clearance_then_till";
                if (!tile.WateringRefillApproachStandable &&
                    !requiresClearance)
                {
                    continue;
                }
                var nextCost = new RouteCost(
                    currentCost.ClearanceCount + (requiresClearance ? 1 : 0),
                    currentCost.PathLength + 1);
                if (nextCost.ClearanceCount >
                    MaxWateringRefillPreparationTileCount ||
                    costs.TryGetValue((next.X, next.Y), out var oldCost) &&
                    oldCost.CompareTo(nextCost) <= 0)
                {
                    continue;
                }
                costs[(next.X, next.Y)] = nextCost;
                previous[(next.X, next.Y)] = current;
                frontier.Enqueue(
                    (next.X, next.Y),
                    (nextCost.ClearanceCount,
                        nextCost.PathLength,
                        next.Y,
                        next.X));
            }
        }
        if (!reached.HasValue)
            return WateringRefillRouteProof.Unverified;

        var path = new List<(int X, int Y)> { reached.Value };
        while (path[^1] != startCoordinate)
            path.Add(previous[path[^1]]);
        path.Reverse();
        var preparationTiles = path
            .Select(value => byCoordinate[value])
            .Where(value => value.Status ==
                "requires_single_clearance_then_till")
            .Select(value => new MoneyRevenueCropPlantingTile(
                value.TileX,
                value.TileY))
            .Distinct()
            .ToArray();
        return new WateringRefillRouteProof(
            true,
            path.Count - 1,
            preparationTiles,
            new MoneyRevenueCropPlantingTile(
                reached.Value.X,
                reached.Value.Y));
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

    private readonly record struct RouteCost(
        int ClearanceCount,
        int PathLength) : IComparable<RouteCost>
    {
        public int CompareTo(RouteCost other)
        {
            var clearance = ClearanceCount.CompareTo(other.ClearanceCount);
            return clearance != 0
                ? clearance
                : PathLength.CompareTo(other.PathLength);
        }
    }

    private sealed record WateringRefillRouteProof(
        bool Verified,
        int PathLength,
        MoneyRevenueCropPlantingTile[] PreparationTiles,
        MoneyRevenueCropPlantingTile? ApproachTile)
    {
        public static WateringRefillRouteProof Unverified { get; } = new(
            false,
            0,
            Array.Empty<MoneyRevenueCropPlantingTile>(),
            null);
    }
}
