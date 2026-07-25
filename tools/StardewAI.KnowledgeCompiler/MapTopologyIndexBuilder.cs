using System.Globalization;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class MapTopologyIndexBuilder
{
    private static readonly string[] WarpPropertyNames = ["NPCWarp", "Warp"];
    private static readonly string[] InteractionPropertyNames = ["Action", "TouchAction"];

    public MapTopologyIndex Build(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        var maps = new List<MapTopologyMap>();
        var nonMapAssets = new List<MapNonMapAsset>();
        var issues = new List<MapTopologyIssue>();

        foreach (var asset in assets.Values
                     .Where(row => row.AssetName.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(row => row.AssetName, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryString(asset.Payload, "AssetKind", out var assetKind))
            {
                issues.Add(new("blocking", "map_asset_kind_missing", asset.AssetName, "payload.AssetKind"));
                continue;
            }

            var runtimeType = String(asset.Payload, "RuntimeType") ?? string.Empty;
            if (!string.Equals(assetKind, "x_tile_map", StringComparison.Ordinal))
            {
                nonMapAssets.Add(new(asset.AssetName, runtimeType, assetKind));
                continue;
            }

            if (!asset.Payload.TryGetProperty("Map", out var map) || map.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("blocking", "x_tile_map_projection_missing", asset.AssetName, "payload.Map"));
                continue;
            }

            maps.Add(BuildMap(asset.AssetName, map, issues));
        }

        return new(
            maps,
            nonMapAssets,
            issues,
            new(
                maps.Count,
                nonMapAssets.Count,
                maps.Sum(row => row.Layers.Count),
                maps.Sum(row => row.Layers.Sum(layer => layer.OccupiedTileCount)),
                maps.Sum(row => row.Warps.Count),
                maps.Sum(row => row.Interactions.Count),
                maps.Sum(row => row.BasePassability.BlockedTileCount),
                issues.Count(row => row.Severity == "blocking")));
    }

    private static MapTopologyMap BuildMap(
        string assetName,
        JsonElement map,
        ICollection<MapTopologyIssue> issues)
    {
        var layers = new List<MapLayerTopology>();
        var sourceLayers = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (map.TryGetProperty("Layers", out var layerArray) && layerArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var layer in layerArray.EnumerateArray())
            {
                var id = String(layer, "Id") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(new("blocking", "map_layer_id_missing", assetName, "payload.Map.Layers"));
                    continue;
                }

                sourceLayers[id] = layer;
                var positions = EnumerateTiles(layer)
                    .Select(tile => new TilePoint(Int(tile, "X"), Int(tile, "Y")))
                    .Where(point => point.X is not null && point.Y is not null)
                    .Select(point => (point.X!.Value, point.Y!.Value))
                    .Distinct()
                    .ToArray();
                layers.Add(new(
                    id,
                    Int(layer, "Width") ?? 0,
                    Int(layer, "Height") ?? 0,
                    positions.Length,
                    ToRuns(positions)));
            }
        }
        else
        {
            issues.Add(new("blocking", "map_layers_missing", assetName, "payload.Map.Layers"));
        }

        var interactions = BuildInteractions(assetName, sourceLayers);
        var warps = BuildWarps(assetName, map, issues);
        var passability = BuildBasePassability(sourceLayers);

        var width = layers.Count == 0 ? 0 : layers.Max(row => row.Width);
        var height = layers.Count == 0 ? 0 : layers.Max(row => row.Height);
        return new(
            assetName,
            String(map, "Id") ?? string.Empty,
            width,
            height,
            layers.OrderBy(row => row.Id, StringComparer.Ordinal).ToArray(),
            warps,
            interactions,
            passability);
    }

    private static IReadOnlyList<MapWarp> BuildWarps(
        string assetName,
        JsonElement map,
        ICollection<MapTopologyIssue> issues)
    {
        var result = new List<MapWarp>();
        if (!map.TryGetProperty("Properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var propertyName in WarpPropertyNames)
        {
            foreach (var property in properties.EnumerateArray()
                         .Where(row => string.Equals(String(row, "Name"), propertyName, StringComparison.Ordinal)))
            {
                var raw = String(property, "Value") ?? string.Empty;
                var fields = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var index = 0; index < fields.Length; index += 5)
                {
                    var hasFiveFields = fields.Length >= index + 5;
                    if (!hasFiveFields ||
                        !int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromX) ||
                        !int.TryParse(fields[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromY) ||
                        !int.TryParse(fields[index + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var toX) ||
                        !int.TryParse(fields[index + 4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var toY))
                    {
                        issues.Add(new(
                            "blocking",
                            "native_map_warp_parse_failure",
                            assetName,
                            $"{propertyName}[{index / 5}]={string.Join(' ', fields.Skip(index))}"));
                        continue;
                    }

                    result.Add(new(
                        propertyName,
                        propertyName == "NPCWarp",
                        fromX,
                        fromY,
                        fields[index + 2],
                        toX,
                        toY,
                        raw,
                        "GameLocation.updateWarps / ArgUtility.SplitBySpace / exact five-field group"));
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<MapInteraction> BuildInteractions(
        string assetName,
        IReadOnlyDictionary<string, JsonElement> layers)
    {
        var result = new List<MapInteraction>();
        foreach (var (layerId, layer) in layers.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            foreach (var tile in EnumerateTiles(layer))
            {
                var x = Int(tile, "X");
                var y = Int(tile, "Y");
                if (x is null || y is null)
                    continue;

                foreach (var propertyName in InteractionPropertyNames)
                {
                    var direct = FindProperty(tile, "Properties", propertyName);
                    var inherited = FindProperty(tile, "TileIndexProperties", propertyName);
                    if (direct is not null)
                    {
                        result.Add(new(
                            assetName,
                            layerId,
                            x.Value,
                            y.Value,
                            propertyName,
                            direct,
                            "tile_property",
                            true));
                    }
                    if (inherited is not null)
                    {
                        result.Add(new(
                            assetName,
                            layerId,
                            x.Value,
                            y.Value,
                            propertyName,
                            inherited,
                            "tile_index_property",
                            direct is null));
                    }
                }
            }
        }

        return result
            .OrderBy(row => row.Layer, StringComparer.Ordinal)
            .ThenBy(row => row.Y)
            .ThenBy(row => row.X)
            .ThenBy(row => row.PropertyName, StringComparer.Ordinal)
            .ThenBy(row => row.Source, StringComparer.Ordinal)
            .ToArray();
    }

    private static MapBasePassability BuildBasePassability(
        IReadOnlyDictionary<string, JsonElement> layers)
    {
        if (!layers.TryGetValue("Back", out var back) ||
            !layers.TryGetValue("Buildings", out var buildings))
        {
            return new(
                "not_applicable_missing_back_or_buildings_layer",
                0,
                0,
                0,
                Array.Empty<TileRun>(),
                "GameLocation.isTilePassable requires Back and Buildings; this projected map may be a map fragment.");
        }

        var width = Int(back, "Width") ?? 0;
        var height = Int(back, "Height") ?? 0;
        var blocked = new HashSet<(int X, int Y)>();

        foreach (var tile in EnumerateTiles(back))
        {
            var x = Int(tile, "X");
            var y = Int(tile, "Y");
            if (x is not null && y is not null && HasProperty(tile, "TileIndexProperties", "Passable"))
                blocked.Add((x.Value, y.Value));
        }

        foreach (var tile in EnumerateTiles(buildings))
        {
            var x = Int(tile, "X");
            var y = Int(tile, "Y");
            if (x is null || y is null)
                continue;
            var shadow = HasProperty(tile, "TileIndexProperties", "Shadow");
            var passable = HasProperty(tile, "TileIndexProperties", "Passable");
            if (!shadow && !passable)
                blocked.Add((x.Value, y.Value));
        }

        return new(
            "authoritative_static_base_rule",
            width,
            height,
            blocked.Count,
            ToRuns(blocked),
            "Exact GameLocation.isTilePassable static map rule. Dynamic buildings, furniture, objects, characters, events, and location overrides remain runtime context.");
    }

    private static IEnumerable<JsonElement> EnumerateTiles(JsonElement layer)
    {
        if (layer.TryGetProperty("Tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
            return tiles.EnumerateArray();
        return Array.Empty<JsonElement>();
    }

    private static IReadOnlyList<TileRun> ToRuns(IEnumerable<(int X, int Y)> positions)
    {
        var result = new List<TileRun>();
        foreach (var row in positions.GroupBy(point => point.Y).OrderBy(group => group.Key))
        {
            var ordered = row.Select(point => point.X).Distinct().OrderBy(value => value).ToArray();
            if (ordered.Length == 0)
                continue;

            var start = ordered[0];
            var end = start;
            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index] == end + 1)
                {
                    end = ordered[index];
                    continue;
                }
                result.Add(new(row.Key, start, end));
                start = end = ordered[index];
            }
            result.Add(new(row.Key, start, end));
        }
        return result;
    }

    private static string? FindProperty(JsonElement tile, string collectionName, string propertyName)
    {
        if (!tile.TryGetProperty(collectionName, out var properties) ||
            properties.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var property in properties.EnumerateArray())
        {
            if (string.Equals(String(property, "Name"), propertyName, StringComparison.Ordinal))
                return String(property, "Value") ?? string.Empty;
        }
        return null;
    }

    private static bool HasProperty(JsonElement tile, string collectionName, string propertyName) =>
        FindProperty(tile, collectionName, propertyName) is not null;

    private static bool TryString(JsonElement value, string propertyName, out string result)
    {
        result = String(value, propertyName) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(result);
    }

    private static string? String(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? Int(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var result)
            ? result
            : null;

    private sealed record TilePoint(int? X, int? Y);
}

internal sealed record MapTopologyIndex(
    IReadOnlyList<MapTopologyMap> Maps,
    IReadOnlyList<MapNonMapAsset> NonMapAssets,
    IReadOnlyList<MapTopologyIssue> Issues,
    MapTopologySummary Summary);

internal sealed record MapTopologySummary(
    int MapCount,
    int NonMapAssetCount,
    int LayerCount,
    int OccupiedTileCount,
    int WarpCount,
    int InteractionPropertyCount,
    int StaticBlockedTileCount,
    int BlockingIssueCount);

internal sealed record MapTopologyMap(
    string AssetName,
    string MapId,
    int Width,
    int Height,
    IReadOnlyList<MapLayerTopology> Layers,
    IReadOnlyList<MapWarp> Warps,
    IReadOnlyList<MapInteraction> Interactions,
    MapBasePassability BasePassability);

internal sealed record MapLayerTopology(
    string Id,
    int Width,
    int Height,
    int OccupiedTileCount,
    IReadOnlyList<TileRun> OccupiedRuns);

internal sealed record TileRun(int Y, int StartX, int EndX);

internal sealed record MapWarp(
    string PropertyName,
    bool NpcOnly,
    int FromX,
    int FromY,
    string DestinationLocation,
    int DestinationX,
    int DestinationY,
    string Raw,
    string NativeParser);

internal sealed record MapInteraction(
    string AssetName,
    string Layer,
    int X,
    int Y,
    string PropertyName,
    string Value,
    string Source,
    bool EffectiveUnderNativePropertyPrecedence);

internal sealed record MapBasePassability(
    string Status,
    int Width,
    int Height,
    int BlockedTileCount,
    IReadOnlyList<TileRun> BlockedRuns,
    string Authority);

internal sealed record MapNonMapAsset(string AssetName, string RuntimeType, string AssetKind);

internal sealed record MapTopologyIssue(string Severity, string Code, string Subject, string Detail);
