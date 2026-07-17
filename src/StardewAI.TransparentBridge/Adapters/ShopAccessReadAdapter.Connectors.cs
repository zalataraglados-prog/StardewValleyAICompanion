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
    private static object ReadRouteConnectors()
    {
        var location = Game1.currentLocation;
        var connectors = new List<object>();

        connectors.AddRange(location.warps.Select(warp => new
        {
            kind = "warp",
            tile_x = warp.X,
            tile_y = warp.Y,
            target_location = warp.TargetName,
            target_x = (int?)warp.TargetX,
            target_y = (int?)warp.TargetY,
            action = (string?)null,
            open = (bool?)null,
            resolved = true,
            unresolved_reason = (string?)null
        }));

        connectors.AddRange(location.doors.Pairs.Select(pair => new
        {
            kind = "door",
            tile_x = pair.Key.X,
            tile_y = pair.Key.Y,
            target_location = pair.Value,
            target_x = (int?)null,
            target_y = (int?)null,
            action = location.doesTileHaveProperty(pair.Key.X, pair.Key.Y, "Action", "Buildings"),
            open = (bool?)null,
            resolved = false,
            unresolved_reason = "door_target_tile_not_resolved"
        }));

        connectors.AddRange(location.interiorDoors.Doors.Select(door => new
        {
            kind = "interior_door",
            tile_x = door.Position.X,
            tile_y = door.Position.Y,
            target_location = (string?)null,
            target_x = (int?)null,
            target_y = (int?)null,
            action = location.doesTileHaveProperty(door.Position.X, door.Position.Y, "Action", "Buildings"),
            open = (bool?)door.Value,
            resolved = false,
            unresolved_reason = "interior_door_room_transition_not_resolved"
        }));

        foreach (var connector in ReadActionConnectors(location))
        {
            connectors.Add(connector);
        }

        connectors.AddRange(ReadBuildingDoorConnectors(location));

        return new
        {
            location_id = location.NameOrUniqueName,
            connector_count = connectors.Count,
            resolved_connector_count = connectors.Count(connector => ReadBoolProperty(connector, "resolved") == true),
            unresolved_connector_count = connectors.Count(connector => ReadBoolProperty(connector, "resolved") != true),
            route_planner_enabled = true,
            route_planner_scope = "resolved_supported_connectors_only",
            connectors = connectors
                .OrderBy(connector => ReadStringProperty(connector, "kind"), StringComparer.Ordinal)
                .ThenBy(connector => ReadIntProperty(connector, "tile_y") ?? 0)
                .ThenBy(connector => ReadIntProperty(connector, "tile_x") ?? 0)
                .ToArray()
        };
    }

    private static object[] ReadActionConnectors(GameLocation location)
    {
        var connectors = new List<object>();
        foreach (var actionRow in ReadMapActions(location))
        {
            var parts = actionRow.raw_action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            if (string.Equals(parts[0], "Warp", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
            {
                var touchAction = string.Equals(actionRow.source_property, "Back.TouchAction", StringComparison.OrdinalIgnoreCase);
                var targetLocation = touchAction ? Part(parts, 1) : Part(parts, 3);
                var targetX = touchAction ? ParseIntPart(parts, 2) : ParseIntPart(parts, 1);
                var targetY = touchAction ? ParseIntPart(parts, 3) : ParseIntPart(parts, 2);
                var resolved = !string.IsNullOrWhiteSpace(targetLocation) && targetX.HasValue && targetY.HasValue;
                connectors.Add(new
                {
                    kind = touchAction ? "touch_action_warp" : "action_warp",
                    tile_x = actionRow.tile_x,
                    tile_y = actionRow.tile_y,
                    target_location = targetLocation,
                    target_x = targetX,
                    target_y = targetY,
                    action = actionRow.raw_action,
                    source_property = actionRow.source_property,
                    open = (bool?)null,
                    resolved,
                    unresolved_reason = resolved ? (string?)null : "warp_action_parse_failed"
                });
            }
            else if (string.Equals(parts[0], "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
            {
                var targetLocation = Part(parts, 3);
                var targetX = ParseIntPart(parts, 1);
                var targetY = ParseIntPart(parts, 2);
                var resolved = !string.IsNullOrWhiteSpace(targetLocation) && targetX.HasValue && targetY.HasValue;
                connectors.Add(new
                {
                    kind = "locked_door_warp",
                    tile_x = actionRow.tile_x,
                    tile_y = actionRow.tile_y,
                    target_location = targetLocation,
                    target_x = targetX,
                    target_y = targetY,
                    action = actionRow.raw_action,
                    source_property = actionRow.source_property,
                    open = (bool?)null,
                    resolved,
                    unresolved_reason = resolved ? (string?)null : "locked_door_warp_parse_failed"
                });
            }
            else if (IsShopEndpointAction(parts[0]))
            {
                connectors.Add(new
                {
                    kind = "shop_action",
                    tile_x = actionRow.tile_x,
                    tile_y = actionRow.tile_y,
                    target_location = ResolveShopEndpointId(location, parts),
                    target_x = (int?)null,
                    target_y = (int?)null,
                    action = actionRow.raw_action,
                    source_property = actionRow.source_property,
                    open = (bool?)null,
                    resolved = false,
                    unresolved_reason = "shop_action_is_endpoint_not_location_transition"
                });
            }
        }

        return connectors.ToArray();
    }

    private static object[] ReadBuildingDoorConnectors(GameLocation location)
    {
        var connectors = new List<object>();
        foreach (var building in location.buildings)
        {
            var doorConnector = ReadSingleBuildingDoorConnector(location, building);
            connectors.Add(doorConnector);
        }

        return connectors.ToArray();
    }

    private static object ReadSingleBuildingDoorConnector(GameLocation location, Building building)
    {
        var humanDoor = building.humanDoor;
        if (humanDoor.X < 0 || humanDoor.Y < 0)
        {
            return new
            {
                kind = "building_door",
                tile_x = (int?)null,
                tile_y = (int?)null,
                target_location = (string?)null,
                target_x = (int?)null,
                target_y = (int?)null,
                action = (string?)null,
                open = (bool?)null,
                resolved = false,
                unresolved_reason = "human_door_unavailable",
                building_type = building.buildingType.Value,
                building_tile_x = building.tileX.Value,
                building_tile_y = building.tileY.Value,
                stand_tile_x = (int?)null,
                stand_tile_y = (int?)null,
                source = "Building.humanDoor unavailable; Building.GetIndoors()"
            };
        }

        var indoors = building.GetIndoors();
        if (indoors is null)
        {
            return new
            {
                kind = "building_door",
                tile_x = building.tileX.Value + humanDoor.X,
                tile_y = building.tileY.Value + humanDoor.Y,
                target_location = (string?)null,
                target_x = (int?)null,
                target_y = (int?)null,
                action = (string?)null,
                open = (bool?)null,
                resolved = false,
                unresolved_reason = "indoor_location_unavailable",
                building_type = building.buildingType.Value,
                building_tile_x = building.tileX.Value,
                building_tile_y = building.tileY.Value,
                stand_tile_x = building.tileX.Value + humanDoor.X,
                stand_tile_y = building.tileY.Value + humanDoor.Y + 1,
                source = "Building.humanDoor; Building.GetIndoors() returned null"
            };
        }

        if (indoors.warps.Count == 0)
        {
            return new
            {
                kind = "building_door",
                tile_x = building.tileX.Value + humanDoor.X,
                tile_y = building.tileY.Value + humanDoor.Y,
                target_location = indoors.NameOrUniqueName,
                target_x = (int?)null,
                target_y = (int?)null,
                action = (string?)null,
                open = (bool?)null,
                resolved = false,
                unresolved_reason = "indoor_entry_warp_unavailable",
                building_type = building.buildingType.Value,
                building_tile_x = building.tileX.Value,
                building_tile_y = building.tileY.Value,
                stand_tile_x = building.tileX.Value + humanDoor.X,
                stand_tile_y = building.tileY.Value + humanDoor.Y + 1,
                source = "Building.humanDoor; Building.GetIndoors().warps is empty"
            };
        }

        var actionTileX = building.tileX.Value + humanDoor.X;
        var actionTileY = building.tileY.Value + humanDoor.Y;
        var standTileX = actionTileX;
        var standTileY = actionTileY + 1;

        var arrivalTileX = (int?)indoors.warps[0].X;
        var arrivalTileY = (int?)indoors.warps[0].Y - 1;

        var constructionLeft = building.daysOfConstructionLeft.Value;
        var resolved = constructionLeft <= 0;

        return new
        {
            kind = "building_door",
            tile_x = actionTileX,
            tile_y = actionTileY,
            target_location = indoors.NameOrUniqueName,
            target_x = arrivalTileX,
            target_y = arrivalTileY,
            action = (string?)null,
            open = (bool?)null,
            resolved,
            unresolved_reason = resolved ? (string?)null : "building_under_construction",
            building_type = building.buildingType.Value,
            building_tile_x = building.tileX.Value,
            building_tile_y = building.tileY.Value,
            stand_tile_x = standTileX,
            stand_tile_y = standTileY,
            source = "Building.humanDoor; Building.GetIndoors(); Building.GetIndoors().warps[0]"
        };
    }

}
