using System.Text.Json;
using StardewAI.Contracts.State;

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
            Require(ReadString(context, "projection_status") ==
                        "complete_bounded_current_farm_region" &&
                    ReadString(context, "location_id") == "Farm",
                "Money Revenue crop-production Teacher layout projection is incomplete.");
            var bounds = RequiredProperty(context, "bounds");
            var minX = ReadInt(bounds, "min_x");
            var minY = ReadInt(bounds, "min_y");
            var maxX = ReadInt(bounds, "max_x");
            var maxY = ReadInt(bounds, "max_y");
            Require(minX >= 0 && minY >= 0 && maxX >= minX &&
                    maxY >= minY && maxX - minX + 1 <= 32 &&
                    maxY - minY + 1 <= 32,
                "Money Revenue crop-production Teacher layout bounds are invalid.");
            var rows = RequiredProperty(context, "tiles");
            Require(rows.ValueKind == JsonValueKind.Array,
                "Money Revenue crop-production Teacher layout tiles are missing.");
            var tiles = rows.EnumerateArray().Select(value =>
                new LayoutTile(
                    ReadInt(value, "tile_x"),
                    ReadInt(value, "tile_y"),
                    ReadString(value, "status"))).ToArray();
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
                    tiles.Length),
                tiles));
        }
        return result.ToArray();
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

    private sealed record LayoutTile(int TileX, int TileY, string Status);
}
