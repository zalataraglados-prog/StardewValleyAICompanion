using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string FireworkNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)893|(O)894|(O)895)->broadcastSprites+netAudio(fuse)+DelayedAction.StopPlaying(fuse)";
    private const string FireworkRandomContract = "live_Game1.random_runtime_only_no_read_side_rng_advance";
    private static readonly object FireworkPlacementCacheLock = new();
    private static string cachedFireworkPlacementFingerprint = string.Empty;
    private static object? cachedFireworkPlacementContext;

    private static object ReadFireworkPlacementContext(Farmer? player)
    {
        if (player is null || Game1.currentLocation is not { } location)
        {
            return new { projection_status = "unavailable_world_player_or_location", inventory_firework_count = 0, rows = Array.Empty<object>() };
        }

        var inventory = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                entry.item is StardewValley.Object obj && TryFireworkType(obj.QualifiedItemId, out _))
            .Select(entry => new InventoryFireworkRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_firework_count = inventory.Length,
                inventory_firework_slots = inventory.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locationRef = new MachineLocationRef(
            location,
            "current_loaded_location",
            ReferenceEquals(location, player.currentLocation),
            location.GetRootLocation().NameOrUniqueName,
            location.ParentBuilding?.GetType().FullName ?? string.Empty);
        var transientTiles = ExactTemporarySpriteTiles(location);
        var fingerprintRows = inventory.Select(row =>
                "firework|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" + row.Item.Stack + "|" + row.Item.GetType().FullName)
            .Append("current_location|" + location.NameOrUniqueName)
            .Concat(transientTiles.Select(tile => "temporary_sprite|" + tile.X + "|" + tile.Y));
        var fingerprint = Sha256(PersistentPlacementTopologyFingerprint(fingerprintRows, new[] { locationRef }));
        lock (FireworkPlacementCacheLock)
        {
            if (cachedFireworkPlacementContext is not null &&
                string.Equals(cachedFireworkPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedFireworkPlacementContext;
            }
        }

        var rows = inventory.Select(row => ReadFireworkPlacementRow(row, locationRef, transientTiles)).ToArray();
        var context = new
        {
            schema_version = "firework_placement.v1",
            projection_status = "complete_inventory_fireworks_for_current_loaded_location",
            inventory_firework_count = inventory.Length,
            location_count = 1,
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            supported_variants = new[]
            {
                new { item_id = "893", qualified_item_id = "(O)893", firework_type = 0, source_rect_x = 256 },
                new { item_id = "894", qualified_item_id = "(O)894", firework_type = 1, source_rect_x = 272 },
                new { item_id = "895", qualified_item_id = "(O)895", firework_type = 2, source_rect_x = 288 }
            },
            random_outcome_contract = FireworkRandomContract,
            native_runtime_contract = FireworkNativeContract,
            invocation_policy = "player_command_only",
            autonomous_candidate_enabled = false,
            rows
        };
        lock (FireworkPlacementCacheLock)
        {
            cachedFireworkPlacementFingerprint = fingerprint;
            cachedFireworkPlacementContext = context;
        }
        return context;
    }

    private static object ReadFireworkPlacementRow(
        InventoryFireworkRef inventory, MachineLocationRef locationRef, Point[] transientTiles)
    {
        TryFireworkType(inventory.Item.QualifiedItemId, out var fireworkType);
        return new
        {
            inventory_slot_index = inventory.SlotIndex,
            item_id = inventory.Item.ItemId,
            qualified_item_id = inventory.Item.QualifiedItemId,
            display_name = inventory.Item.DisplayName,
            inventory_runtime_type = inventory.Item.GetType().FullName,
            stack_before = inventory.Item.Stack,
            stack_after = Math.Max(0, inventory.Item.Stack - 1),
            firework_type = fireworkType,
            source_rect_x = 256 + fireworkType * 16,
            source_rect_y = 397,
            source_rect_width = 16,
            source_rect_height = 16,
            fuse_duration_ms = 2400,
            rocket_delay_ms = 2400,
            rocket_id_min = 20,
            rocket_id_max = 30,
            acceleration_y_min = -0.36,
            acceleration_y_max = -0.27,
            acceleration_y_step = 0.01,
            random_outcome_contract = FireworkRandomContract,
            native_contract = FireworkNativeContract,
            locations = new[] { ReadFireworkPlacementLocation(inventory.Item, locationRef, transientTiles) }
        };
    }

    private static object ReadFireworkPlacementLocation(
        StardewValley.Object inventoryFirework, MachineLocationRef locationRef, Point[] transientTiles)
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
            var probe = (StardewValley.Object)inventoryFirework.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            if (probe.GetType() != typeof(StardewValley.Object) || forbidden || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = probe.GetType() != typeof(StardewValley.Object)
                    ? "custom_inventory_runtime_type_blocked"
                    : forbidden ? "native_location_placement_forbidden"
                    : !probe.isPlaceable() ? "firework_not_placeable"
                    : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendGrassLegalRanges(ranges, y, width, x =>
                        probe.canBePlacedHere(location, new Vector2(x, y), ~(CollisionMask.Characters | CollisionMask.Farmers)),
                        ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_firework_placement_probe_exception:" + ex.GetType().Name;
        }

        return new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_runtime_type = location.GetType().FullName,
            location_kind = locationRef.Kind,
            root_location_id = locationRef.RootLocationId,
            parent_building_runtime_type = locationRef.ParentBuildingRuntimeType,
            location_is_player_controlled = locationRef.IsPlayerControlled,
            location_is_current = ReferenceEquals(location, Game1.currentLocation),
            map_width = width,
            map_height = height,
            native_location_placement_forbidden = forbidden,
            placement_probe_status = status,
            static_legal_tile_count = legalCount,
            static_legal_tile_ranges = ranges.ToArray(),
            temporary_sprite_blocked_tiles = transientTiles.Select(tile => new { tile_x = tile.X, tile_y = tile.Y }).ToArray(),
            temporary_sprite_collision_policy = "Object.placementAction rejects any sprite whose position exactly equals target_tile_times_64",
            persistent_world_effect = false,
            runtime_recheck = "Utility.playerCanPlaceItemHere_then_exact_live_temporary_sprite_position_absence"
        };
    }

    private static Point[] ExactTemporarySpriteTiles(GameLocation location) => location.temporarySprites
        .Where(sprite => sprite.position.X % Game1.tileSize == 0f && sprite.position.Y % Game1.tileSize == 0f)
        .Select(sprite => new Point((int)(sprite.position.X / Game1.tileSize), (int)(sprite.position.Y / Game1.tileSize)))
        .Distinct()
        .OrderBy(tile => tile.Y)
        .ThenBy(tile => tile.X)
        .ToArray();

    private static bool TryFireworkType(string qualifiedItemId, out int fireworkType)
    {
        fireworkType = qualifiedItemId switch { "(O)893" => 0, "(O)894" => 1, "(O)895" => 2, _ => -1 };
        return fireworkType >= 0;
    }

    private sealed record InventoryFireworkRef(StardewValley.Object Item, int SlotIndex);
}
