using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ShopAccessReadAdapter : ReadAdapterBase
{
    public override string Domain => "locations";
    public override int Priority => 34;

    public override StateAdapterResult Collect(long tick)
    {
        if (!Context.IsWorldReady)
        {
            return Section("locations", new Dictionary<string, object>
            {
                ["shops"] = Unavailable("world_not_ready", "DataLoader.Shops(Game1.content)", tick, "vanilla_1_6_shops"),
                ["collision_grid"] = Unavailable("world_not_ready", "Game1.currentLocation.isCollidingPosition", tick, "vanilla_1_6_route"),
                ["route_connectors"] = Unavailable("world_not_ready", "Game1.currentLocation.warps/doors/interiorDoors/map Action", tick, "vanilla_1_6_route"),
                ["route_blockers"] = Unavailable("world_not_ready", "Game1.currentLocation collision participants", tick, "vanilla_1_6_route"),
                ["route_gate_context"] = Unavailable("world_not_ready", "GameLocation LockedDoorWarp/ConditionalDoor/Warp action gates", tick, "vanilla_1_6_route"),
                ["route_action_branch_coverage"] = Unavailable("world_not_ready", "GameLocation performAction branch coverage audit", tick, "vanilla_1_6_route"),
                ["route_graph"] = Unavailable("world_not_ready", "Game1.locations route graph preview", tick, "vanilla_1_6_route"),
                ["route_map_summaries"] = Unavailable("world_not_ready", "Game1.locations route map summaries", tick, "vanilla_1_6_route")
            }, new[] { "locations.shops", "locations.collision_grid", "locations.route_connectors", "locations.route_blockers", "locations.route_gate_context", "locations.route_action_branch_coverage", "locations.route_graph", "locations.route_map_summaries" }, "unavailable");
        }

        return Section("locations", new Dictionary<string, object>
        {
            ["shops"] = Field(ReadShopAccess(), "DataLoader.Shops(Game1.content); ShopBuilder.GetCurrentOwners; Utility.isFestivalDay; GameLocation.AreStoresClosedForFestival", tick, "vanilla_1_6_shops"),
            ["collision_grid"] = Field(ReadCollisionGrid(), "Game1.currentLocation.isCollidingPosition compressed current map grid", tick, "vanilla_1_6_route"),
            ["route_connectors"] = Field(ReadRouteConnectors(), "Game1.currentLocation.warps/doors/interiorDoors and map Action connector index", tick, "vanilla_1_6_route"),
            ["route_blockers"] = Field(ReadRouteBlockers(), "Game1.currentLocation characters/objects/terrain/resource clumps/furniture collision participants", tick, "vanilla_1_6_route"),
            ["route_gate_context"] = Field(ReadRouteGateContext(), "GameLocation LockedDoorWarp/ConditionalDoor/Warp action gates and map BuildConditions", tick, "vanilla_1_6_route"),
            ["route_action_branch_coverage"] = Field(ReadRouteActionBranchCoverage(), "GameLocation performAction branch coverage audit for current map actions", tick, "vanilla_1_6_route"),
            ["route_graph"] = Field(ReadRouteGraph(), "Game1.locations warps/doors/action-warp route graph preview", tick, "vanilla_1_6_route"),
            ["route_map_summaries"] = Field(ReadRouteMapSummaries(), "Game1.locations map dimensions and route connector/action summaries", tick, "vanilla_1_6_route")
        }, Array.Empty<string>(), "partial");
    }

    private static object ReadShopAccess()
    {
        var location = Game1.currentLocation;
        var currentCharacters = location.characters
            .Select(character => new
            {
                name = character.Name,
                tile_x = character.TilePoint.X,
                tile_y = character.TilePoint.Y,
                is_villager = character.IsVillager
            })
            .OrderBy(character => character.name, StringComparer.Ordinal)
            .ToArray();

        var shops = DataLoader.Shops(Game1.content)
            .Select(pair => ReadShopSummary(pair.Key, pair.Value))
            .OrderBy(shop => shop.shop_id, StringComparer.Ordinal)
            .ToArray();

        return new
        {
            current_location_id = location.NameOrUniqueName,
            current_time = Game1.timeOfDay,
            current_day = Game1.dayOfMonth,
            current_season = Game1.currentSeason,
            festival_day = Utility.isFestivalDay(),
            stores_closed_for_festival = GameLocation.AreStoresClosedForFestival(),
            current_location_character_count = currentCharacters.Length,
            current_location_characters = currentCharacters,
            shop_count = shops.Length,
            shops
        };
    }

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
                resolved,
                unresolved_reason = resolved ? (string?)null : "locked_door_warp_target_not_resolved"
            };
        }

        if (IsShopEndpointAction(parts[0]))
        {
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
                shop_id = ResolveShopEndpointId(location, parts),
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
            "AdventureGuild" => "covered_for_read",
            "adventureGuild" => "covered_for_read",
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
            "AdventureGuild" or "adventureGuild" => "dialogue shop endpoint recognized; shop-opening executor may call branch then whitelisted dialogue response",
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
            || string.Equals(branch, "AdventureGuild", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "adventureGuild", StringComparison.OrdinalIgnoreCase);
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

    private static object ReadCollisionGrid()
    {
        var location = Game1.currentLocation;
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length > 0 ? layers.Max(layer => layer.LayerWidth) : 0;
        var height = layers.Length > 0 ? layers.Max(layer => layer.LayerHeight) : 0;
        var notableTiles = new List<object>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                var touchAction = location.doesTileHaveProperty(x, y, "TouchAction", "Back");
                var friendshipDoor = ReadFriendshipDoorGate(location, touchAction);
                var warp = location.warps.FirstOrDefault(candidate => candidate.X == x && candidate.Y == y);
                var point = new Point(x, y);
                var hasDoor = location.doors.ContainsKey(point);
                var hasInteriorDoor = location.interiorDoors.ContainsKey(point);
                var collision = location.isCollidingPosition(
                    new Rectangle(x * 64 + 1, y * 64 + 1, 62, 62),
                    Game1.viewport,
                    isFarmer: true,
                    damagesFarmer: 0,
                    glider: false,
                    Game1.player,
                    pathfinding: true);

                var collisionBlocked = collision || friendshipDoor is { AllowedNow: false };
                if (!collisionBlocked && string.IsNullOrWhiteSpace(action) && string.IsNullOrWhiteSpace(touchAction) && warp is null && !hasDoor && !hasInteriorDoor)
                {
                    continue;
                }

                notableTiles.Add(new
                {
                    tile_x = x,
                    tile_y = y,
                    collision_blocked = collisionBlocked,
                    native_collision_blocked = collision,
                    action,
                    touch_action = touchAction,
                    warp_target = warp?.TargetName,
                    door = hasDoor,
                    interior_door = hasInteriorDoor,
                    friendship_door = friendshipDoor is not null,
                    friendship_door_allowed_now = friendshipDoor?.AllowedNow,
                    friendship_door_required_hearts = friendshipDoor?.RequiredHearts,
                    friendship_door_npc_names = friendshipDoor?.NpcNames,
                    friendship_door_green_rain_override = friendshipDoor?.GreenRainOverride,
                    friendship_door_gate_source = friendshipDoor?.Source
                });
            }
        }

        return new
        {
            location_id = location.NameOrUniqueName,
            width,
            height,
            compression = "blocked_or_action_or_warp_or_door_tiles_only",
            probe_rect_offset_x = 1,
            probe_rect_offset_y = 1,
            probe_rect_width = 62,
            probe_rect_height = 62,
            notable_tile_count = notableTiles.Count,
            notable_tiles = notableTiles
        };
    }

    private static FriendshipDoorGate? ReadFriendshipDoorGate(GameLocation location, string? touchAction)
    {
        var parts = (touchAction ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "Door", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var npcNames = parts.Skip(1).ToArray();
        var greenRainOverride = Game1.year == 1 &&
            location.IsGreenRainingHere() &&
            npcNames.Any(name => string.Equals(name, "Sebastian", StringComparison.Ordinal));
        var friendshipAllowed = npcNames.Length == 0 ||
            npcNames.Any(name => Game1.player.getFriendshipHeartLevelForNPC(name) >= 2);

        return new FriendshipDoorGate(
            AllowedNow: friendshipAllowed || greenRainOverride,
            RequiredHearts: 2,
            NpcNames: npcNames,
            GreenRainOverride: greenRainOverride,
            Source: "GameLocation.performTouchAction Door branch; Farmer.getFriendshipHeartLevelForNPC; year-one Green Rain Sebastian override");
    }

    private sealed record ShopAccessSummary(
        string shop_id,
        int owner_rule_count,
        int current_owner_rule_count,
        bool has_current_owner_rule,
        object[] current_owner_rules,
        int item_rule_count,
        bool condition_present,
        string? currency,
        object stock_preview);

    private sealed record FriendshipDoorGate(
        bool AllowedNow,
        int RequiredHearts,
        string[] NpcNames,
        bool GreenRainOverride,
        string Source);

    private static ShopAccessSummary ReadShopSummary(string shopId, ShopData shopData)
    {
        var ownerEntries = ReadEnumerableProperty(shopData, "Owners");
        var currentOwners = ShopBuilder.GetCurrentOwners((StardewValley.GameData.Shops.ShopData)shopData)
            .Select(owner => new
            {
                name = ReadStringProperty(owner, "Name"),
                type = ReadProperty(owner, "Type")?.ToString(),
                has_closed_message = !string.IsNullOrWhiteSpace(ReadStringProperty(owner, "ClosedMessage")),
                condition_present = !string.IsNullOrWhiteSpace(ReadStringProperty(owner, "Condition"))
            })
            .Cast<object>()
            .ToArray();

        return new ShopAccessSummary(
            shopId,
            ownerEntries.Length,
            currentOwners.Length,
            currentOwners.Length > 0,
            currentOwners,
            ReadEnumerableProperty(shopData, "Items").Length,
            !string.IsNullOrWhiteSpace(ReadStringProperty(shopData, "Condition")),
            ReadProperty(shopData, "Currency")?.ToString(),
            ReadShopStockPreview(shopId, shopData));
    }

    private static object ReadShopStockPreview(string shopId, ShopData shopData)
    {
        var stock = ShopBuilder.GetShopStock(shopId, shopData)
            .OrderBy(entry => entry.Key.QualifiedItemId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.DisplayName, StringComparer.Ordinal)
            .Select(entry =>
            {
                var blockReasons = ShopStockPreviewBlockReasons(entry.Key, entry.Value);
                return new
                {
                    item_id = entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId,
                    qualified_item_id = entry.Key.QualifiedItemId,
                    display_name = entry.Key.DisplayName,
                    name = entry.Key.Name,
                    stack = entry.Key.Stack,
                    quality = entry.Key.Quality,
                    is_recipe = entry.Key.IsRecipe,
                    runtime_type = entry.Key.GetType().FullName,
                    price = entry.Value.Price,
                    stock = entry.Value.Stock,
                    infinite_stock = entry.Value.Stock == StardewValley.Menus.ShopMenu.infiniteStock,
                    trade_item = entry.Value.TradeItem,
                    trade_item_count = entry.Value.TradeItemCount,
                    effective_trade_item_count = entry.Value.TradeItem is null ? (int?)null : entry.Value.TradeItemCount ?? 5,
                    limited_stock_mode = entry.Value.LimitedStockMode.ToString(),
                    synced_key = entry.Value.SyncedKey,
                    action_on_purchase_count = entry.Value.ActionsOnPurchase?.Count ?? 0,
                    can_buy_item = entry.Key.CanBuyItem(Game1.player),
                    total_price_for_one_purchase = entry.Value.Price,
                    currency_balance = Game1.player.Money,
                    can_afford_one_with_currency = Game1.player.Money >= entry.Value.Price,
                    trade_item_available_count = entry.Value.TradeItem is null ? (int?)null : CountAvailableTradeItem(entry.Value.TradeItem),
                    can_afford_one_with_trade_item = entry.Value.TradeItem is null || CountAvailableTradeItem(entry.Value.TradeItem) >= (entry.Value.TradeItemCount ?? 5),
                    could_inventory_accept = entry.Key.GetSalableInstance() is Item salableItem && Game1.player.couldInventoryAcceptThisItem(salableItem),
                    action_when_purchased_may_discard_or_mutate = entry.Key.IsRecipe || entry.Value.ActionsOnPurchase?.Count > 0 || entry.Key.GetType() != typeof(StardewValley.Object),
                    executor_purchase_preview_enabled = blockReasons.Length == 0,
                    executor_block_reasons = blockReasons,
                    runtime_menu_recheck_required = true
                };
            })
            .ToArray();
        var anyEnabled = stock.Any(entry => entry.executor_purchase_preview_enabled);

        return new
        {
            kind = "shop_stock_preview",
            shop_id = shopId,
            source = "ShopBuilder.GetShopStock(shopId, shopData)",
            runtime_menu_recheck_required = true,
            executor_purchase_preview_enabled = anyEnabled,
            executor_block_reason = anyEnabled ? "" : "no_safe_executor_purchase_preview_candidate",
            entry_count = stock.Length,
            entries = stock
        };
    }

    private static string[] ShopStockPreviewBlockReasons(ISalable item, ItemStockInformation stock)
    {
        var reasons = new List<string>();

        if (stock.TradeItem is not null)
        {
            reasons.Add("trade_item_purchase_requires_consumption_audit");
        }

        if (stock.ActionsOnPurchase?.Count > 0)
        {
            reasons.Add("actions_on_purchase_present");
        }

        if (item.IsRecipe)
        {
            reasons.Add("recipe_purchase_discards_item_and_learns_recipe");
        }

        if (item.GetType() != typeof(StardewValley.Object))
        {
            reasons.Add("non_plain_object_purchase_side_effects_unmodeled");
        }

        if (stock.Stock != StardewValley.Menus.ShopMenu.infiniteStock &&
            (stock.LimitedStockMode.ToString() != "None" || stock.SyncedKey is not null))
        {
            reasons.Add("synchronized_or_limited_stock_requires_post_state_audit");
        }

        if (!item.CanBuyItem(Game1.player))
        {
            reasons.Add("shop_item_cannot_be_bought");
        }

        if (stock.Stock != StardewValley.Menus.ShopMenu.infiniteStock && stock.Stock <= 0)
        {
            reasons.Add("shop_item_out_of_stock");
        }

        if (Game1.player.Money < stock.Price)
        {
            reasons.Add("insufficient_currency_for_purchase");
        }

        if (stock.TradeItem is not null && CountAvailableTradeItem(stock.TradeItem) < (stock.TradeItemCount ?? 5))
        {
            reasons.Add("insufficient_trade_item_for_purchase");
        }

        if (item.GetSalableInstance() is not Item salableItem || !Game1.player.couldInventoryAcceptThisItem(salableItem))
        {
            reasons.Add("inventory_cannot_accept_purchase");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int CountAvailableTradeItem(string qualifiedOrUnqualifiedItemId)
    {
        return Game1.player.Items
            .Where(item => item is not null)
            .Where(item =>
                string.Equals(item!.QualifiedItemId, qualifiedOrUnqualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ItemId, qualifiedOrUnqualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item!.Stack);
    }

    private static object[] ReadEnumerableProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName) is System.Collections.IEnumerable enumerable
            ? enumerable.Cast<object>().ToArray()
            : Array.Empty<object>();
    }

    private static string? ReadStringProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName)?.ToString();
    }

    private static int? ReadIntProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName) as int?;
    }

    private static bool? ReadBoolProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName) as bool?;
    }

    private static string? ResolveShopEndpointId(GameLocation location, string[] parts)
    {
        if (parts.Length == 0)
        {
            return null;
        }

        if (string.Equals(parts[0], "JojaShop", StringComparison.OrdinalIgnoreCase))
        {
            return "Joja";
        }

        var rawShopId = Part(parts, 1);
        if (string.Equals(parts[0], "Buy", StringComparison.OrdinalIgnoreCase))
        {
            return ShopIdResolver.ResolveLegacyBuy(location, rawShopId);
        }

        return rawShopId;
    }

    private static string? Part(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length ? parts[index] : null;
    }

    private static int? ParseIntPart(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length && int.TryParse(parts[index], out var value)
            ? value
            : null;
    }

    private static object? ReadProperty(object source, string propertyName)
    {
        return source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(source);
    }
}
