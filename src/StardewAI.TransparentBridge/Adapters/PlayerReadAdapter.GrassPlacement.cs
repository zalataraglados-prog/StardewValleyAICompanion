using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly object GrassPlacementCacheLock = new();
    private static string cachedGrassPlacementFingerprint = string.Empty;
    private static object? cachedGrassPlacementContext;

    private static object ReadGrassPlacementContext(Farmer? player)
    {
        if (player is null || Game1.currentLocation is not { } location)
        {
            return new { projection_status = "unavailable_world_player_or_location", inventory_grass_starter_count = 0, rows = Array.Empty<object>() };
        }

        var inventory = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                entry.item is StardewValley.Object obj && TryGrassStarterType(obj.QualifiedItemId, out _))
            .Select(entry => new InventoryGrassStarterRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_grass_starter_count = inventory.Length,
                inventory_grass_starter_slots = inventory.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locationRef = new MachineLocationRef(
            location,
            "current_loaded_location",
            ReferenceEquals(location, player.currentLocation),
            location.GetRootLocation().NameOrUniqueName,
            location.ParentBuilding?.GetType().FullName ?? string.Empty);
        var locations = new[] { locationRef };
        var fingerprintRows = inventory.Select(row =>
                "grass_starter|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" + row.Item.Stack + "|" + row.Item.GetType().FullName)
            .Append("current_location|" + location.NameOrUniqueName);
        var fingerprint = PersistentPlacementTopologyFingerprint(fingerprintRows, locations);
        lock (GrassPlacementCacheLock)
        {
            if (cachedGrassPlacementContext is not null &&
                string.Equals(cachedGrassPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedGrassPlacementContext;
            }
        }

        var rows = inventory.Select(row => ReadGrassPlacementRow(row, locationRef)).ToArray();
        var context = new
        {
            schema_version = "grass_placement.v1",
            projection_status = "complete_inventory_grass_starters_for_current_loaded_location",
            inventory_grass_starter_count = inventory.Length,
            location_count = 1,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            source_runtime_type = typeof(StardewValley.Object).FullName,
            placed_runtime_type = typeof(Grass).FullName,
            supported_qualified_item_ids = new[] { "(O)297", "(O)BlueGrassStarter" },
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)297|(O)BlueGrassStarter)->terrainFeatures.Add(Grass(type,4))",
            layout_owner = "upstream purpose and layout planner selects one exact tile; executor only rebinds and applies",
            route_safety_owner = "shared_collision_grid_passable_target_bfs",
            rows
        };
        lock (GrassPlacementCacheLock)
        {
            cachedGrassPlacementFingerprint = fingerprint;
            cachedGrassPlacementContext = context;
        }
        return context;
    }

    private static object ReadGrassPlacementRow(InventoryGrassStarterRef inventory, MachineLocationRef location)
    {
        TryGrassStarterType(inventory.Item.QualifiedItemId, out var grassType);
        var projection = ReadGrassPlacementLocation(inventory.Item, location);
        return new
        {
            inventory_slot_index = inventory.SlotIndex,
            item_id = inventory.Item.ItemId,
            qualified_item_id = inventory.Item.QualifiedItemId,
            display_name = inventory.Item.DisplayName,
            stack = inventory.Item.Stack,
            inventory_runtime_type = inventory.Item.GetType().FullName,
            placed_runtime_type = typeof(Grass).FullName,
            expected_grass_type = grassType,
            expected_initial_number_of_weeds = 4,
            placement_sound = "dirtyHit",
            expected_passable = true,
            static_legal_tile_count = projection.StaticLegalTileCount,
            locations = new[] { projection.Row }
        };
    }

    private static GrassPlacementLocationProjection ReadGrassPlacementLocation(
        StardewValley.Object inventoryGrass, MachineLocationRef locationRef)
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
            var probe = (StardewValley.Object)inventoryGrass.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            if (probe.GetType() != typeof(StardewValley.Object) || forbidden || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = probe.GetType() != typeof(StardewValley.Object)
                    ? "custom_inventory_runtime_type_blocked"
                    : forbidden ? "native_location_placement_forbidden"
                    : !probe.isPlaceable() ? "grass_starter_not_placeable"
                    : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendGrassLegalRanges(ranges, y, width, x =>
                    {
                        var tile = new Vector2(x, y);
                        return !location.objects.ContainsKey(tile) &&
                            !location.terrainFeatures.ContainsKey(tile) &&
                            probe.canBePlacedHere(location, tile, ~(CollisionMask.Characters | CollisionMask.Farmers));
                    }, ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_grass_placement_probe_exception:" + ex.GetType().Name;
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
            static_collision_mask = "CollisionMask.All_without_Characters_or_Farmers",
            object_policy = "target_must_have_no_existing_object",
            terrain_feature_policy = "target_must_have_no_existing_terrain_feature",
            transient_occupancy_policy = "actors_do_not_remove_layout_candidates;runtime_rechecks_exact_tile",
            passability_policy = "Grass.isPassable;placement_does_not_virtual_block_route",
            runtime_recheck = "Utility.playerCanPlaceItemHere_then_exact_live_object_and_terrain_feature_absence"
        };
        return new GrassPlacementLocationProjection(row, legalCount);
    }

    private static void AppendGrassLegalRanges(
        ICollection<object> ranges, int y, int width, Func<int, bool> legalAt, ref int legalCount)
    {
        int? start = null;
        for (var x = 0; x <= width; x++)
        {
            var legal = x < width && legalAt(x);
            if (legal)
            {
                legalCount++;
                start ??= x;
            }
            else if (start.HasValue)
            {
                ranges.Add(new { y, start_x = start.Value, end_x = x - 1 });
                start = null;
            }
        }
    }

    private static bool TryGrassStarterType(string qualifiedItemId, out int grassType)
    {
        grassType = string.Equals(qualifiedItemId, "(O)297", StringComparison.Ordinal) ? 1 :
            string.Equals(qualifiedItemId, "(O)BlueGrassStarter", StringComparison.Ordinal) ? 7 : -1;
        return grassType >= 0;
    }

    private sealed record InventoryGrassStarterRef(StardewValley.Object Item, int SlotIndex);
    private sealed record GrassPlacementLocationProjection(object Row, int StaticLegalTileCount);
}
