using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string SignPlacementNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(sign_item_or_TextSign)->location.objects";
    private static readonly object SignPlacementCacheLock = new();
    private static string cachedSignPlacementFingerprint = string.Empty;
    private static object? cachedSignPlacementContext;

    private static object ReadSignPlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new { projection_status = "unavailable_world_player_or_farm", inventory_sign_count = 0, rows = Array.Empty<object>() };
        }

        var catalog = Game1.bigCraftableData
            .Where(pair => IsSignCatalogEntry(pair.Key, pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => ReadSignCatalogEntry(pair.Key, pair.Value))
            .ToArray();
        var inventorySigns = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                entry.item is StardewValley.Object item && SignPlacementKind(item) is not null)
            .Select(entry => new InventorySignRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_sign_count = inventorySigns.Length,
                inventory_sign_slots = inventorySigns.Select(row => row.SlotIndex).ToArray(),
                sign_catalog_count = catalog.Length,
                sign_catalog = catalog,
                rows = Array.Empty<object>()
            };
        }

        var persistent = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var current = Game1.currentLocation;
        var currentRef = persistent.FirstOrDefault(row => ReferenceEquals(row.Location, current) ||
            string.Equals(row.Location.NameOrUniqueName, current?.NameOrUniqueName, StringComparison.OrdinalIgnoreCase));
        if (currentRef is null && current is not null)
        {
            currentRef = new MachineLocationRef(current, "current_generated_or_nonpersistent", false,
                current.GetRootLocation().NameOrUniqueName, current.ParentBuilding?.GetType().FullName ?? string.Empty);
        }
        var locations = currentRef is null ? Array.Empty<MachineLocationRef>() : new[] { currentRef };
        var fingerprintRows = inventorySigns.Select(row =>
                "sign|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" + row.Item.Stack + "|" +
                row.Item.GetType().FullName + "|" + SignPlacementKind(row.Item))
            .Concat(Game1.bigCraftableData.Where(pair => IsSignCatalogEntry(pair.Key, pair.Value))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => "sign_data|" + pair.Key + "|" + JsonSerializer.Serialize(pair.Value)))
            .Concat(locations.SelectMany(location => location.Location.objects.Pairs
                .Where(pair => pair.Value is Sign || pair.Value.IsTextSign())
                .OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X)
                .Select(pair => "sign_topology|" + location.Location.NameOrUniqueName + "|" +
                    (int)pair.Key.X + "," + (int)pair.Key.Y + "|" + pair.Value.QualifiedItemId + "|" +
                    pair.Value.GetType().FullName + "|" + ReadPlacedSignPayloadSignature(pair.Value))))
            .Append("current_location|" + (current?.NameOrUniqueName ?? string.Empty));
        var fingerprint = PersistentPlacementTopologyFingerprint(fingerprintRows, locations);
        lock (SignPlacementCacheLock)
        {
            if (cachedSignPlacementContext is not null &&
                string.Equals(cachedSignPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedSignPlacementContext;
            }
        }

        var rows = inventorySigns.Select(row => ReadSignPlacementRow(row, locations)).ToArray();
        var context = new
        {
            schema_version = "sign_placement.v1",
            projection_status = "complete_live_sign_catalog_and_current_loaded_location",
            inventory_sign_count = inventorySigns.Length,
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            source_runtime_type = typeof(StardewValley.Object).FullName,
            sign_data_source = "live Data/BigCraftables context tag sign_item plus exact (BC)TextSign",
            native_runtime_contract = SignPlacementNativeContract,
            placement_scope = "current_loaded_location_only_rebind_after_arrival",
            payload_policy = "placement_creates_an_empty_sign;display_item_assignment_and_text_editing_are_separate_native_actions",
            route_safety_owner = "shared_collision_grid_virtual_occupancy_bfs_and_protected_access",
            sign_catalog_count = catalog.Length,
            sign_catalog = catalog,
            rows
        };
        lock (SignPlacementCacheLock)
        {
            cachedSignPlacementFingerprint = fingerprint;
            cachedSignPlacementContext = context;
        }
        return context;
    }

    private static object ReadSignCatalogEntry(string itemId, BigCraftableData data)
    {
        var qualifiedItemId = "(BC)" + itemId;
        var probe = ItemRegistry.Create<StardewValley.Object>(qualifiedItemId);
        var kind = SignPlacementKind(probe) ?? "unsupported";
        return new
        {
            item_id = itemId,
            qualified_item_id = qualifiedItemId,
            display_name = probe.DisplayName,
            placement_kind = kind,
            inventory_runtime_type = probe.GetType().FullName,
            expected_placed_runtime_type = kind == "display_item_sign" ? typeof(Sign).FullName : typeof(StardewValley.Object).FullName,
            is_placeable = probe.isPlaceable(),
            is_passable = probe.isPassable(),
            context_tags = probe.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
            raw_data = data
        };
    }

    private static object ReadSignPlacementRow(InventorySignRef inventory, IReadOnlyList<MachineLocationRef> locations)
    {
        var kind = SignPlacementKind(inventory.Item)!;
        var expectedType = kind == "display_item_sign" ? typeof(Sign).FullName : typeof(StardewValley.Object).FullName;
        var locationRows = locations.Select(location => ReadSignPlacementLocation(inventory.Item, location)).ToArray();
        return new
        {
            inventory_slot_index = inventory.SlotIndex,
            item_id = inventory.Item.ItemId,
            qualified_item_id = inventory.Item.QualifiedItemId,
            display_name = inventory.Item.DisplayName,
            stack = inventory.Item.Stack,
            inventory_runtime_type = inventory.Item.GetType().FullName,
            placement_kind = kind,
            expected_placed_runtime_type = expectedType,
            expected_passable = false,
            expected_display_item_empty = true,
            expected_display_type = 0,
            expected_sign_text = string.Empty,
            expected_show_next_index = kind == "text_sign",
            native_contract = SignPlacementNativeContract,
            location_count = locationRows.Length,
            static_legal_tile_count = locationRows.Sum(row => row.StaticLegalTileCount),
            locations = locationRows.Select(row => row.Row).ToArray()
        };
    }

    private static SignPlacementLocationProjection ReadSignPlacementLocation(
        StardewValley.Object source,
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
            var probe = ItemRegistry.Create<StardewValley.Object>(source.QualifiedItemId);
            if (probe.GetType() != typeof(StardewValley.Object) || SignPlacementKind(probe) is null || forbidden ||
                !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = probe.GetType() != typeof(StardewValley.Object)
                    ? "custom_inventory_runtime_type_blocked"
                    : SignPlacementKind(probe) is null
                        ? "native_sign_branch_unavailable"
                        : forbidden
                            ? "native_location_placement_forbidden"
                            : !probe.isPlaceable()
                                ? "sign_item_not_placeable"
                                : "location_map_dimensions_unavailable";
            }
            else
            {
                probe.Location = location;
                for (var y = 0; y < height; y++)
                {
                    int? start = null;
                    for (var x = 0; x <= width; x++)
                    {
                        var legal = x < width && probe.canBePlacedHere(location, new Vector2(x, y),
                            ~(CollisionMask.Characters | CollisionMask.Farmers));
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
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_sign_placement_probe_exception:" + ex.GetType().Name;
        }

        return new SignPlacementLocationProjection(new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_runtime_type = location.GetType().FullName,
            location_kind = locationRef.Kind,
            root_location_id = locationRef.RootLocationId,
            parent_building_runtime_type = locationRef.ParentBuildingRuntimeType,
            location_is_current = ReferenceEquals(location, Game1.currentLocation),
            location_is_outdoors = location.IsOutdoors,
            map_width = width,
            map_height = height,
            native_location_placement_forbidden = forbidden,
            placement_probe_status = status,
            static_legal_tile_count = legalCount,
            static_legal_tile_ranges = ranges.ToArray(),
            static_collision_mask = "CollisionMask.All_without_Characters_or_Farmers",
            expected_nonpassable_single_tile = true,
            runtime_recheck = "Utility.playerCanPlaceItemHere_then_Utility.tryToPlaceItem_at_exact_loaded_tile"
        }, legalCount);
    }

    private static bool IsSignCatalogEntry(string itemId, BigCraftableData data) =>
        string.Equals(itemId, "TextSign", StringComparison.Ordinal) ||
        data.ContextTags?.Contains("sign_item", StringComparer.OrdinalIgnoreCase) == true;

    private static string? SignPlacementKind(StardewValley.Object item)
    {
        if (!item.bigCraftable.Value)
        {
            return null;
        }
        if (item.HasContextTag("sign_item"))
        {
            return "display_item_sign";
        }
        return item.IsTextSign() ? "text_sign" : null;
    }

    private static string ReadPlacedSignPayloadSignature(StardewValley.Object item) => item switch
    {
        Sign sign => "display|" + sign.displayType.Value + "|" + (sign.displayItem.Value?.QualifiedItemId ?? string.Empty),
        _ when item.IsTextSign() => "text|" + (item.SignText ?? string.Empty) + "|" + item.showNextIndex.Value,
        _ => "not_sign"
    };

    private sealed record InventorySignRef(StardewValley.Object Item, int SlotIndex);
    private sealed record SignPlacementLocationProjection(object Row, int StaticLegalTileCount);
}
