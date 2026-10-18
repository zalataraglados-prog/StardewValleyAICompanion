using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using TileLocation = xTile.Dimensions.Location;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static StateAdapterResult CollectFarmSitePreparation(
        GameLocation location,
        FarmSitePreparationBounds? bounds,
        long tick)
    {
        var source =
            "bounded current Farm region requested by farm_layout snapshot profile";
        if (bounds is null)
        {
            return Section(
                "current_location",
                new Dictionary<string, object>
                {
                    ["identity"] = Field(
                        ReadIdentity(location),
                        "Game1.currentLocation.Name/NameOrUniqueName",
                        tick),
                    ["flags"] = Field(
                        ReadFlags(location),
                        "Game1.currentLocation.IsOutdoors/IsFarm",
                        tick),
                    ["objects"] = Unavailable(
                        "farm_layout_bounds_missing_or_invalid",
                        source,
                        tick,
                        "snapshot_profile"),
                    ["terrain_features"] = Unavailable(
                        "farm_layout_bounds_missing_or_invalid",
                        source,
                        tick,
                        "snapshot_profile"),
                    ["site_preparation_context"] = Unavailable(
                        "farm_layout_bounds_missing_or_invalid",
                        source,
                        tick,
                        "snapshot_profile"),
                    ["map"] = Field(
                        ReadMap(location),
                        "Game1.currentLocation.map.Layers",
                        tick)
                },
                new[]
                {
                    "current_location.objects",
                    "current_location.terrain_features",
                    "current_location.site_preparation_context"
                },
                "partial");
        }

        var boundedObjects = location.objects.Pairs
            .Where(pair => Inside(bounds, pair.Key))
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => ReadObject(
                location,
                pair.Key,
                pair.Value,
                Game1.player))
            .ToArray();
        var boundedTerrainFeatures = location.terrainFeatures.Pairs
            .Where(pair => Inside(bounds, pair.Key))
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => ReadTerrainFeature(pair.Key, pair.Value))
            .ToArray();
        return Section(
            "current_location",
            new Dictionary<string, object>
            {
                ["identity"] = Field(
                    ReadIdentity(location),
                    "Game1.currentLocation.Name/NameOrUniqueName",
                    tick),
                ["flags"] = Field(
                    ReadFlags(location),
                    "Game1.currentLocation.IsOutdoors/IsFarm",
                    tick),
                ["objects"] = Field(
                    boundedObjects,
                    source + "; Game1.currentLocation.objects",
                    tick),
                ["terrain_features"] = Field(
                    boundedTerrainFeatures,
                    source + "; Game1.currentLocation.terrainFeatures",
                    tick),
                ["site_preparation_context"] = Field(
                    FarmSitePreparationProjector.ReadContext(
                        location,
                        bounds),
                    source + "; native diggable, passability and occupancy queries",
                    tick,
                    "vanilla_1_6_15_farm_site_preparation"),
                ["map"] = Field(
                    ReadMap(location),
                    "Game1.currentLocation.map.Layers",
                    tick)
            },
            Array.Empty<string>(),
            "partial");
    }

    private static bool Inside(
        FarmSitePreparationBounds bounds,
        Vector2 tile) =>
        tile.X >= bounds.MinX && tile.X <= bounds.MaxX &&
        tile.Y >= bounds.MinY && tile.Y <= bounds.MaxY;
}
