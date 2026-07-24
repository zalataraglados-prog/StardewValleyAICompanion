using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly object StoragePlacementCacheLock = new();
    private static string cachedStoragePlacementFingerprint = string.Empty;
    private static object? cachedStoragePlacementContext;

    private static object ReadStoragePlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new
            {
                projection_status = "unavailable_world_player_or_farm",
                inventory_storage_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var inventoryStorage = player.Items
            .Select((item, slot) => new { item, slot })
            .Select(entry => entry.item is StardewValley.Object item &&
                    TryClassifyNativeStoragePlacement(
                        item,
                        out var branch)
                ? new InventoryStorageRef(
                    item,
                    entry.slot,
                    branch)
                : null)
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();
        if (!SnapshotProfileContext.IncludesPersistentMaterialInventoryGraph)
        {
            return new
            {
                projection_status =
                    "blocked_requires_daily_training_machine_fishing_or_full_profile",
                inventory_storage_count = inventoryStorage.Length,
                inventory_storage_slots = inventoryStorage
                    .Select(row => row.SlotIndex)
                    .ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locations =
            MachineLocationTopology.ReadPersistentLocations(
                farm,
                player);
        var fingerprint = PersistentPlacementTopologyFingerprint(
            inventoryStorage.Select(row =>
                "storage|" + row.SlotIndex + "|" +
                row.Item.QualifiedItemId + "|" +
                row.Item.Stack + "|" +
                row.Item.GetType().FullName + "|" +
                row.Branch),
            locations);
        lock (StoragePlacementCacheLock)
        {
            if (cachedStoragePlacementContext is not null &&
                string.Equals(
                    cachedStoragePlacementFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                return cachedStoragePlacementContext;
            }
        }

        var rows = inventoryStorage
            .Select(storage =>
                ReadStoragePlacementRow(storage, locations))
            .ToArray();
        var context = new
        {
            schema_version = "storage_placement.v1",
            projection_status =
                "complete_inventory_player_chests_across_persistent_player_locations",
            inventory_storage_count = inventoryStorage.Length,
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            location_scope =
                "Utility.ForEachLocation(includeInteriors:true,includeGenerated:false)_plus_native_player_home_topology",
            layout_policy_owner = "small_model",
            placement_fact_owner = "transparent_bridge",
            route_safety_owner =
                "core_collision_grid_articulation_and_adjacent_access_projection",
            actionability =
                "read_only_until_route_safe_candidate_compiler_and_native_executor_are_connected",
            native_runtime_contract =
                "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Chest.placementAction",
            rows
        };
        lock (StoragePlacementCacheLock)
        {
            cachedStoragePlacementFingerprint = fingerprint;
            cachedStoragePlacementContext = context;
        }
        return context;
    }

    private static object ReadStoragePlacementRow(
        InventoryStorageRef inventoryStorage,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var item = inventoryStorage.Item;
        var probe = CreateStoragePlacementProbe(
            item,
            inventoryStorage.Branch);
        var locationProjections = locations
            .Select(location =>
                ReadStoragePlacementLocation(
                    item,
                    inventoryStorage.Branch,
                    location))
            .ToArray();
        return new
        {
            inventory_slot_index = inventoryStorage.SlotIndex,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            display_name = item.DisplayName,
            stack = item.Stack,
            runtime_type = item.GetType().FullName,
            native_storage_branch = inventoryStorage.Branch,
            placed_runtime_type = typeof(Chest).FullName,
            special_chest_type = probe.SpecialChestType.ToString(),
            actual_capacity = probe.GetActualCapacity(),
            global_inventory_id = probe.GlobalInventoryId ?? string.Empty,
            ordinary_material_storage =
                (probe.SpecialChestType is
                    Chest.SpecialChestTypes.None or
                    Chest.SpecialChestTypes.BigChest) &&
                inventoryStorage.Branch !=
                    "native_object_placement_mini_fridge" &&
                inventoryStorage.Branch !=
                    "native_object_placement_mini_shipping_bin",
            shared_global_storage =
                probe.SpecialChestType ==
                Chest.SpecialChestTypes.JunimoChest,
            shipping_storage =
                probe.SpecialChestType ==
                Chest.SpecialChestTypes.MiniShippingBin,
            fridge_storage =
                inventoryStorage.Branch ==
                "native_object_placement_mini_fridge",
            location_count = locationProjections.Length,
            static_legal_tile_count =
                locationProjections.Sum(row =>
                    row.StaticLegalTileCount),
            locations = locationProjections
                .Select(row => row.Row)
                .ToArray()
        };
    }

    private static StoragePlacementLocationProjection
        ReadStoragePlacementLocation(
            StardewValley.Object inventoryItem,
            string branch,
            MachineLocationRef locationRef)
    {
        var location = locationRef.Location;
        var layers = location.map?.Layers?
            .Cast<xTile.Layers.Layer>()
            .ToArray() ??
            Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0
            ? 0
            : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0
            ? 0
            : layers.Max(layer => layer.LayerHeight);
        var forbidden = Utility.isPlacementForbiddenHere(location);
        var staticRanges = new List<object>();
        var staticCount = 0;
        string status;

        try
        {
            var probe = CreateStoragePlacementProbe(
                inventoryItem,
                branch);
            var branchAllowed = NativeStorageBranchAllowsLocation(
                branch,
                location,
                out var branchBlockReason);
            if (forbidden ||
                     !branchAllowed ||
                     !probe.isPlaceable() ||
                     width <= 0 ||
                     height <= 0)
            {
                status = forbidden
                    ? "native_location_placement_forbidden"
                    : !branchAllowed
                        ? branchBlockReason
                    : !probe.isPlaceable()
                        ? "storage_item_not_placeable"
                        : "location_map_dimensions_unavailable";
            }
            else
            {
                probe.Location = location;
                probe.TileLocation = Vector2.Zero;
                for (var y = 0; y < height; y++)
                {
                    AppendPlacementLegalRanges(
                        staticRanges,
                        y,
                        width,
                        x => probe.canBePlacedHere(
                            location,
                            new Vector2(x, y),
                            ~(CollisionMask.Characters |
                              CollisionMask.Farmers)),
                        ref staticCount);
                }
                status = staticCount > 0
                    ? "native_legal_tiles_available"
                    : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status =
                "native_storage_placement_probe_exception:" +
                ex.GetType().Name;
        }

        var row = new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_runtime_type =
                location.GetType().FullName,
            location_kind = locationRef.Kind,
            root_location_id = locationRef.RootLocationId,
            parent_building_runtime_type =
                locationRef.ParentBuildingRuntimeType,
            location_is_player_controlled =
                locationRef.IsPlayerControlled,
            location_is_current =
                ReferenceEquals(location, Game1.currentLocation),
            location_is_outdoors = location.IsOutdoors,
            location_is_greenhouse = location.IsGreenhouse,
            map_width = width,
            map_height = height,
            native_location_placement_forbidden = forbidden,
            placement_probe_status = status,
            static_legal_tile_count = staticCount,
            static_legal_tile_ranges = staticRanges.ToArray(),
            static_collision_mask =
                "CollisionMask.All_without_Characters_or_Farmers",
            transient_occupancy_policy =
                "actors_do_not_remove_layout_candidates;runtime_rechecks_exact_tile",
            access_stand_policy =
                "core_must_preserve_one_reachable_cardinal_neighbor_after_virtual_placement",
            route_continuity_policy =
                "core_must_reject_current_map_articulation_tiles_and_action_warp_door_tiles",
            runtime_recheck =
                "Utility.playerCanPlaceItemHere_at_exact_loaded_location_tile"
        };
        return new StoragePlacementLocationProjection(
            row,
            staticCount);
    }

    private static bool TryClassifyNativeStoragePlacement(
        StardewValley.Object item,
        out string branch)
    {
        branch = item.QualifiedItemId switch
        {
            "(BC)130" or "(BC)232" =>
                "native_object_placement_normal_chest",
            "(BC)BigChest" or "(BC)BigStoneChest" =>
                "native_object_placement_big_chest",
            "(BC)216" =>
                "native_object_placement_mini_fridge",
            "(BC)248" =>
                "native_object_placement_mini_shipping_bin",
            "(BC)256" =>
                "native_object_placement_junimo_chest",
            _ => string.Empty
        };
        if (branch.Length > 0)
        {
            return true;
        }

        if (item is Chest chest && chest.playerChest.Value)
        {
            branch = "inventory_runtime_chest_placement";
            return true;
        }

        return false;
    }

    private static Chest CreateStoragePlacementProbe(
        StardewValley.Object item,
        string branch)
    {
        Chest probe;
        if (branch == "native_object_placement_mini_fridge")
        {
            probe = new Chest(
                item.ItemId,
                Vector2.Zero,
                starting_lid_frame: 217,
                lid_frame_count: 2);
            probe.fridge.Value = true;
        }
        else
        {
            probe = new Chest(
                playerChest: true,
                tileLocation: Vector2.Zero,
                itemId: item.ItemId);
        }

        probe.CopyFieldsFrom(item);
        probe.SetSpecialChestType();
        return probe;
    }

    private static bool NativeStorageBranchAllowsLocation(
        string branch,
        GameLocation location,
        out string blockReason)
    {
        if (branch == "inventory_runtime_chest_placement")
        {
            blockReason = string.Empty;
            return true;
        }

        if (branch == "native_object_placement_mini_fridge")
        {
            if (location.TryGetMapPropertyAs(
                    "AllowMiniFridges",
                    out bool allowed,
                    required: false))
            {
                blockReason = allowed
                    ? string.Empty
                    : "native_mini_fridge_map_property_denied";
                return allowed;
            }

            if (location is FarmHouse { upgradeLevel: < 1 })
            {
                blockReason =
                    "native_mini_fridge_requires_kitchen";
                return false;
            }

            var allowedByLocationType =
                location is FarmHouse or IslandFarmHouse;
            blockReason = allowedByLocationType
                ? string.Empty
                : "native_mini_fridge_location_type_denied";
            return allowedByLocationType;
        }

        if (location is MineShaft or VolcanoDungeon)
        {
            blockReason =
                "native_storage_branch_forbids_mine_or_volcano";
            return false;
        }

        blockReason = string.Empty;
        return true;
    }

    private sealed record InventoryStorageRef(
        StardewValley.Object Item,
        int SlotIndex,
        string Branch);

    private sealed record StoragePlacementLocationProjection(
        object Row,
        int StaticLegalTileCount);
}
