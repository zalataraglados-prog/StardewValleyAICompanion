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
    private static object ReadRouteActionBranchCoverage()
    {
        var location = Game1.currentLocation;
        var actions = ReadMapActions(location);
        var rows = actions
            .Select(action => new
            {
                action.tile_x,
                action.tile_y,
                action.source_property,
                action.raw_action,
                branch = action.branch,
                route_transparency_status = ClassifyRouteActionBranch(action.branch),
                route_training_blocked = ClassifyRouteActionBranch(action.branch) != "covered_for_read",
                note = RouteActionBranchNote(action.branch)
            })
            .OrderBy(row => row.route_transparency_status, StringComparer.Ordinal)
            .ThenBy(row => row.tile_y)
            .ThenBy(row => row.tile_x)
            .ToArray();

        return new
        {
            location_id = location.NameOrUniqueName,
            action_count = rows.Length,
            covered_for_read_count = rows.Count(row => row.route_transparency_status == "covered_for_read"),
            unsupported_for_route_training_count = rows.Count(row => row.route_transparency_status != "covered_for_read"),
            route_execution_enabled = false,
            rows
        };
    }

    private static object ReadRouteGraph()
    {
        var locations = Game1.locations
            .Where(location => location is not null)
            .OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .ToArray();
        var locationNames = locations
            .Select(location => location.NameOrUniqueName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = new List<object>();
        var unsupportedActionBranches = 0;

        foreach (var location in locations)
        {
            foreach (var warp in location.warps)
            {
                edges.Add(new
                {
                    kind = "warp",
                    from_location = location.NameOrUniqueName,
                    from_x = warp.X,
                    from_y = warp.Y,
                    target_location = warp.TargetName,
                    target_x = (int?)warp.TargetX,
                    target_y = (int?)warp.TargetY,
                    source_property = "GameLocation.warps",
                    raw_action = (string?)null,
                    resolved = !string.IsNullOrWhiteSpace(warp.TargetName) && locationNames.Contains(warp.TargetName),
                    unresolved_reason = !string.IsNullOrWhiteSpace(warp.TargetName) && locationNames.Contains(warp.TargetName) ? (string?)null : "target_location_not_loaded"
                });
            }

            foreach (var pair in location.doors.Pairs)
            {
                var target = pair.Value;
                edges.Add(new
                {
                    kind = "door",
                    from_location = location.NameOrUniqueName,
                    from_x = pair.Key.X,
                    from_y = pair.Key.Y,
                    target_location = target,
                    target_x = (int?)null,
                    target_y = (int?)null,
                    source_property = "GameLocation.doors",
                    raw_action = location.doesTileHaveProperty(pair.Key.X, pair.Key.Y, "Action", "Buildings"),
                    resolved = false,
                    unresolved_reason = "door_target_tile_not_resolved"
                });
            }

            foreach (var building in location.buildings)
            {
                var doorEdge = ReadBuildingDoorGraphEdge(location, building, locationNames);
                edges.Add(doorEdge);
            }

            foreach (var action in ReadMapActions(location))
            {
                var edge = ReadRouteGraphActionEdge(location, action, locationNames);
                if (edge is not null)
                {
                    edges.Add(edge);
                }
                else if (ClassifyRouteActionBranch(action.branch) != "covered_for_read")
                {
                    unsupportedActionBranches++;
                }
            }
        }

        return new
        {
            location_count = locations.Length,
            edge_count = edges.Count,
            resolved_edge_count = edges.Count(edge => ReadBoolProperty(edge, "resolved") == true),
            unresolved_edge_count = edges.Count(edge => ReadBoolProperty(edge, "resolved") != true),
            unsupported_action_branch_count = unsupportedActionBranches,
            route_executor_enabled = true,
            route_executor_scope = "resolved_warp_touch_action_warp_action_warp_locked_door_warp_building_door_only",
            route_replan_policy = "fresh_snapshot_after_each_location_transition",
            graph_scope = "loaded_locations_only",
            edges = edges
                .OrderBy(edge => ReadStringProperty(edge, "from_location"), StringComparer.Ordinal)
                .ThenBy(edge => ReadIntProperty(edge, "from_y") ?? 0)
                .ThenBy(edge => ReadIntProperty(edge, "from_x") ?? 0)
                .ToArray()
        };
    }

    private static object ReadRouteMapSummaries()
    {
        var locations = Game1.locations
            .Where(location => location is not null)
            .OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .Select(ReadRouteMapSummary)
            .ToArray();

        return new
        {
            location_count = locations.Length,
            loaded_locations_only = true,
            full_collision_grids_included = false,
            locations
        };
    }

    private static object ReadRouteMapSummary(GameLocation location)
    {
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length > 0 ? layers.Max(layer => layer.LayerWidth) : 0;
        var height = layers.Length > 0 ? layers.Max(layer => layer.LayerHeight) : 0;
        var actions = ReadMapActions(location);
        var unsupportedActions = actions.Count(action => ClassifyRouteActionBranch(action.branch) != "covered_for_read");
        var actionWarps = actions.Count(action => string.Equals(action.branch, "Warp", StringComparison.OrdinalIgnoreCase));
        var lockedDoorWarps = actions.Count(action => string.Equals(action.branch, "LockedDoorWarp", StringComparison.OrdinalIgnoreCase));
        var shopEndpoints = actions.Count(action => IsShopEndpointAction(action.branch));

        return new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            type = location.GetType().FullName,
            map_id = location.map?.Id,
            width,
            height,
            layer_count = layers.Length,
            warp_count = location.warps.Count,
            door_count = location.doors.Count(),
            interior_door_count = location.interiorDoors.Doors.Count(),
            action_count = actions.Length,
            action_warp_count = actionWarps,
            locked_door_warp_count = lockedDoorWarps,
            shop_endpoint_count = shopEndpoints,
            unsupported_action_branch_count = unsupportedActions,
            has_unsupported_action_branches = unsupportedActions > 0,
            collision_grid_available = string.Equals(location.NameOrUniqueName, Game1.currentLocation?.NameOrUniqueName, StringComparison.Ordinal),
            segment_validation_status = string.Equals(location.NameOrUniqueName, Game1.currentLocation?.NameOrUniqueName, StringComparison.Ordinal)
                ? "current_location_collision_grid_available"
                : "pending_per_location_collision_grid"
        };
    }

    private static object? ReadRouteGraphActionEdge(GameLocation location, MapActionRow action, HashSet<string> locationNames)
    {
        var parts = action.raw_action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        if (string.Equals(parts[0], "Warp", StringComparison.OrdinalIgnoreCase))
        {
            var touchAction = string.Equals(action.source_property, "Back.TouchAction", StringComparison.OrdinalIgnoreCase);
            var targetLocation = touchAction ? Part(parts, 1) : Part(parts, 3);
            var targetX = touchAction ? ParseIntPart(parts, 2) : ParseIntPart(parts, 1);
            var targetY = touchAction ? ParseIntPart(parts, 3) : ParseIntPart(parts, 2);
            var resolved = !string.IsNullOrWhiteSpace(targetLocation) && targetX.HasValue && targetY.HasValue && locationNames.Contains(targetLocation);
            return new
            {
                kind = touchAction ? "touch_action_warp" : "action_warp",
                from_location = location.NameOrUniqueName,
                from_x = action.tile_x,
                from_y = action.tile_y,
                target_location = targetLocation,
                target_x = targetX,
                target_y = targetY,
                source_property = action.source_property,
                raw_action = action.raw_action,
                resolved,
                unresolved_reason = resolved ? (string?)null : "action_warp_target_not_resolved"
            };
        }

        if (string.Equals(parts[0], "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
        {
            var targetLocation = Part(parts, 3);
            var targetX = ParseIntPart(parts, 1);
            var targetY = ParseIntPart(parts, 2);
            var resolved = !string.IsNullOrWhiteSpace(targetLocation) && targetX.HasValue && targetY.HasValue && locationNames.Contains(targetLocation);
            var gate = ReadLockedDoorWarpGate(
                location,
                action.tile_x,
                action.tile_y,
                action.raw_action,
                action.source_property,
                parts);
            return new
            {
                kind = "locked_door_warp",
                from_location = location.NameOrUniqueName,
                from_x = action.tile_x,
                from_y = action.tile_y,
                target_location = targetLocation,
                target_x = targetX,
                target_y = targetY,
                source_property = action.source_property,
                raw_action = action.raw_action,
                open_time = ParseIntPart(parts, 4),
                close_time = ParseIntPart(parts, 5),
                npc_name = Part(parts, 6),
                min_friendship = ParseIntPart(parts, 7),
                gate,
                resolved,
                unresolved_reason = resolved ? (string?)null : "locked_door_warp_target_not_resolved"
            };
        }

        if (IsShopEndpointAction(parts[0]))
        {
            var parsed = CurrentLocationReadAdapter.ParseShopAction(
                location,
                action.raw_action,
                action.tile_x,
                action.tile_y);
            var ownerServiceStatus = parsed is null
                ? null
                : CurrentLocationReadAdapter.ReadOwnerServiceStatus(location, parsed);
            var ownerRequired = ownerServiceStatus is not null &&
                ReadBoolProperty(ownerServiceStatus, "owner_required") == true;
            var ownerAtServiceCounter = !ownerRequired ||
                ReadBoolProperty(ownerServiceStatus!, "in_service_area") == true;
            var openTime = string.Equals(parts[0], "OpenShop", StringComparison.OrdinalIgnoreCase)
                ? ParseIntPart(parts, 3)
                : null;
            var closeTime = string.Equals(parts[0], "OpenShop", StringComparison.OrdinalIgnoreCase)
                ? ParseIntPart(parts, 4)
                : null;
            var directTimeGateKnown = openTime.HasValue && closeTime.HasValue;
            var directTimeAllowed = !directTimeGateKnown ||
                Game1.timeOfDay >= openTime!.Value &&
                Game1.timeOfDay < closeTime!.Value;
            var festivalClosed = location.InValleyContext() &&
                GameLocation.AreStoresClosedForFestival();
            return new
            {
                kind = "shop_endpoint",
                from_location = location.NameOrUniqueName,
                from_x = action.tile_x,
                from_y = action.tile_y,
                target_location = (string?)null,
                target_x = (int?)null,
                target_y = (int?)null,
                source_property = action.source_property,
                raw_action = action.raw_action,
                action_type = parts[0],
                shop_id = ResolveShopEndpointId(location, parts),
                open_time = openTime,
                close_time = closeTime,
                direct_time_gate_known = directTimeGateKnown,
                direct_time_allowed = directTimeAllowed,
                parsed,
                owner_service_status = ownerServiceStatus,
                festival_closed = festivalClosed,
                allowed_now = directTimeAllowed && ownerAtServiceCounter && !festivalClosed,
                resolved = false,
                unresolved_reason = "shop_endpoint_not_location_transition"
            };
        }

        return null;
    }

    private static object ReadBuildingDoorGraphEdge(GameLocation location, Building building, HashSet<string> locationNames)
    {
        var humanDoor = building.humanDoor;
        if (humanDoor.X < 0 || humanDoor.Y < 0)
        {
            return new
            {
                kind = "building_door",
                from_location = location.NameOrUniqueName,
                from_x = (int?)null,
                from_y = (int?)null,
                target_location = (string?)null,
                target_x = (int?)null,
                target_y = (int?)null,
                source_property = "Building.humanDoor; Building.GetIndoors()",
                raw_action = (string?)null,
                building_type = building.buildingType.Value,
                building_tile_x = building.tileX.Value,
                building_tile_y = building.tileY.Value,
                resolved = false,
                unresolved_reason = "human_door_unavailable"
            };
        }

        var indoors = building.GetIndoors();
        if (indoors is null)
        {
            return new
            {
                kind = "building_door",
                from_location = location.NameOrUniqueName,
                from_x = building.tileX.Value + humanDoor.X,
                from_y = building.tileY.Value + humanDoor.Y,
                target_location = (string?)null,
                target_x = (int?)null,
                target_y = (int?)null,
                source_property = "Building.humanDoor; Building.GetIndoors()",
                raw_action = (string?)null,
                building_type = building.buildingType.Value,
                building_tile_x = building.tileX.Value,
                building_tile_y = building.tileY.Value,
                resolved = false,
                unresolved_reason = "indoor_location_unavailable"
            };
        }

        if (indoors.warps.Count == 0)
        {
            return new
            {
                kind = "building_door",
                from_location = location.NameOrUniqueName,
                from_x = building.tileX.Value + humanDoor.X,
                from_y = building.tileY.Value + humanDoor.Y,
                target_location = indoors.NameOrUniqueName,
                target_x = (int?)null,
                target_y = (int?)null,
                source_property = "Building.humanDoor; Building.GetIndoors()",
                raw_action = (string?)null,
                building_type = building.buildingType.Value,
                building_tile_x = building.tileX.Value,
                building_tile_y = building.tileY.Value,
                resolved = false,
                unresolved_reason = "indoor_entry_warp_unavailable"
            };
        }

        var actionTileX = building.tileX.Value + humanDoor.X;
        var actionTileY = building.tileY.Value + humanDoor.Y;
        var targetLocation = indoors.NameOrUniqueName;
        var arrivalTileX = (int?)indoors.warps[0].X;
        var arrivalTileY = (int?)indoors.warps[0].Y - 1;
        var targetLoaded = !string.IsNullOrWhiteSpace(targetLocation) && locationNames.Contains(targetLocation);
        var underConstruction = building.daysOfConstructionLeft.Value > 0;
        var resolved = targetLoaded && !underConstruction;

        return new
        {
            kind = "building_door",
            from_location = location.NameOrUniqueName,
            from_x = actionTileX,
            from_y = actionTileY,
            target_location = targetLocation,
            target_x = arrivalTileX,
            target_y = arrivalTileY,
            source_property = "Building.humanDoor; Building.GetIndoors()",
            raw_action = (string?)null,
            building_type = building.buildingType.Value,
            building_tile_x = building.tileX.Value,
            building_tile_y = building.tileY.Value,
            resolved,
            unresolved_reason = resolved ? (string?)null :
                (!targetLoaded ? "target_location_not_loaded" : "building_under_construction")
        };
    }

    private static string ClassifyRouteActionBranch(string? branch)
    {
        return branch switch
        {
            "Warp" => "covered_for_read",
            "LockedDoorWarp" => "covered_for_read",
            "ConditionalDoor" => "covered_for_read",
            "Door" => "covered_for_read",
            "OpenShop" => "covered_for_read",
            "Buy" => "covered_for_read",
            "JojaShop" => "covered_for_read",
            "Blacksmith" => "covered_for_read",
            "Carpenter" => "covered_for_read",
            "Marnie" => "covered_for_read",
            "AnimalShop" => "covered_for_read",
            "AdventureGuild" => "covered_for_read",
            "adventureGuild" => "covered_for_read",
            "AdventureShop" => "covered_for_read",
            null or "" => "unsupported_for_route_training",
            _ => "unsupported_for_route_training"
        };
    }

    private static string RouteActionBranchNote(string? branch)
    {
        return branch switch
        {
            "Warp" => "read-side target/mail gate preview exists where Stardew action format exposes it",
            "LockedDoorWarp" => "read-side time/festival/key/friendship gate preview exists",
            "ConditionalDoor" => "read-side GameStateQuery gate preview exists",
            "Door" => "door branch is recognized but NPC-specific hardcoded details may still block execution",
            "OpenShop" => "shop endpoint recognized; shop-opening executor may call this branch; safe purchase executor is stock-gated",
            "Buy" => "legacy shop endpoint recognized; shop-opening executor may call this branch; safe purchase executor is stock-gated",
            "JojaShop" => "Joja shop endpoint recognized from GameLocation.performAction; shop-opening executor may call this branch",
            "Blacksmith" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
            "Carpenter" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
            "Marnie" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
            "AnimalShop" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
            "AdventureGuild" or "adventureGuild" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
            "AdventureShop" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
            _ => "branch not route-transparent; route/shop-opening training must block on this action"
        };
    }

    private static bool IsShopEndpointAction(string? branch)
    {
        return string.Equals(branch, "OpenShop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "Buy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "JojaShop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "Blacksmith", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "Carpenter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "Marnie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "AnimalShop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "AdventureGuild", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "adventureGuild", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "AdventureShop", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MapActionRow(int tile_x, int tile_y, string source_property, string raw_action, string? branch);

    private static MapActionRow[] ReadMapActions(GameLocation location)
    {
        var map = location.map;
        if (map?.Layers is null || map.Layers.Count == 0)
        {
            return Array.Empty<MapActionRow>();
        }

        var width = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerWidth);
        var height = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerHeight);
        var rows = new List<MapActionRow>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                AddMapAction(rows, x, y, "Buildings.Action", location.doesTileHaveProperty(x, y, "Action", "Buildings"));
                AddMapAction(rows, x, y, "Back.TouchAction", location.doesTileHaveProperty(x, y, "TouchAction", "Back"));
            }
        }

        return rows.ToArray();
    }

    private static void AddMapAction(List<MapActionRow> rows, int x, int y, string sourceProperty, string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        var branch = action.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        rows.Add(new MapActionRow(x, y, sourceProperty, action, branch));
    }

}
