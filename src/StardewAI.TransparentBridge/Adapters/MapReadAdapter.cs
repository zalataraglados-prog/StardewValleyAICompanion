using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class MapReadAdapter : ReadAdapterBase
{
    public override string Domain => "maps";
    public override int Priority => 60;

    public override StateAdapterResult Collect(long tick)
    {
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        var map = location?.Map;
        var layers = map?.Layers
            .Select(layer => new
            {
                id = layer.Id,
                width = layer.LayerWidth,
                height = layer.LayerHeight,
                tile_width = layer.TileWidth,
                tile_height = layer.TileHeight
            })
            .ToArray();

        return Section("locations", new Dictionary<string, object>
        {
            ["current_map"] = Field(location?.NameOrUniqueName, "Game1.currentLocation.NameOrUniqueName", tick),
            ["display_name"] = Field(location?.DisplayName, "Game1.currentLocation.DisplayName", tick),
            ["is_outdoors"] = Field(location?.IsOutdoors, "Game1.currentLocation.IsOutdoors", tick),
            ["map_layers"] = Field(layers, "Game1.currentLocation.Map.Layers", tick),
            ["object_count"] = Field(location?.Objects.Pairs.Count(), "Game1.currentLocation.Objects", tick),
            ["terrain_feature_count"] = Field(location?.terrainFeatures.Pairs.Count(), "Game1.currentLocation.terrainFeatures", tick),
            ["collision_grid"] = Unavailable("collision_grid_adapter_not_implemented", "GameLocation.isCollidingPosition/isTilePassable", tick)
        }, new[]
        {
            "locations.collision_grid",
            "locations.pathfinding_graph",
            "locations.interactable_tiles"
        });
    }
}
