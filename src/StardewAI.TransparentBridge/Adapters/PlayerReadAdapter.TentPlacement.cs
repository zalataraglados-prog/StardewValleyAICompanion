using Microsoft.Xna.Framework;
using StardewAI.Contracts.Execution;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string TentKitQualifiedItemId = "(O)TentKit";
    private static readonly object TentPlacementCacheLock = new();
    private static string cachedTentPlacementFingerprint = string.Empty;
    private static object? cachedTentPlacementContext;

    private static object ReadTentPlacementContext(Farmer? player)
    {
        if (player is null || Game1.currentLocation is not { } currentLocation || Game1.getFarm() is not { } farm)
        {
            return new { projection_status = "unavailable_world_player_or_current_location", inventory_tent_kit_count = 0, rows = Array.Empty<object>() };
        }

        var inventory = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                entry.item is StardewValley.Object item &&
                string.Equals(item.QualifiedItemId, TentKitQualifiedItemId, StringComparison.Ordinal))
            .Select(entry => new InventoryTentKitRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_tent_kit_count = inventory.Length,
                inventory_tent_kit_slots = inventory.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var persistent = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var currentRef = persistent.FirstOrDefault(row => ReferenceEquals(row.Location, currentLocation) ||
            string.Equals(row.Location.NameOrUniqueName, currentLocation.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
            ?? new MachineLocationRef(
                currentLocation,
                "current_generated_or_nonpersistent",
                false,
                currentLocation.GetRootLocation().NameOrUniqueName,
                currentLocation.ParentBuilding?.GetType().FullName ?? string.Empty);
        var tomorrow = ReadTentTomorrowBlock(currentLocation);
        var fingerprintRows = inventory.Select(row =>
                "tent_kit|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" + row.Item.Stack + "|" + row.Item.GetType().FullName)
            .Append("date|" + Game1.season + "|" + Game1.dayOfMonth)
            .Append("current_location|" + currentLocation.NameOrUniqueName)
            .Append("tomorrow_block|" + tomorrow.Blocked + "|" + tomorrow.FestivalId + "|" + tomorrow.BlockReason);
        var fingerprint = PersistentPlacementTopologyFingerprint(fingerprintRows, new[] { currentRef });
        lock (TentPlacementCacheLock)
        {
            if (cachedTentPlacementContext is not null &&
                string.Equals(cachedTentPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedTentPlacementContext;
            }
        }

        var location = ReadTentPlacementLocation(currentLocation, currentRef, tomorrow);
        var rows = inventory.Select(row => new
        {
            inventory_slot_index = row.SlotIndex,
            item_id = row.Item.ItemId,
            qualified_item_id = row.Item.QualifiedItemId,
            display_name = row.Item.DisplayName,
            stack = row.Item.Stack,
            inventory_runtime_type = row.Item.GetType().FullName,
            exact_base_object = row.Item.GetType() == typeof(StardewValley.Object),
            placed_runtime_type = typeof(Tent).FullName,
            placed_initial_health = 5,
            locations = new[] { location }
        }).ToArray();
        var context = new
        {
            schema_version = "tent_placement.v1",
            projection_status = "complete_exact_inventory_tent_kits_for_current_loaded_location",
            inventory_tent_kit_count = inventory.Length,
            location_count = 1,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            source_qualified_item_id = TentKitQualifiedItemId,
            source_runtime_type = typeof(StardewValley.Object).FullName,
            placed_runtime_type = typeof(Tent).FullName,
            footprint_width = TentPlacementGeometryResolver.RectangleWidth,
            footprint_height = TentPlacementGeometryResolver.RectangleHeight,
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)TentKit)->largeTerrainFeatures.Add(Tent(rectangle.X+1,rectangle.Y+1))",
            direction_contract = "Utility.getDirectionFromChange(target_tile,player_tile)->native_3x2_rectangle",
            lifetime_contract = "Tent.dayUpdate->health=0->tickUpdate->onDestroy;placement_and_sleep_are_distinct_actions",
            sleep_handoff_contract = "Tent.performUseAction->SleepTent_yes_no;recovery.sleep_in_tent remains a separate semantic action",
            passability_contract = "Tent.isPassable(Character c)=c!=null",
            layout_policy_owner = "small_model",
            route_safety_owner = "shared_collision_grid_passable_rectangular_footprint_bfs",
            rows
        };
        lock (TentPlacementCacheLock)
        {
            cachedTentPlacementFingerprint = fingerprint;
            cachedTentPlacementContext = context;
        }
        return context;
    }

    private static object ReadTentPlacementLocation(
        GameLocation location,
        MachineLocationRef locationRef,
        TentTomorrowBlock tomorrow)
    {
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var directionRows = new List<object>();
        var legalCount = 0;
        for (var direction = 0; direction < 4; direction++)
        {
            var ranges = new List<object>();
            var directionCount = 0;
            for (var y = 0; y < height; y++)
            {
                AppendPlacementLegalRanges(
                    ranges,
                    y,
                    width,
                    x =>
                    {
                        var geometry = TentPlacementGeometryResolver.ResolveFromStand(x, y, direction);
                        return !tomorrow.Blocked && location.IsOutdoors &&
                            location.isAreaClear(new Rectangle(
                                geometry.RectangleX,
                                geometry.RectangleY,
                                geometry.RectangleWidth,
                                geometry.RectangleHeight));
                    },
                    ref directionCount);
            }
            legalCount += directionCount;
            var sample = TentPlacementGeometryResolver.ResolveFromStand(0, 0, direction);
            directionRows.Add(new
            {
                direction,
                direction_name = sample.DirectionName,
                target_delta_x = sample.TargetTileX,
                target_delta_y = sample.TargetTileY,
                rectangle_offset_x_from_stand = sample.RectangleX,
                rectangle_offset_y_from_stand = sample.RectangleY,
                anchor_offset_x_from_stand = sample.AnchorTileX,
                anchor_offset_y_from_stand = sample.AnchorTileY,
                static_legal_stand_tile_count = directionCount,
                static_legal_stand_tile_ranges = ranges.ToArray()
            });
        }

        var status = !location.IsOutdoors
            ? "native_requires_outdoors"
            : tomorrow.Blocked
                ? "native_tomorrow_festival_blocked"
                : width <= 0 || height <= 0
                    ? "location_map_dimensions_unavailable"
                    : legalCount > 0 ? "native_legal_directional_stands_available" : "no_native_legal_directional_stand";
        return new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_context_id = location.GetLocationContextId(),
            location_runtime_type = location.GetType().FullName,
            location_kind = locationRef.Kind,
            root_location_id = locationRef.RootLocationId,
            parent_building_runtime_type = locationRef.ParentBuildingRuntimeType,
            location_is_current = ReferenceEquals(location, Game1.currentLocation),
            location_is_outdoors = location.IsOutdoors,
            map_width = width,
            map_height = height,
            tomorrow_season = tomorrow.Season,
            tomorrow_day = tomorrow.Day,
            tomorrow_festival_blocked = tomorrow.Blocked,
            tomorrow_festival_id = tomorrow.FestivalId,
            tomorrow_festival_block_reason = tomorrow.BlockReason,
            placement_probe_status = status,
            static_legal_directional_stand_count = legalCount,
            direction_rows = directionRows.ToArray(),
            transient_occupancy_policy = "native_isAreaClear_rechecked_after_shared_route",
            runtime_recheck = "Utility.playerCanPlaceItemHere_then_Object.placementAction_exact_directional_rectangle"
        };
    }

    private static TentTomorrowBlock ReadTentTomorrowBlock(GameLocation location)
    {
        var day = (Game1.dayOfMonth + 1) % 28;
        var season = Game1.dayOfMonth == 28
            ? (Season)((int)(Game1.season + 1) % 4)
            : Game1.season;
        if (Utility.isFestivalDay(day, season, location.GetLocationContextId()))
        {
            return new TentTomorrowBlock(season.ToString(), day, true, string.Empty, "active_festival_for_location_context");
        }

        if (!Utility.TryGetPassiveFestivalDataForDay(day, season, null, out var id, out var data) || data is null)
        {
            return new TentTomorrowBlock(season.ToString(), day, false, string.Empty, "none");
        }
        var mapReplacement = data.MapReplacements?.Keys.Any(key => string.Equals(key, location.Name, StringComparison.Ordinal)) == true;
        var durationBlocked = ((string.Equals(id, "TroutDerby", StringComparison.Ordinal) && string.Equals(location.Name, "Forest", StringComparison.Ordinal)) ||
                (string.Equals(id, "SquidFest", StringComparison.Ordinal) && string.Equals(location.Name, "Beach", StringComparison.Ordinal))) &&
            data.StartDay > Game1.dayOfMonth;
        return new TentTomorrowBlock(
            season.ToString(),
            day,
            mapReplacement || durationBlocked,
            id ?? string.Empty,
            mapReplacement ? "passive_festival_map_replacement" : durationBlocked ? "passive_festival_multi_day_window" : "none");
    }

    private sealed record InventoryTentKitRef(StardewValley.Object Item, int SlotIndex);
    private sealed record TentTomorrowBlock(string Season, int Day, bool Blocked, string FestivalId, string BlockReason);
}
