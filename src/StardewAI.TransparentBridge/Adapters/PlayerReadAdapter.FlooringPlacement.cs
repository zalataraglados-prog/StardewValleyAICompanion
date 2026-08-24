using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.GameData.FloorsAndPaths;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly object FlooringPlacementCacheLock = new();
    private static string cachedFlooringPlacementFingerprint = string.Empty;
    private static object? cachedFlooringPlacementContext;

    private static object ReadFlooringPlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new { projection_status = "unavailable_world_player_or_farm", inventory_flooring_count = 0, rows = Array.Empty<object>() };
        }

        var lookup = Flooring.GetFloorPathItemLookup();
        var inventoryFlooring = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                entry.item is StardewValley.Object item && item.IsFloorPathItem() && lookup.ContainsKey(item.ItemId))
            .Select(entry => new InventoryFlooringRef((StardewValley.Object)entry.item!, entry.slot, lookup[((StardewValley.Object)entry.item!).ItemId]))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_flooring_count = inventoryFlooring.Length,
                inventory_flooring_slots = inventoryFlooring.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var persistentLocations = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var currentLocation = Game1.currentLocation;
        var currentRef = persistentLocations.FirstOrDefault(row => ReferenceEquals(row.Location, currentLocation) ||
            string.Equals(row.Location.NameOrUniqueName, currentLocation?.NameOrUniqueName, StringComparison.OrdinalIgnoreCase));
        if (currentRef is null && currentLocation is not null)
        {
            currentRef = new MachineLocationRef(
                currentLocation,
                "current_generated_or_nonpersistent",
                false,
                currentLocation.GetRootLocation().NameOrUniqueName,
                currentLocation.ParentBuilding?.GetType().FullName ?? string.Empty);
        }
        var locations = currentRef is null ? Array.Empty<MachineLocationRef>() : new[] { currentRef };
        var catalogRows = Game1.floorPathData
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.ItemId))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => ReadFloorPathCatalogRow(pair.Key, pair.Value))
            .ToArray();
        var fingerprintRows = inventoryFlooring.Select(row =>
            {
                Game1.floorPathData.TryGetValue(row.FloorDataKey, out var data);
                return "flooring|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" + row.Item.Stack + "|" +
                    row.Item.GetType().FullName + "|" + row.FloorDataKey + "|" + (data?.ItemId ?? string.Empty) + "|" +
                    (data?.ConnectType.ToString() ?? string.Empty) + "|" + (data?.PlacementSound ?? string.Empty) + "|" +
                    (data?.RemovalSound ?? string.Empty) + "|" + (data?.FarmSpeedBuff.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
            })
            .Concat(Game1.floorPathData
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => "floor_data|" + pair.Key + "|" + (pair.Value.ItemId ?? string.Empty) + "|" +
                    pair.Value.ConnectType + "|" + pair.Value.ShadowType + "|" + (pair.Value.PlacementSound ?? string.Empty) + "|" +
                    (pair.Value.RemovalSound ?? string.Empty) + "|" + pair.Value.RemovalDebrisType + "|" +
                    (pair.Value.FootstepSound ?? string.Empty) + "|" + pair.Value.FarmSpeedBuff.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Concat(locations.SelectMany(location => location.Location.terrainFeatures.Pairs
                .Where(pair => pair.Value is Flooring)
                .OrderBy(pair => pair.Key.Y)
                .ThenBy(pair => pair.Key.X)
                .Select(pair =>
                {
                    var flooring = (Flooring)pair.Value;
                    return "flooring_topology|" + location.Location.NameOrUniqueName + "|" +
                        (int)pair.Key.X + "," + (int)pair.Key.Y + "|" + flooring.whichFloor.Value + "|" +
                        flooring.whichView.Value + "|" + flooring.GetType().FullName;
                })))
            .Append("current_location|" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty));
        var fingerprint = PersistentPlacementTopologyFingerprint(fingerprintRows, locations);
        lock (FlooringPlacementCacheLock)
        {
            if (cachedFlooringPlacementContext is not null &&
                string.Equals(cachedFlooringPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedFlooringPlacementContext;
            }
        }

        var rows = inventoryFlooring.Select(row => ReadFlooringPlacementRow(row, locations)).ToArray();
        var context = new
        {
            schema_version = "flooring_placement.v1",
            projection_status = "complete_inventory_flooring_for_current_loaded_location",
            inventory_flooring_count = inventoryFlooring.Length,
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            source_runtime_type = typeof(StardewValley.Object).FullName,
            placed_runtime_type = typeof(Flooring).FullName,
            floor_path_data_source = "Data/FloorsAndPaths via Game1.floorPathData and Flooring.GetFloorPathItemLookup",
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFloorPathItem)->terrainFeatures.Add(Flooring)",
            replacement_policy = "native placement rejects every tile already containing any TerrainFeature; removal is a separate Axe/Pickaxe/damage action",
            route_safety_owner = "shared_collision_grid_passable_target_bfs",
            floor_path_catalog_count = catalogRows.Length,
            floor_path_catalog = catalogRows,
            rows
        };
        lock (FlooringPlacementCacheLock)
        {
            cachedFlooringPlacementFingerprint = fingerprint;
            cachedFlooringPlacementContext = context;
        }
        return context;
    }

    private static object ReadFloorPathCatalogRow(string key, FloorPathData data) => new
    {
        floor_data_key = key,
        floor_data_id = data.Id,
        item_id = data.ItemId,
        qualified_item_id = ItemRegistry.QualifyItemId(data.ItemId),
        connect_type = data.ConnectType.ToString(),
        shadow_type = data.ShadowType.ToString(),
        placement_sound = data.PlacementSound,
        removal_sound = data.RemovalSound,
        removal_debris_type = data.RemovalDebrisType,
        footstep_sound = data.FootstepSound,
        corner_size = data.CornerSize,
        farm_speed_buff = data.FarmSpeedBuff,
        is_item_lookup_target = !string.IsNullOrWhiteSpace(data.ItemId) &&
            Flooring.GetFloorPathItemLookup().TryGetValue(data.ItemId, out var resolvedKey) &&
            string.Equals(resolvedKey, key, StringComparison.Ordinal),
        source = "Game1.floorPathData"
    };

    private static object ReadFlooringPlacementRow(
        InventoryFlooringRef inventoryFlooring,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var item = inventoryFlooring.Item;
        Game1.floorPathData.TryGetValue(inventoryFlooring.FloorDataKey, out var data);
        var projections = locations
            .Select(location => ReadFlooringPlacementLocation(item, inventoryFlooring.FloorDataKey, location))
            .ToArray();
        var random = data?.ConnectType == FloorPathConnectType.Random;
        return new
        {
            inventory_slot_index = inventoryFlooring.SlotIndex,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            display_name = item.DisplayName,
            stack = item.Stack,
            inventory_runtime_type = item.GetType().FullName,
            placed_runtime_type = typeof(Flooring).FullName,
            floor_data_key = inventoryFlooring.FloorDataKey,
            floor_data_id = data?.Id,
            floor_data_item_id = data?.ItemId,
            connect_type = data?.ConnectType.ToString(),
            shadow_type = data?.ShadowType.ToString(),
            placement_sound = data?.PlacementSound,
            removal_sound = data?.RemovalSound,
            removal_debris_type = data?.RemovalDebrisType,
            footstep_sound = data?.FootstepSound,
            farm_speed_buff = data?.FarmSpeedBuff,
            expected_passable = true,
            expected_which_view_min = random ? 0 : 0,
            expected_which_view_max = random ? 15 : 0,
            which_view_contract = random ? "native_random_domain_0_through_15" : "constructor_default_zero;rendering_uses_neighbor_mask",
            location_count = projections.Length,
            static_legal_tile_count = projections.Sum(row => row.StaticLegalTileCount),
            locations = projections.Select(row => row.Row).ToArray()
        };
    }

    private static FlooringPlacementLocationProjection ReadFlooringPlacementLocation(
        StardewValley.Object inventoryFlooring,
        string floorDataKey,
        MachineLocationRef locationRef)
    {
        var location = locationRef.Location;
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var forbidden = Utility.isPlacementForbiddenHere(location);
        var ranges = new List<object>();
        var legalCount = 0;
        string status;
        try
        {
            var probe = (StardewValley.Object)inventoryFlooring.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            if (probe.GetType() != typeof(StardewValley.Object) || forbidden || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = probe.GetType() != typeof(StardewValley.Object)
                    ? "custom_inventory_runtime_type_blocked"
                    : forbidden ? "native_location_placement_forbidden"
                    : !probe.isPlaceable() ? "flooring_item_not_placeable"
                    : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendFlooringLegalRanges(ranges, y, width, x =>
                    {
                        var tile = new Vector2(x, y);
                        var legal = !location.terrainFeatures.ContainsKey(tile) && probe.canBePlacedHere(
                            location, tile, ~(CollisionMask.Characters | CollisionMask.Farmers));
                        return new FlooringTileSignature(legal, legal ? ReadFlooringConnectionMask(location, tile, floorDataKey) : 0);
                    }, ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_flooring_placement_probe_exception:" + ex.GetType().Name;
        }

        var row = new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_runtime_type = location.GetType().FullName,
            location_kind = locationRef.Kind,
            root_location_id = locationRef.RootLocationId,
            parent_building_runtime_type = locationRef.ParentBuildingRuntimeType,
            location_is_player_controlled = locationRef.IsPlayerControlled,
            location_is_current = ReferenceEquals(location, Game1.currentLocation),
            location_is_outdoors = location.IsOutdoors,
            map_width = width,
            map_height = height,
            native_location_placement_forbidden = forbidden,
            placement_probe_status = status,
            static_legal_tile_count = legalCount,
            static_legal_tile_ranges = ranges.ToArray(),
            static_collision_mask = "CollisionMask.All_without_Characters_or_Farmers;Object.canBePlacedHere_removes_Buildings_for_flooring",
            terrain_feature_policy = "target_must_have_no_existing_terrain_feature",
            transient_occupancy_policy = "actors_do_not_remove_layout_candidates;runtime_rechecks_exact_tile",
            passability_policy = "Flooring.isPassable_always_true;placement_does_not_virtual_block_route",
            runtime_recheck = "Utility.playerCanPlaceItemHere_then_exact_live_same_floor_eight_neighbor_mask"
        };
        return new FlooringPlacementLocationProjection(row, legalCount);
    }

    internal static int ReadFlooringConnectionMask(GameLocation location, Vector2 tile, string floorDataKey)
    {
        var mask = 0;
        AddFlooringConnection(location, tile + Flooring.N_Offset, floorDataKey, Flooring.N, ref mask);
        AddFlooringConnection(location, tile + Flooring.E_Offset, floorDataKey, Flooring.E, ref mask);
        AddFlooringConnection(location, tile + Flooring.S_Offset, floorDataKey, Flooring.S, ref mask);
        AddFlooringConnection(location, tile + Flooring.W_Offset, floorDataKey, Flooring.W, ref mask);
        AddFlooringConnection(location, tile + Flooring.NE_Offset, floorDataKey, Flooring.NE, ref mask);
        AddFlooringConnection(location, tile + Flooring.NW_Offset, floorDataKey, Flooring.NW, ref mask);
        AddFlooringConnection(location, tile + Flooring.SE_Offset, floorDataKey, Flooring.SE, ref mask);
        AddFlooringConnection(location, tile + Flooring.SW_Offset, floorDataKey, Flooring.SW, ref mask);
        return mask;
    }

    private static void AddFlooringConnection(GameLocation location, Vector2 neighborTile, string floorDataKey, byte direction, ref int mask)
    {
        if ((location.map is not null && !location.isTileOnMap(neighborTile)) ||
            (location.terrainFeatures.TryGetValue(neighborTile, out var feature) &&
             feature is Flooring flooring && string.Equals(flooring.whichFloor.Value, floorDataKey, StringComparison.Ordinal)))
        {
            mask |= direction;
        }
    }

    private static void AppendFlooringLegalRanges(
        ICollection<object> ranges, int y, int width, Func<int, FlooringTileSignature> read, ref int legalCount)
    {
        int? start = null;
        var current = default(FlooringTileSignature);
        for (var x = 0; x <= width; x++)
        {
            var next = x < width ? read(x) : default;
            if (next.Legal)
            {
                legalCount++;
                if (!start.HasValue)
                {
                    start = x;
                    current = next;
                }
                else if (next != current)
                {
                    ranges.Add(FlooringRange(y, start.Value, x - 1, current));
                    start = x;
                    current = next;
                }
            }
            else if (start.HasValue)
            {
                ranges.Add(FlooringRange(y, start.Value, x - 1, current));
                start = null;
            }
        }
    }

    private static object FlooringRange(int y, int startX, int endX, FlooringTileSignature signature) => new
    {
        y,
        start_x = startX,
        end_x = endX,
        expected_neighbor_mask_after = signature.NeighborMask
    };

    private sealed record InventoryFlooringRef(StardewValley.Object Item, int SlotIndex, string FloorDataKey);
    private sealed record FlooringPlacementLocationProjection(object Row, int StaticLegalTileCount);
    private readonly record struct FlooringTileSignature(bool Legal, int NeighborMask);
}
