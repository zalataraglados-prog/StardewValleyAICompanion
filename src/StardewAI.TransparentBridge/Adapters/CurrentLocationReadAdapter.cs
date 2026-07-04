using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class CurrentLocationReadAdapter : ReadAdapterBase
{
    public override string Domain => "current_location";
    public override int Priority => 30;

    public override StateAdapterResult Collect(long tick)
    {
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        var unavailable = location is null
            ? new[]
            {
                "current_location.identity",
                "current_location.display_name",
                "current_location.flags",
                "current_location.objects",
                "current_location.terrain_features",
                "current_location.warps",
                "current_location.map"
            }
            : Array.Empty<string>();

        return Section("current_location", new Dictionary<string, object>
        {
            ["identity"] = Field(location is null ? null : ReadIdentity(location), "Game1.currentLocation.Name/NameOrUniqueName", tick),
            ["display_name"] = Field(location is null ? null : ReadDisplayName(location), "Game1.currentLocation.GetDisplayName()/Name", tick),
            ["flags"] = Field(location is null ? null : ReadFlags(location), "Game1.currentLocation.IsOutdoors/IsFarm", tick),
            ["objects"] = Field(location is null ? null : ReadObjects(location), "Game1.currentLocation.objects", tick),
            ["terrain_features"] = Field(location is null ? null : ReadTerrainFeatures(location), "Game1.currentLocation.terrainFeatures", tick),
            ["warps"] = Field(location is null ? null : ReadWarps(location), "Game1.currentLocation.warps", tick),
            ["map"] = Field(location is null ? null : ReadMap(location), "Game1.currentLocation.map.Layers", tick)
        }, unavailable, location is null ? "unavailable" : "partial");
    }

    private static object ReadIdentity(GameLocation location)
    {
        return new
        {
            name = location.Name,
            name_or_unique_name = location.NameOrUniqueName,
            type = location.GetType().FullName
        };
    }

    private static string ReadDisplayName(GameLocation location)
    {
        return location.GetDisplayName() ?? location.Name;
    }

    private static object ReadFlags(GameLocation location)
    {
        return new
        {
            is_outdoors = location.IsOutdoors,
            is_farm = location.IsFarm
        };
    }

    private static object[] ReadObjects(GameLocation location)
    {
        return location.objects.Pairs
            .OrderBy(pair => pair.Key.X)
            .ThenBy(pair => pair.Key.Y)
            .Select(pair => ReadObject(pair.Key, pair.Value))
            .ToArray();
    }

    private static object ReadObject(Vector2 tile, StardewObject item)
    {
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            name = item.Name,
            display_name = item.DisplayName,
            stack = item.Stack,
            quality = item.Quality,
            type = item.GetType().FullName
        };
    }

    private static object[] ReadTerrainFeatures(GameLocation location)
    {
        return location.terrainFeatures.Pairs
            .OrderBy(pair => pair.Key.X)
            .ThenBy(pair => pair.Key.Y)
            .Select(pair => ReadTerrainFeature(pair.Key, pair.Value))
            .ToArray();
    }

    private static object ReadTerrainFeature(Vector2 tile, TerrainFeature feature)
    {
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            type = feature.GetType().FullName
        };
    }

    private static object[] ReadWarps(GameLocation location)
    {
        return location.warps
            .OrderBy(warp => warp.X)
            .ThenBy(warp => warp.Y)
            .ThenBy(warp => warp.TargetName, StringComparer.Ordinal)
            .Select(warp => new
            {
                x = warp.X,
                y = warp.Y,
                target_name = warp.TargetName,
                target_x = warp.TargetX,
                target_y = warp.TargetY,
                flip_farmer = warp.flipFarmer.Value,
                npc_only = warp.npcOnly.Value
            })
            .ToArray();
    }

    private static object ReadMap(GameLocation location)
    {
        var layers = location.map?.Layers
            .Cast<xTile.Layers.Layer>()
            .Select((layer, index) => new
            {
                index,
                id = layer.Id,
                width = layer.LayerWidth,
                height = layer.LayerHeight
            })
            .OrderBy(layer => layer.index)
            .ToArray();

        return new
        {
            id = location.map?.Id,
            width = layers?.Length > 0 ? layers.Max(layer => layer.width) : (int?)null,
            height = layers?.Length > 0 ? layers.Max(layer => layer.height) : (int?)null,
            layer_count = layers?.Length ?? 0,
            layers = layers ?? Array.Empty<object>()
        };
    }
}
