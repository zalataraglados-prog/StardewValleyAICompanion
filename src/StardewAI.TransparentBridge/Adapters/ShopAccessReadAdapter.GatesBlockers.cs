using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter : ReadAdapterBase
{
    private static object ReadRouteGateContext()
    {
        var location = Game1.currentLocation;
        var buildConditions = location.getMapProperty("BuildConditions");
        var actionGates = ReadActionGates(location);

        return new
        {
            location_id = location.NameOrUniqueName,
            current_time = Game1.timeOfDay,
            current_day = Game1.dayOfMonth,
            current_day_name = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth),
            current_year = Game1.year,
            has_town_key = Game1.player?.HasTownKey ?? false,
            festival_day = Utility.isFestivalDay(),
            stores_closed_for_festival = GameLocation.AreStoresClosedForFestival(),
            in_valley_context = location.InValleyContext(),
            green_raining_here = location.IsGreenRainingHere(),
            build_conditions = string.IsNullOrWhiteSpace(buildConditions) ? null : buildConditions,
            build_conditions_met = string.IsNullOrWhiteSpace(buildConditions) ? (bool?)null : GameStateQuery.CheckConditions(buildConditions, location),
            action_gate_count = actionGates.Length,
            action_gates = actionGates,
            route_planner_enabled = true,
            route_planner_scope = "current_location_first_connector_with_fresh_snapshot_replan"
        };
    }

    private static object[] ReadActionGates(GameLocation location)
    {
        var map = location.map;
        if (map?.Layers is null || map.Layers.Count == 0)
        {
            return Array.Empty<object>();
        }

        var width = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerWidth);
        var height = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerHeight);
        var gates = new List<object>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                var touchAction = location.doesTileHaveProperty(x, y, "TouchAction", "Back");
                if (string.IsNullOrWhiteSpace(action) && string.IsNullOrWhiteSpace(touchAction))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    var gate = ReadActionGate(location, x, y, action, "Buildings.Action");
                    if (gate is not null)
                    {
                        gates.Add(gate);
                    }
                }

                if (!string.IsNullOrWhiteSpace(touchAction))
                {
                    var gate = ReadActionGate(location, x, y, touchAction, "Back.TouchAction");
                    if (gate is not null)
                    {
                        gates.Add(gate);
                    }
                }
            }
        }

        return gates
            .OrderBy(gate => ReadIntProperty(gate, "tile_y") ?? 0)
            .ThenBy(gate => ReadIntProperty(gate, "tile_x") ?? 0)
            .ToArray();
    }

    private static object? ReadActionGate(GameLocation location, int x, int y, string action, string sourceProperty)
    {
        var parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        if (string.Equals(parts[0], "ConditionalDoor", StringComparison.OrdinalIgnoreCase))
        {
            var condition = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;
            return new
            {
                kind = "conditional_door",
                tile_x = x,
                tile_y = y,
                source_property = sourceProperty,
                action,
                condition,
                condition_met = string.IsNullOrWhiteSpace(condition) ? (bool?)null : GameStateQuery.CheckConditions(condition, location),
                locked_message_present = !string.IsNullOrWhiteSpace(location.doesTileHaveProperty(x, y, "LockedDoorMessage", "Buildings")),
                allowed_now = string.IsNullOrWhiteSpace(condition) ? (bool?)null : GameStateQuery.CheckConditions(condition, location),
                unresolved_reason = (string?)null
            };
        }

        if (string.Equals(parts[0], "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
        {
            return ReadLockedDoorWarpGate(location, x, y, action, sourceProperty, parts);
        }

        if (string.Equals(parts[0], "Warp", StringComparison.OrdinalIgnoreCase))
        {
            var touchAction = string.Equals(sourceProperty, "Back.TouchAction", StringComparison.OrdinalIgnoreCase);
            var mailRequired = touchAction ? Part(parts, 4) : null;
            return new
            {
                kind = "warp_action",
                tile_x = x,
                tile_y = y,
                source_property = sourceProperty,
                action,
                target_location = touchAction ? Part(parts, 1) : Part(parts, 3),
                target_x = touchAction ? ParseIntPart(parts, 2) : ParseIntPart(parts, 1),
                target_y = touchAction ? ParseIntPart(parts, 3) : ParseIntPart(parts, 2),
                mail_required = mailRequired,
                mail_requirement_met = string.IsNullOrWhiteSpace(mailRequired) ? (bool?)null : Game1.player?.mailReceived.Contains(mailRequired),
                allowed_now = touchAction && !string.IsNullOrWhiteSpace(mailRequired) ? Game1.player?.mailReceived.Contains(mailRequired) : (parts.Length >= 4 ? (bool?)true : null),
                unresolved_reason = parts.Length >= 4 ? (string?)null : "warp_action_parse_failed"
            };
        }

        if (string.Equals(parts[0], "Door", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "door_action",
                tile_x = x,
                tile_y = y,
                source_property = sourceProperty,
                action,
                allowed_now = (bool?)null,
                unresolved_reason = "door_action_npc_specific_logic_not_resolved"
            };
        }

        return null;
    }

    private static object ReadLockedDoorWarpGate(GameLocation location, int x, int y, string action, string sourceProperty, string[] parts)
    {
        var targetX = ParseIntPart(parts, 1);
        var targetY = ParseIntPart(parts, 2);
        var locationName = Part(parts, 3);
        var openTime = ParseIntPart(parts, 4);
        var closeTime = ParseIntPart(parts, 5);
        var npcName = Part(parts, 6);
        var minFriendship = ParseIntPart(parts, 7) ?? 0;
        var inValleyContext = location.InValleyContext();
        var festivalClosed = GameLocation.AreStoresClosedForFestival() && inValleyContext;
        var hasTownKey = Game1.player?.HasTownKey == true && inValleyContext && !(location.GetType().Name == "BeachNightMarket" && locationName != "FishShop");
        var effectiveOpenTime = locationName == "FishShop" && Game1.player?.mailReceived.Contains("willyHours") == true ? 800 : openTime;
        var seedShopWednesdayClosed = locationName == "SeedShop"
            && string.Equals(Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth), "Wed", StringComparison.OrdinalIgnoreCase)
            && !Utility.HasAnyPlayerSeenEvent("191393")
            && !hasTownKey;
        var friendPoints = ReadFriendshipPoints(npcName);
        var friendshipAllowed = minFriendship <= 0 || location.IsWinterHere() || (friendPoints.HasValue && friendPoints.Value >= minFriendship);
        var timeAllowed = hasTownKey || (effectiveOpenTime.HasValue && closeTime.HasValue && Game1.timeOfDay >= effectiveOpenTime.Value && Game1.timeOfDay < closeTime.Value);
        var greenRainOverride = location.IsGreenRainingHere()
            && Game1.year == 1
            && location.GetType().Name != "Beach"
            && location.GetType().Name != "Forest"
            && !string.Equals(locationName, "AdventureGuild", StringComparison.OrdinalIgnoreCase);
        var parsed = targetX.HasValue && targetY.HasValue && !string.IsNullOrWhiteSpace(locationName) && openTime.HasValue && closeTime.HasValue;
        var allowed = parsed && !festivalClosed && !seedShopWednesdayClosed && ((timeAllowed && friendshipAllowed) || greenRainOverride);

        return new
        {
            kind = "locked_door_warp",
            tile_x = x,
            tile_y = y,
            source_property = sourceProperty,
            action,
            target_location = locationName,
            target_x = targetX,
            target_y = targetY,
            open_time = openTime,
            effective_open_time = effectiveOpenTime,
            close_time = closeTime,
            npc_name = npcName,
            min_friendship = minFriendship,
            friendship_points = friendPoints,
            has_town_key = hasTownKey,
            festival_closed = festivalClosed,
            seed_shop_wednesday_closed = seedShopWednesdayClosed,
            time_allowed = timeAllowed,
            friendship_allowed = friendshipAllowed,
            green_rain_override = greenRainOverride,
            allowed_now = allowed,
            unresolved_reason = parsed ? (string?)null : "locked_door_warp_parse_failed"
        };
    }

    private static int? ReadFriendshipPoints(string? npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName) || Game1.player is null)
        {
            return null;
        }

        return Game1.player.friendshipData.TryGetValue(npcName, out var friendship)
            ? friendship.Points
            : null;
    }

    private static object ReadRouteBlockers()
    {
        var location = Game1.currentLocation;
        var characters = location.characters
            .Select(character => new
            {
                kind = "character",
                name = character.Name,
                type = character.GetType().FullName,
                tile_x = character.TilePoint.X,
                tile_y = character.TilePoint.Y,
                bounding_box = ReadRectangle(character.GetBoundingBox())
            })
            .OrderBy(character => character.name, StringComparer.Ordinal)
            .Cast<object>()
            .ToArray();

        var objects = location.objects.Pairs
            .Select(pair => new
            {
                kind = "object",
                item_id = pair.Value.ItemId,
                qualified_item_id = pair.Value.QualifiedItemId,
                type = pair.Value.GetType().FullName,
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                passable = pair.Value.isPassable(),
                bounding_box = ReadRectangle(pair.Value.GetBoundingBoxAt((int)pair.Key.X, (int)pair.Key.Y))
            })
            .OrderBy(item => item.tile_y)
            .ThenBy(item => item.tile_x)
            .Cast<object>()
            .ToArray();

        var terrainFeatures = location.terrainFeatures.Pairs
            .Select(pair => new
            {
                kind = "terrain_feature",
                type = pair.Value.GetType().FullName,
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                bounding_box = ReadRectangle(pair.Value.getBoundingBox())
            })
            .OrderBy(feature => feature.tile_y)
            .ThenBy(feature => feature.tile_x)
            .Cast<object>()
            .ToArray();

        var largeTerrainFeatures = location.largeTerrainFeatures
            .Select(feature => new
            {
                kind = "large_terrain_feature",
                type = feature.GetType().FullName,
                bounding_box = ReadRectangle(feature.getBoundingBox())
            })
            .Cast<object>()
            .ToArray();

        var resourceClumps = location.resourceClumps
            .Select(clump => new
            {
                kind = "resource_clump",
                type = clump.GetType().FullName,
                bounding_box = ReadRectangle(clump.getBoundingBox())
            })
            .Cast<object>()
            .ToArray();

        var furniture = location.furniture
            .Select(item => new
            {
                kind = "furniture",
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                type = item.GetType().FullName,
                tile_x = (int)item.TileLocation.X,
                tile_y = (int)item.TileLocation.Y,
                bounding_box = ReadRectangle(item.boundingBox.Value)
            })
            .OrderBy(item => item.tile_y)
            .ThenBy(item => item.tile_x)
            .Cast<object>()
            .ToArray();

        return new
        {
            location_id = location.NameOrUniqueName,
            character_count = characters.Length,
            object_count = objects.Length,
            terrain_feature_count = terrainFeatures.Length,
            large_terrain_feature_count = largeTerrainFeatures.Length,
            resource_clump_count = resourceClumps.Length,
            furniture_count = furniture.Length,
            collision_api_source = "GameLocation.isCollidingPosition",
            characters,
            objects,
            terrain_features = terrainFeatures,
            large_terrain_features = largeTerrainFeatures,
            resource_clumps = resourceClumps,
            furniture
        };
    }

    private static object ReadRectangle(Rectangle rectangle)
    {
        return new
        {
            x = rectangle.X,
            y = rectangle.Y,
            width = rectangle.Width,
            height = rectangle.Height
        };
    }

}
