using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static class NativeSleepTargetProjection
{
    public static NativeSleepApproachTile? SelectApproachTile(
        SnapshotEnvelope snapshot,
        int bedX,
        int bedY)
    {
        var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
        var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
        var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
        if (!startX.HasValue ||
            !startY.HasValue ||
            !grid.HasValue ||
            grid.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var width = ReadInt(grid.Value, "width");
        var height = ReadInt(grid.Value, "height");
        if (!RouteGridPathfinder.InBounds(
                startX.Value,
                startY.Value,
                width,
                height))
        {
            return null;
        }

        var blocked = RouteGridPathfinder.ReadBlockedTiles(grid.Value);
        var unsupported = RouteGridPathfinder.ReadUnsupportedActionTiles(snapshot);
        return RouteGridPathfinder.Neighbors(bedX, bedY)
            .Select(tile => TryBindApproachTile(
                snapshot,
                grid.Value,
                startX.Value,
                startY.Value,
                tile.X,
                tile.Y,
                width,
                height,
                blocked,
                unsupported))
            .Where(tile => tile is not null)
            .Select(tile => tile!)
            .OrderBy(tile => tile.Distance)
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .FirstOrDefault();
    }

    private static NativeSleepApproachTile? TryBindApproachTile(
        SnapshotEnvelope snapshot,
        JsonElement grid,
        int startX,
        int startY,
        int targetX,
        int targetY,
        int width,
        int height,
        ISet<string> blocked,
        ISet<string> unsupported)
    {
        if (!RouteGridPathfinder.InBounds(targetX, targetY, width, height))
        {
            return null;
        }

        var key = RouteGridPathfinder.Key(targetX, targetY);
        if (blocked.Contains(key))
        {
            return null;
        }

        var isNativeSleepTouchTile = IsNativeSleepTouchTile(
            snapshot,
            grid,
            targetX,
            targetY);
        var effectiveUnsupported = new HashSet<string>(
            unsupported,
            StringComparer.Ordinal);
        if (isNativeSleepTouchTile)
        {
            effectiveUnsupported.Remove(key);
        }

        var distance = RouteGridPathfinder.ShortestPathLength(
            startX,
            startY,
            targetX,
            targetY,
            width,
            height,
            blocked,
            effectiveUnsupported);
        return distance.HasValue
            ? new NativeSleepApproachTile(
                targetX,
                targetY,
                distance.Value,
                isNativeSleepTouchTile)
            : null;
    }

    private static bool IsNativeSleepTouchTile(
        SnapshotEnvelope snapshot,
        JsonElement grid,
        int x,
        int y)
    {
        if (!grid.TryGetProperty("notable_tiles", out var tiles) ||
            tiles.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var exactTouchTile = tiles.EnumerateArray().Any(tile =>
            tile.ValueKind == JsonValueKind.Object &&
            ReadInt(tile, "tile_x") == x &&
            ReadInt(tile, "tile_y") == y &&
            ReadBool(tile, "collision_blocked") != true &&
            FirstToken(ReadString(tile, "touch_action")) == "Sleep");
        if (!exactTouchTile)
        {
            return false;
        }

        var coverage = ReadStateFieldValue(
            snapshot,
            "locations",
            "route_action_branch_coverage");
        return coverage.HasValue &&
            coverage.Value.ValueKind == JsonValueKind.Object &&
            coverage.Value.TryGetProperty("rows", out var rows) &&
            rows.ValueKind == JsonValueKind.Array &&
            rows.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == x &&
                ReadInt(row, "tile_y") == y &&
                string.Equals(
                    ReadString(row, "branch"),
                    "Sleep",
                    StringComparison.Ordinal));
    }

    private static string FirstToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)[0];
    }
}

public sealed record NativeSleepApproachTile(
    int X,
    int Y,
    int Distance,
    bool IsNativeSleepTouchTile);
