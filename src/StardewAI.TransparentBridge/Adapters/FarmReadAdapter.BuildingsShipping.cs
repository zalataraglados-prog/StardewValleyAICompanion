using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter : ReadAdapterBase
{
    private static object[] ReadBuildings(Farm farm)
    {
        return farm.buildings
            .Select(building => ReadBuildingRow(building))
            .Cast<object>()
            .OrderBy(building =>
            {
                var yObj = building.GetType().GetProperty("tile_y")?.GetValue(building);
                return yObj is int y ? y : 0;
            })
            .ThenBy(building =>
            {
                var xObj = building.GetType().GetProperty("tile_x")?.GetValue(building);
                return xObj is int x ? x : 0;
            })
            .ThenBy(building => building.GetType().GetProperty("type")?.GetValue(building) as string ?? string.Empty)
            .ToArray();
    }

    private static object ReadBuildingRow(Building building)
    {
        var type = building.buildingType.Value;
        var tileX = building.tileX.Value;
        var tileY = building.tileY.Value;
        var humanDoor = building.humanDoor;
        var indoors = building.GetIndoors();
        var hasIndoors = indoors is not null;
        var interiorWarps = hasIndoors ? indoors!.warps.ToArray() : Array.Empty<Warp>();

        int? humanDoorAbsoluteX = null;
        int? humanDoorAbsoluteY = null;
        int? exteriorStandX = null;
        int? exteriorStandY = null;
        int? exteriorEntryX = null;
        int? exteriorEntryY = null;
        string? indoorLocationId = null;
        int? indoorArrivalX = null;
        int? indoorArrivalY = null;

        if (humanDoor.X >= 0 && humanDoor.Y >= 0)
        {
            humanDoorAbsoluteX = tileX + humanDoor.X;
            humanDoorAbsoluteY = tileY + humanDoor.Y;
            exteriorEntryX = humanDoorAbsoluteX;
            exteriorEntryY = humanDoorAbsoluteY + 1;
            exteriorStandX = exteriorEntryX;
            exteriorStandY = exteriorEntryY;
        }

        if (hasIndoors)
        {
            indoorLocationId = indoors!.NameOrUniqueName;
            if (interiorWarps.Length > 0)
            {
                indoorArrivalX = interiorWarps[0].X;
                indoorArrivalY = interiorWarps[0].Y - 1;
            }
        }

        var constructionLeft = building.daysOfConstructionLeft.Value;
        var locked = building.daysOfConstructionLeft.Value > 0;
        var underConstruction = building.isUnderConstruction();

        object doorData;
        if (humanDoor.X >= 0 && humanDoor.Y >= 0)
        {
            doorData = new
            {
                human_door_relative_x = humanDoor.X,
                human_door_relative_y = humanDoor.Y,
                human_door_absolute_tile_x = humanDoorAbsoluteX,
                human_door_absolute_tile_y = humanDoorAbsoluteY,
                exterior_entry_tile_x = exteriorEntryX,
                exterior_entry_tile_y = exteriorEntryY,
                exterior_stand_tile_x = exteriorStandX,
                exterior_stand_tile_y = exteriorStandY,
                indoor_location_id = indoorLocationId,
                indoor_arrival_tile_x = indoorArrivalX,
                indoor_arrival_tile_y = indoorArrivalY,
                source_label = "Building.humanDoor; Building.GetIndoors(); Building.GetIndoors().warps[0]"
            };
        }
        else
        {
            doorData = new
            {
                human_door_unavailable = true,
                indoor_location_id = indoorLocationId,
                indoor_arrival_tile_x = indoorArrivalX,
                indoor_arrival_tile_y = indoorArrivalY,
                source_label = "Building.humanDoor absent; Building.GetIndoors()"
            };
        }

        return new
        {
            type,
            tile_x = tileX,
            tile_y = tileY,
            tiles_wide = building.tilesWide.Value,
            tiles_high = building.tilesHigh.Value,
            days_of_construction_left = constructionLeft,
            is_under_construction = underConstruction,
            is_locked_by_construction = locked,
            indoor_location_id = indoorLocationId,
            has_door_access_resolved = humanDoor.X >= 0 && humanDoor.Y >= 0 && hasIndoors,
            door_resolution_status = humanDoor.X >= 0 && humanDoor.Y >= 0
                ? (hasIndoors ? "resolved_building_door_connector" : "missing_indoor_location")
                : (hasIndoors ? "unresolved_human_door" : "unresolved_both_door_and_indoor"),
            door = doorData
        };
    }

    private static object[] ReadShippingBins(Farm farm)
    {
        var player = Game1.player;
        var playerId = player.UniqueMultiplayerID;
        var useSeparateWallets = player.team.useSeparateWallets.Value;
        var binInventory = farm.getShippingBin(player);
        var aggregateContents = ReadBinAggregateContents(binInventory);
        var contentsSignature = ComputeContentsSignature(aggregateContents);
        var contentsTotalCount = aggregateContents.Sum(c => c.count);
        var contentsDistinctCount = aggregateContents.Length;

        return farm.buildings
            .OfType<ShippingBin>()
            .Select(bin =>
            {
                var distanceToPlayer = Vector2.Distance(player.Tile, new Vector2(bin.tileX.Value + 0.5f, bin.tileY.Value));
                var completed = bin.daysOfConstructionLeft.Value <= 0;
                var standTiles = completed ? ComputeAllBinInteractionStandTiles(farm, bin, player) : Array.Empty<BinStandTileEntry>();
                var preferred = standTiles
                    .FirstOrDefault(t => t.map_passable && !t.blocked)
                    ?? standTiles.FirstOrDefault(t => t.map_passable);
                return new
                {
                    tile_x = bin.tileX.Value,
                    tile_y = bin.tileY.Value,
                    tiles_wide = bin.tilesWide.Value,
                    tiles_high = bin.tilesHigh.Value,
                    tile_width = bin.tilesWide.Value,
                    tile_height = bin.tilesHigh.Value,
                    days_of_construction_left = bin.daysOfConstructionLeft.Value,
                    completed,
                    distance_to_player = distanceToPlayer,
                    player_within_shipping_range = distanceToPlayer <= 2f,
                    interaction_stand_tile_x = preferred?.x,
                    interaction_stand_tile_y = preferred?.y,
                    interaction_stand_tile_blocked_reason = preferred?.blocked_reason,
                    stand_tiles = standTiles.Select(t => new { t.x, t.y, t.map_passable, t.blocked, t.blocked_reason }).ToArray(),
                    bin_scope = useSeparateWallets ? "personal" : "shared",
                    player_id = playerId,
                    contents = aggregateContents.Select(c => new
                    {
                        item_id = c.itemId,
                        qualified_item_id = c.qualifiedItemId,
                        count = c.count
                    }).ToArray(),
                    contents_total_count = contentsTotalCount,
                    contents_distinct_item_count = contentsDistinctCount,
                    contents_signature = contentsSignature,
                    contents_truncated = false
                };
            })
            .OrderBy(bin => bin.tile_y)
            .ThenBy(bin => bin.tile_x)
            .ToArray();
    }

    private sealed class BinStandTileEntry
    {
        public BinStandTileEntry(int x, int y, bool mapPassable, bool blocked, string? blockReason)
        {
            this.x = x;
            this.y = y;
            this.map_passable = mapPassable;
            this.blocked = blocked;
            this.blocked_reason = blockReason;
        }

        public readonly int x;
        public readonly int y;
        public readonly bool map_passable;
        public readonly bool blocked;
        public readonly string? blocked_reason;
    }

    private static BinStandTileEntry[] ComputeAllBinInteractionStandTiles(Farm farm, ShippingBin bin, Farmer player)
    {
        var binX = bin.tileX.Value;
        var binY = bin.tileY.Value;
        var binW = bin.tilesWide.Value;
        var binH = bin.tilesHigh.Value;
        var centerX = binX + 0.5;
        var centerY = (float)binY;

        var tileEntries = new List<BinStandTileEntry>();
        var minX = binX - 2;
        var maxX = binX + binW + 1;
        var minY = binY - 2;
        var maxY = binY + binH + 1;

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (IsTileInBuildingFootprint(x, y, binX, binY, binW, binH))
                    continue;

                var dx = x - centerX;
                var dy = y - centerY;
                if (Math.Sqrt(dx * dx + dy * dy) > 2.0)
                    continue;

                var mapPassable = IsTilePassableForInteraction(farm, x, y);
                var dynamicBlocked = IsTileDynamicallyBlocked(farm, x, y);
                var blocked = !mapPassable || dynamicBlocked;
                string? blockReason = null;
                if (!mapPassable && dynamicBlocked)
                    blockReason = "map_and_dynamic_blocked";
                else if (!mapPassable)
                    blockReason = "static_map_blocked";
                else if (dynamicBlocked)
                    blockReason = "dynamic_transient_blocked";

                tileEntries.Add(new BinStandTileEntry(x, y, mapPassable, blocked, blockReason));
            }
        }

        return tileEntries
            .OrderBy(t => Math.Abs(player.TilePoint.X - t.x) + Math.Abs(player.TilePoint.Y - t.y))
            .ThenBy(t => t.y)
            .ThenBy(t => t.x)
            .ToArray();
    }

    private static bool IsTileInBuildingFootprint(int x, int y, int binX, int binY, int binW, int binH)
    {
        return x >= binX && x < binX + binW && y >= binY && y < binY + binH;
    }

    private static bool IsTileDynamicallyBlocked(GameLocation location, int x, int y)
    {
        return location.isCollidingPosition(
            new Microsoft.Xna.Framework.Rectangle(x * 64 + 1, y * 64 + 1, 62, 62),
            Game1.viewport,
            isFarmer: true,
            damagesFarmer: 0,
            glider: false,
            Game1.player,
            pathfinding: true);
    }

    private sealed class BinContentEntry
    {
        public readonly string itemId;
        public readonly string qualifiedItemId;
        public readonly int count;

        public BinContentEntry(string itemId, string qualifiedItemId, int count)
        {
            this.itemId = itemId;
            this.qualifiedItemId = qualifiedItemId;
            this.count = count;
        }
    }

    private static BinContentEntry[] ReadBinAggregateContents(object? binInventory)
    {
        if (binInventory == null)
            return Array.Empty<BinContentEntry>();

        var items = binInventory as System.Collections.IEnumerable;
        if (items == null)
        {
            var itemsProp = binInventory.GetType().GetProperty("Items",
                BindingFlags.Instance | BindingFlags.Public);
            if (itemsProp == null)
                return Array.Empty<BinContentEntry>();
            items = itemsProp.GetValue(binInventory) as System.Collections.IEnumerable;
            if (items == null)
                return Array.Empty<BinContentEntry>();
        }

        var dict = new Dictionary<string, BinContentEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in items)
        {
            if (obj is not Item stardewItem || stardewItem.Stack <= 0)
                continue;
            var qId = stardewItem.QualifiedItemId ?? string.Empty;
            if (dict.TryGetValue(qId, out var existing))
            {
                dict[qId] = new BinContentEntry(existing.itemId, existing.qualifiedItemId, existing.count + stardewItem.Stack);
            }
            else
            {
                dict[qId] = new BinContentEntry(stardewItem.ItemId ?? string.Empty, qId, stardewItem.Stack);
            }
        }

        return dict.Values
            .OrderBy(e => e.qualifiedItemId, StringComparer.Ordinal)
            .ThenBy(e => e.itemId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ComputeContentsSignature(BinContentEntry[] contents)
    {
        var sb = new StringBuilder();
        foreach (var entry in contents)
        {
            sb.Append(entry.qualifiedItemId);
            sb.Append('|');
            sb.Append(entry.count);
            sb.Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsTilePassableForInteraction(GameLocation location, int x, int y)
    {
        if (x < 0 || y < 0 || x >= location.map.Layers[0].LayerWidth || y >= location.map.Layers[0].LayerHeight)
            return false;
        var tileLoc = new xTile.Dimensions.Location(x, y);
        return location.isTilePassable(tileLoc, Game1.viewport);
    }

}
