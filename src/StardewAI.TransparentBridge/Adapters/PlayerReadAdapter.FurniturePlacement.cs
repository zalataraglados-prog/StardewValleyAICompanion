using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly object FurniturePlacementCacheLock = new();
    private static string cachedFurniturePlacementFingerprint = string.Empty;
    private static object? cachedFurniturePlacementContext;

    private static object ReadFurniturePlacementContext(Farmer? player)
    {
        if (player is null || Game1.currentLocation is not { } currentLocation)
        {
            return new { projection_status = "unavailable_world_player_or_current_location", inventory_furniture_count = 0, rows = Array.Empty<object>() };
        }

        var inventory = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item is Furniture)
            .Select(entry => new InventoryFurnitureRef((Furniture)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_furniture_count = inventory.Length,
                inventory_furniture_slots = inventory.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var furnitureData = Game1.content.Load<Dictionary<string, string>>("Data\\Furniture");
        var catalog = furnitureData
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => ReadFurnitureCatalogRow(pair.Key, pair.Value))
            .ToArray();
        var locationRef = new MachineLocationRef(
            currentLocation,
            "current_loaded_location",
            currentLocation.IsFarm || currentLocation.ParentBuilding is not null,
            currentLocation.GetRootLocation().NameOrUniqueName,
            currentLocation.ParentBuilding?.GetType().FullName ?? string.Empty);
        var locations = new[] { locationRef };
        var fingerprintRows = inventory.Select(row => FurnitureIdentityFingerprint("inventory", row.SlotIndex, row.Item))
            .Concat(furnitureData.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => "data|" + pair.Key + "|" + pair.Value))
            .Concat(currentLocation.furniture.Select((item, index) => FurnitureIdentityFingerprint("placed", index, item)))
            .Append("location|" + currentLocation.NameOrUniqueName + "|" + currentLocation.CanFreePlaceFurniture());
        var fingerprint = PersistentPlacementTopologyFingerprint(fingerprintRows, locations);
        lock (FurniturePlacementCacheLock)
        {
            if (cachedFurniturePlacementContext is not null &&
                string.Equals(cachedFurniturePlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedFurniturePlacementContext;
            }
        }

        var rows = inventory.Select(row => ReadFurniturePlacementRow(row, locationRef)).ToArray();
        var context = new
        {
            schema_version = "furniture_placement.v1",
            projection_status = "complete_inventory_furniture_for_current_loaded_location",
            inventory_furniture_count = inventory.Length,
            location_count = 1,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            furniture_data_source = "Data/Furniture plus Furniture.GetFurnitureInstance",
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Furniture.placementAction->Object.placementAction->location.furniture_or_table.heldObject",
            rotation_contract = "Furniture.rotate virtual method repeated from exact inventory currentRotation; direct currentRotation writes prohibited",
            reach_contract = "CanFreePlaceFurniture allows native remote placement; otherwise exact cardinal reachable stand is required",
            replacement_policy = "native canBePlacedHere and table AllowPlacementOnThisTile decide overlap; no executor-side replacement",
            furniture_catalog_count = catalog.Length,
            furniture_catalog = catalog,
            rows
        };
        lock (FurniturePlacementCacheLock)
        {
            cachedFurniturePlacementFingerprint = fingerprint;
            cachedFurniturePlacementContext = context;
        }
        return context;
    }

    private static object ReadFurnitureCatalogRow(string itemId, string raw)
    {
        var fields = raw.Split('/');
        Furniture? instance = null;
        string status;
        try
        {
            instance = Furniture.GetFurnitureInstance(itemId);
            status = ItemRegistry.GetDataOrErrorItem("(F)" + itemId).IsErrorItem
                ? "item_registry_error"
                : "canonical_factory_available";
        }
        catch (Exception ex)
        {
            status = "canonical_factory_exception:" + ex.GetType().Name;
        }
        return new
        {
            item_id = itemId,
            qualified_item_id = "(F)" + itemId,
            internal_name = fields.ElementAtOrDefault(0),
            furniture_type_name = fields.ElementAtOrDefault(1),
            source_size_raw = fields.ElementAtOrDefault(2),
            bounding_size_raw = fields.ElementAtOrDefault(3),
            rotations_raw = fields.ElementAtOrDefault(4),
            price_raw = fields.ElementAtOrDefault(5),
            placement_restriction_raw = fields.ElementAtOrDefault(6),
            raw_fields = fields,
            raw_data = raw,
            canonical_factory_status = status,
            canonical_runtime_type = instance?.GetType().FullName,
            furniture_type = instance?.furniture_type.Value,
            rotations = instance?.rotations.Value,
            placement_restriction = instance?.placementRestriction,
            is_ground_furniture = instance?.isGroundFurniture(),
            is_passable = instance?.isPassable(),
            default_tiles_wide = instance?.getTilesWide(),
            default_tiles_high = instance?.getTilesHigh()
        };
    }

    private static object ReadFurniturePlacementRow(InventoryFurnitureRef inventory, MachineLocationRef location)
    {
        var item = inventory.Item;
        var factory = TryGetFurnitureFactory(item.ItemId);
        var supported = IsVanillaFurnitureRuntimeType(item.GetType()) && factory is not null;
        var expectedPlacedType = item.GetType() == typeof(Furniture) && factory?.GetType() != typeof(Furniture)
            ? typeof(StorageFurniture).FullName
            : item.GetType().FullName;
        var rotations = supported ? ReadFurnitureRotations(item, location) : Array.Empty<object>();
        return new
        {
            inventory_slot_index = inventory.SlotIndex,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            display_name = item.DisplayName,
            stack = item.Stack,
            inventory_runtime_type = item.GetType().FullName,
            canonical_factory_runtime_type = factory?.GetType().FullName,
            expected_placed_runtime_type = expectedPlacedType,
            runtime_type_supported = supported,
            runtime_type_status = supported ? "vanilla_factory_bound" : "custom_or_missing_factory_blocked",
            inventory_current_rotation = item.currentRotation.Value,
            native_rotation_count = item.rotations.Value,
            furniture_type = item.furniture_type.Value,
            placement_restriction = item.placementRestriction,
            is_ground_furniture = item.isGroundFurniture(),
            is_passable = item.isPassable(),
            held_object_qualified_item_id = item.heldObject.Value?.QualifiedItemId,
            storage_item_count = item is StorageFurniture storage ? storage.heldItems.Count(entry => entry is not null) : 0,
            rotation_state_count = rotations.Length,
            rotations
        };
    }

    private static object[] ReadFurnitureRotations(Furniture source, MachineLocationRef location)
    {
        var probe = Furniture.GetFurnitureInstance(source.ItemId);
        for (var attempts = 0; probe.currentRotation.Value != source.currentRotation.Value && attempts < 4; attempts++)
        {
            probe.rotate();
        }
        if (probe.currentRotation.Value != source.currentRotation.Value)
        {
            return Array.Empty<object>();
        }
        var seen = new HashSet<int>();
        var rows = new List<object>();
        for (var steps = 0; steps < 4 && seen.Add(probe.currentRotation.Value); steps++)
        {
            rows.Add(ReadFurnitureRotationRow(probe, source.currentRotation.Value, steps, location));
            probe.rotate();
        }
        return rows.ToArray();
    }

    private static object ReadFurnitureRotationRow(
        Furniture rotationProbe, int inventoryRotation, int rotationSteps, MachineLocationRef locationRef)
    {
        var location = locationRef.Location;
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var ranges = new List<object>();
        var legalCount = 0;
        var forbidden = Utility.isPlacementForbiddenHere(location);
        string status;
        try
        {
            if (forbidden || width <= 0 || height <= 0 || !location.CanPlaceThisFurnitureHere(rotationProbe))
            {
                status = forbidden ? "native_location_placement_forbidden"
                    : width <= 0 || height <= 0 ? "location_map_dimensions_unavailable"
                    : "native_furniture_location_restriction";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendFurnitureLegalRanges(ranges, y, width, x =>
                    {
                        var tileProbe = Furniture.GetFurnitureInstance(rotationProbe.ItemId);
                        for (var attempts = 0; tileProbe.currentRotation.Value != rotationProbe.currentRotation.Value && attempts < 4; attempts++)
                        {
                            tileProbe.rotate();
                        }
                        if (tileProbe.currentRotation.Value != rotationProbe.currentRotation.Value)
                        {
                            return default;
                        }
                        var tile = new Vector2(x, y);
                        tileProbe.InitializeAtTile(tile);
                        var legal = tileProbe.canBePlacedHere(
                            location, tile, ~(CollisionMask.Characters | CollisionMask.Farmers));
                        if (!legal)
                        {
                            return default;
                        }
                        var anchor = tileProbe.TileLocation;
                        var endpoint = ReadFurniturePlacementEndpoint(location, tileProbe);
                        return new FurnitureTileSignature(
                            true,
                            (int)anchor.X - x,
                            (int)anchor.Y - y,
                            tileProbe.getTilesWide(),
                            tileProbe.getTilesHigh(),
                            tileProbe.isPassable(),
                            endpoint.Kind,
                            endpoint.TableIndex,
                            endpoint.TableTileX,
                            endpoint.TableTileY);
                    }, ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_furniture_placement_probe_exception:" + ex.GetType().Name;
        }

        return new
        {
            inventory_rotation_before = inventoryRotation,
            rotation_steps_from_inventory = rotationSteps,
            desired_current_rotation = rotationProbe.currentRotation.Value,
            rotations = rotationProbe.rotations.Value,
            furniture_type = rotationProbe.furniture_type.Value,
            is_ground_furniture = rotationProbe.isGroundFurniture(),
            is_passable = rotationProbe.isPassable(),
            tiles_wide = rotationProbe.getTilesWide(),
            tiles_high = rotationProbe.getTilesHigh(),
            location_id = location.NameOrUniqueName,
            location_runtime_type = location.GetType().FullName,
            location_is_outdoors = location.IsOutdoors,
            can_free_place_furniture = location.CanFreePlaceFurniture(),
            placement_probe_status = status,
            static_legal_tile_count = legalCount,
            static_legal_tile_ranges = ranges.ToArray(),
            transient_occupancy_policy = "actors_excluded_from_static_projection;Utility.playerCanPlaceItemHere_rechecks_live_state",
            route_policy = location.CanFreePlaceFurniture()
                ? "native_remote_placement_from_current_reachable_player_tile"
                : "shared_collision_grid_cardinal_stand_and_rectangular_footprint_route_safety"
        };
    }

    private static FurnitureEndpoint ReadFurniturePlacementEndpoint(GameLocation location, Furniture probe)
    {
        var box = probe.GetBoundingBox();
        for (var index = 0; index < location.furniture.Count; index++)
        {
            var table = location.furniture[index];
            if (table.furniture_type.Value == 11 && table.heldObject.Value is null && table.GetBoundingBox().Intersects(box))
            {
                return new FurnitureEndpoint("table_held_object", index, (int)table.TileLocation.X, (int)table.TileLocation.Y);
            }
        }
        return new FurnitureEndpoint("location_furniture", -1, -1, -1);
    }

    private static void AppendFurnitureLegalRanges(
        ICollection<object> ranges, int y, int width, Func<int, FurnitureTileSignature> read, ref int legalCount)
    {
        int? start = null;
        var current = default(FurnitureTileSignature);
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
                    ranges.Add(FurnitureRange(y, start.Value, x - 1, current));
                    start = x;
                    current = next;
                }
            }
            else if (start.HasValue)
            {
                ranges.Add(FurnitureRange(y, start.Value, x - 1, current));
                start = null;
            }
        }
    }

    private static object FurnitureRange(int y, int startX, int endX, FurnitureTileSignature row) => new
    {
        y,
        start_x = startX,
        end_x = endX,
        anchor_offset_x = row.AnchorOffsetX,
        anchor_offset_y = row.AnchorOffsetY,
        footprint_width = row.Width,
        footprint_height = row.Height,
        expected_passable = row.Passable,
        placement_endpoint = row.Endpoint,
        table_index = row.TableIndex,
        table_tile_x = row.TableTileX,
        table_tile_y = row.TableTileY
    };

    private static Furniture? TryGetFurnitureFactory(string itemId)
    {
        try { return Furniture.GetFurnitureInstance(itemId); }
        catch { return null; }
    }

    private static bool IsVanillaFurnitureRuntimeType(Type type) =>
        type == typeof(Furniture) || type == typeof(StorageFurniture) || type == typeof(FishTankFurniture) ||
        type == typeof(BedFurniture) || type == typeof(RandomizedPlantFurniture) || type == typeof(TV);

    private static string FurnitureIdentityFingerprint(string kind, int index, Furniture item)
    {
        var storage = item as StorageFurniture;
        return kind + "|" + index + "|" + item.QualifiedItemId + "|" + item.Stack + "|" +
            item.GetType().FullName + "|" + item.currentRotation.Value + "|" + item.rotations.Value + "|" +
            item.TileLocation.X + "," + item.TileLocation.Y + "|" + item.heldObject.Value?.QualifiedItemId + "|" +
            (storage is null ? string.Empty : string.Join(",", storage.heldItems.Select(entry => entry?.QualifiedItemId + ":" + entry?.Stack)));
    }

    private sealed record InventoryFurnitureRef(Furniture Item, int SlotIndex);
    private sealed record FurnitureEndpoint(string Kind, int TableIndex, int TableTileX, int TableTileY);
    private readonly record struct FurnitureTileSignature(
        bool Legal, int AnchorOffsetX, int AnchorOffsetY, int Width, int Height, bool Passable,
        string Endpoint, int TableIndex, int TableTileX, int TableTileY);
}
