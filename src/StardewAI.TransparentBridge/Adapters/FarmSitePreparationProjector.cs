using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using TileLocation = xTile.Dimensions.Location;

namespace StardewAI.TransparentBridge.Adapters;

internal static class FarmSitePreparationProjector
{
    public static object ReadContext(
        GameLocation location,
        FarmSitePreparationBounds bounds)
    {
        var mapWidth = location.map?.Layers
            .Cast<xTile.Layers.Layer>()
            .Select(layer => layer.LayerWidth)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        var mapHeight = location.map?.Layers
            .Cast<xTile.Layers.Layer>()
            .Select(layer => layer.LayerHeight)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        var resourceClumps = location.resourceClumps.ToArray();
        var hoeEntry = Game1.player.Items
            .Select((item, index) => new
            {
                Tool = item as Hoe,
                SlotIndex = index
            })
            .Where(row => row.Tool is not null)
            .OrderByDescending(row => row.Tool!.UpgradeLevel)
            .ThenBy(row => row.SlotIndex)
            .FirstOrDefault();
        var hoeEnergyCost = hoeEntry?.Tool?.isEfficient.Value == true
            ? 0d
            : Math.Max(0d, 2d - Game1.player.FarmingLevel * 0.1d);
        var rows = new List<object>();
        for (var y = bounds.MinY; ; y++)
        {
            for (var x = bounds.MinX; ; x++)
            {
                rows.Add(ReadTile(
                    location,
                    x,
                    y,
                    mapWidth,
                    mapHeight,
                    resourceClumps,
                    hoeEntry?.SlotIndex,
                    hoeEntry?.Tool?.QualifiedItemId ?? string.Empty,
                    hoeEnergyCost));
                if (x == bounds.MaxX)
                    break;
            }
            if (y == bounds.MaxY)
                break;
        }

        return new
        {
            schema_version = "farm_site_preparation_context.v1",
            projection_status = location is Farm
                ? "complete_bounded_current_farm_region"
                : "blocked_current_location_not_farm",
            location_id = location.NameOrUniqueName,
            is_farm = location is Farm,
            bounds = new
            {
                min_x = bounds.MinX,
                min_y = bounds.MinY,
                max_x = bounds.MaxX,
                max_y = bounds.MaxY
            },
            tile_count = rows.Count,
            hoe_tool_slot_index = hoeEntry?.SlotIndex,
            hoe_tool_qualified_item_id =
                hoeEntry?.Tool?.QualifiedItemId ?? string.Empty,
            hoe_energy_cost_per_single_tile_action = hoeEntry is null
                ? (double?)null
                : hoeEnergyCost,
            hoe_tool_status = hoeEntry is null
                ? "blocked_required_hoe_missing"
                : "ready",
            tiles = rows.ToArray(),
            native_contract =
                "Hoe.DoFunction -> existing terrain/object tool action first; otherwise Back:Diggable + isTilePassable + makeHoeDirt. Projection is bounded and read-only; clearance and tilling remain separate fresh-snapshot actions."
        };
    }

    private static object ReadTile(
        GameLocation location,
        int x,
        int y,
        int mapWidth,
        int mapHeight,
        ResourceClump[] allResourceClumps,
        int? hoeSlotIndex,
        string hoeQualifiedItemId,
        double hoeEnergyCost)
    {
        var tile = new Vector2(x, y);
        var onMap = x >= 0 && y >= 0 && x < mapWidth && y < mapHeight;
        var diggable = onMap &&
            location.doesTileHaveProperty(x, y, "Diggable", "Back") is not null;
        var mapPassable = onMap &&
            location.isTilePassable(new TileLocation(x, y), Game1.viewport);
        location.terrainFeatures.TryGetValue(tile, out var terrainFeature);
        location.objects.TryGetValue(tile, out var tileObject);
        var hoeDirt = terrainFeature as HoeDirt;
        var resourceClumps = allResourceClumps
            .Where(clump =>
                x >= clump.Tile.X &&
                x < clump.Tile.X + clump.width.Value &&
                y >= clump.Tile.Y &&
                y < clump.Tile.Y + clump.height.Value)
            .ToArray();
        var largeTerrainFeatureCount = location.largeTerrainFeatures.Count(
            feature => feature.getBoundingBox().Contains(
                x * 64 + 32,
                y * 64 + 32));
        var buildingPresent = location.getBuildingAt(tile) is not null;
        var nativeBlocked = onMap && location.IsTileBlockedBy(
            tile,
            ~(CollisionMask.Characters | CollisionMask.Farmers));

        var blockerKinds = new List<string>();
        var clearableBlockerKinds = new List<string>();
        if (tileObject is not null)
        {
            blockerKinds.Add("object");
            if (MayUseClearance(tileObject))
                clearableBlockerKinds.Add("object");
        }
        if (terrainFeature is not null && hoeDirt is null)
        {
            blockerKinds.Add("terrain_feature");
            if (terrainFeature is Grass ||
                terrainFeature is Tree tree && !tree.tapped.Value)
                clearableBlockerKinds.Add("terrain_feature");
        }
        if (resourceClumps.Length > 0)
        {
            blockerKinds.Add("resource_clump");
            if (resourceClumps.Length == 1 &&
                resourceClumps[0].parentSheetIndex.Value is
                    ResourceClump.stumpIndex or ResourceClump.hollowLogIndex)
                clearableBlockerKinds.Add("resource_clump");
        }
        if (largeTerrainFeatureCount > 0)
            blockerKinds.Add("large_terrain_feature");
        if (buildingPresent)
            blockerKinds.Add("building");
        if (nativeBlocked && blockerKinds.Count == 0)
            blockerKinds.Add("unclassified_native_blocker");

        var status = location is not Farm
            ? "blocked_current_location_not_farm"
            : !onMap
                ? "blocked_outside_map"
                : hoeDirt?.crop is not null
                    ? "blocked_existing_crop"
                    : hoeDirt is not null
                        ? "already_prepared"
                        : !diggable
                            ? "blocked_not_diggable"
                            : !mapPassable
                                ? "blocked_map_not_passable"
                                : blockerKinds.Count == 0 && !nativeBlocked
                                    ? "ready_to_till"
                                    : blockerKinds.Count == 1 &&
                                      clearableBlockerKinds.Count == 1
                                        ? "requires_single_clearance_then_till"
                                        : "blocked_multiple_or_unsupported_occupancy";

        return new
        {
            tile_x = x,
            tile_y = y,
            status,
            on_map = onMap,
            diggable_back_property = diggable,
            map_passable = mapPassable,
            native_blocked_without_characters = nativeBlocked,
            has_hoe_dirt = hoeDirt is not null,
            hoe_dirt_has_crop = hoeDirt?.crop is not null,
            hoe_tool_slot_index = hoeSlotIndex,
            hoe_tool_qualified_item_id = hoeQualifiedItemId,
            hoe_energy_cost = hoeSlotIndex.HasValue
                ? hoeEnergyCost
                : (double?)null,
            terrain_feature_runtime_type =
                terrainFeature?.GetType().FullName ?? string.Empty,
            object_qualified_item_id =
                tileObject?.QualifiedItemId ?? string.Empty,
            object_runtime_type =
                tileObject?.GetType().FullName ?? string.Empty,
            resource_clump_count = resourceClumps.Length,
            resource_clump_anchors = resourceClumps.Select(clump => new
            {
                tile_x = (int)clump.Tile.X,
                tile_y = (int)clump.Tile.Y,
                width = clump.width.Value,
                height = clump.height.Value,
                parent_sheet_index = clump.parentSheetIndex.Value
            }).ToArray(),
            large_terrain_feature_count = largeTerrainFeatureCount,
            building_present = buildingPresent,
            blocker_kinds = blockerKinds.ToArray(),
            clearable_blocker_kinds = clearableBlockerKinds.ToArray()
        };
    }

    private static bool MayUseClearance(StardewValley.Object item) =>
        item.GetType() == typeof(StardewValley.Object) &&
        (item.QualifiedItemId is "(O)343" or "(O)450" or
            "(O)294" or "(O)295" or "(O)590" or "(O)SeedSpot" ||
         item.QualifiedItemId.StartsWith(
             "(O)Weeds",
             StringComparison.OrdinalIgnoreCase) ||
         item.Name.Contains("weed", StringComparison.OrdinalIgnoreCase));
}
