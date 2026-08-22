using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string CookoutKitQualifiedItemId = "(O)926";
    private const string PlacedCookoutQualifiedItemId = "(BC)278";
    private static readonly object CookoutKitPlacementCacheLock = new();
    private static string cachedCookoutKitPlacementFingerprint = string.Empty;
    private static object? cachedCookoutKitPlacementContext;

    private static object ReadCookoutKitPlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new
            {
                projection_status = "unavailable_world_player_or_farm",
                inventory_cookout_kit_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var inventoryKits = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item is StardewValley.Object item &&
                string.Equals(item.QualifiedItemId, CookoutKitQualifiedItemId, StringComparison.Ordinal))
            .Select(entry => new InventoryCookoutKitRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_cookout_kit_count = inventoryKits.Length,
                inventory_cookout_kit_slots = inventoryKits.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locations = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var fingerprint = PersistentPlacementTopologyFingerprint(
            inventoryKits.Select(row =>
                "cookout_kit|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" +
                row.Item.Stack + "|" + row.Item.GetType().FullName)
                .Append("current_location|" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty)),
            locations);
        lock (CookoutKitPlacementCacheLock)
        {
            if (cachedCookoutKitPlacementContext is not null &&
                string.Equals(cachedCookoutKitPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedCookoutKitPlacementContext;
            }
        }

        var rows = inventoryKits.Select(row => ReadCookoutKitPlacementRow(row, locations)).ToArray();
        var context = new
        {
            schema_version = "cookout_kit_placement.v1",
            projection_status = "complete_inventory_cookout_kits_across_loaded_persistent_locations",
            inventory_cookout_kit_count = inventoryKits.Length,
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            qualified_item_id = CookoutKitQualifiedItemId,
            placed_qualified_item_id = PlacedCookoutQualifiedItemId,
            placed_runtime_type = typeof(Torch).FullName,
            placed_fragility = 1,
            destroy_over_night = true,
            lifetime = "current_day_only",
            layout_policy_owner = "small_model",
            route_safety_owner = "shared_collision_grid_and_adjacent_pathing",
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)926)->Torch((BC)278,destroyOvernight:true)",
            cooking_handoff_contract = "placed Torch.checkForAction opens CraftingPage(cooking:true)",
            rows
        };
        lock (CookoutKitPlacementCacheLock)
        {
            cachedCookoutKitPlacementFingerprint = fingerprint;
            cachedCookoutKitPlacementContext = context;
        }
        return context;
    }

    private static object ReadCookoutKitPlacementRow(
        InventoryCookoutKitRef inventoryKit,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var projections = locations
            .Select(location => ReadCookoutKitPlacementLocation(inventoryKit.Item, location))
            .ToArray();
        return new
        {
            inventory_slot_index = inventoryKit.SlotIndex,
            item_id = inventoryKit.Item.ItemId,
            qualified_item_id = inventoryKit.Item.QualifiedItemId,
            display_name = inventoryKit.Item.DisplayName,
            stack = inventoryKit.Item.Stack,
            inventory_runtime_type = inventoryKit.Item.GetType().FullName,
            placed_runtime_type = typeof(Torch).FullName,
            destroy_over_night = true,
            location_count = projections.Length,
            static_legal_tile_count = projections.Sum(row => row.StaticLegalTileCount),
            locations = projections.Select(row => row.Row).ToArray()
        };
    }

    private static CookoutKitPlacementLocationProjection ReadCookoutKitPlacementLocation(
        StardewValley.Object inventoryKit,
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
            var probe = (StardewValley.Object)inventoryKit.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            if (forbidden || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = forbidden
                    ? "native_location_placement_forbidden"
                    : !probe.isPlaceable()
                        ? "cookout_kit_not_placeable"
                        : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendPlacementLegalRanges(
                        ranges,
                        y,
                        width,
                        x => probe.canBePlacedHere(
                            location,
                            new Vector2(x, y),
                            ~(CollisionMask.Characters | CollisionMask.Farmers)),
                        ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_cookout_kit_placement_probe_exception:" + ex.GetType().Name;
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
            transient_occupancy_policy = "actors_do_not_remove_layout_candidates;runtime_rechecks_exact_tile",
            lifetime_constraint = "must_be_placed_and_used_before_day_end",
            runtime_recheck = "Utility.playerCanPlaceItemHere_at_exact_loaded_location_tile"
        };
        return new CookoutKitPlacementLocationProjection(row, legalCount);
    }

    private sealed record InventoryCookoutKitRef(StardewValley.Object Item, int SlotIndex);
    private sealed record CookoutKitPlacementLocationProjection(object Row, int StaticLegalTileCount);
}
