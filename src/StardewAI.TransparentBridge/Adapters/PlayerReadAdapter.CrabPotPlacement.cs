using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string CrabPotQualifiedItemId = "(O)710";
    private static readonly object CrabPotPlacementCacheLock = new();
    private static string cachedCrabPotPlacementFingerprint = string.Empty;
    private static object? cachedCrabPotPlacementContext;

    private static object ReadCrabPotPlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new
            {
                projection_status = "unavailable_world_player_or_farm",
                inventory_crab_pot_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var inventoryPots = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item is StardewValley.Object item &&
                string.Equals(item.QualifiedItemId, CrabPotQualifiedItemId, StringComparison.Ordinal))
            .Select(entry => new InventoryCrabPotRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_crab_pot_count = inventoryPots.Length,
                inventory_crab_pot_slots = inventoryPots.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locations = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var hasMariner = player.professions.Contains(10);
        var hasLuremaster = player.professions.Contains(11);
        var fingerprint = PersistentPlacementTopologyFingerprint(
            inventoryPots.Select(row =>
                    "crab_pot|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" +
                    row.Item.Stack + "|" + row.Item.GetType().FullName)
                .Append("current_location|" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty))
                .Append("player|" + player.UniqueMultiplayerID + "|mariner=" + hasMariner + "|luremaster=" + hasLuremaster),
            locations);
        lock (CrabPotPlacementCacheLock)
        {
            if (cachedCrabPotPlacementContext is not null &&
                string.Equals(cachedCrabPotPlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedCrabPotPlacementContext;
            }
        }

        var fishData = DataLoader.Fish(Game1.content);
        var rows = inventoryPots
            .Select(row => ReadCrabPotPlacementRow(row, locations, player, fishData))
            .ToArray();
        var context = new
        {
            schema_version = "crab_pot_placement.v1",
            projection_status = "complete_inventory_crab_pots_across_loaded_persistent_locations",
            inventory_crab_pot_count = inventoryPots.Length,
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            qualified_item_id = CrabPotQualifiedItemId,
            placed_qualified_item_id = CrabPotQualifiedItemId,
            placed_runtime_type = typeof(CrabPot).FullName,
            owner_player_id = player.UniqueMultiplayerID,
            owner_has_mariner = hasMariner,
            owner_has_luremaster = hasLuremaster,
            initial_needs_bait = !hasLuremaster,
            initial_bait_qualified_item_id = string.Empty,
            initial_ready_for_harvest = false,
            initial_output_qualified_item_id = string.Empty,
            layout_policy_owner = "small_model",
            route_safety_owner = "shared_collision_grid_and_adjacent_pathing",
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)710)->CrabPot.placementAction(owner=current_player)",
            production_contract = "CrabPot.DayUpdate(owner professions, bait, fish area, Data/Fish trap order)->heldObject",
            bait_effect_contract = "Luremaster(11)=no_bait_required;Mariner(10)=no_trash;normal/deluxe/wild/specific_bait_modify_native_DayUpdate",
            rows
        };
        lock (CrabPotPlacementCacheLock)
        {
            cachedCrabPotPlacementFingerprint = fingerprint;
            cachedCrabPotPlacementContext = context;
        }
        return context;
    }

    private static object ReadCrabPotPlacementRow(
        InventoryCrabPotRef inventoryPot,
        IReadOnlyList<MachineLocationRef> locations,
        Farmer player,
        IDictionary<string, string> fishData)
    {
        var projections = locations
            .Select(location => ReadCrabPotPlacementLocation(inventoryPot.Item, location, player, fishData))
            .ToArray();
        return new
        {
            inventory_slot_index = inventoryPot.SlotIndex,
            item_id = inventoryPot.Item.ItemId,
            qualified_item_id = inventoryPot.Item.QualifiedItemId,
            display_name = inventoryPot.Item.DisplayName,
            stack = inventoryPot.Item.Stack,
            inventory_runtime_type = inventoryPot.Item.GetType().FullName,
            placed_runtime_type = typeof(CrabPot).FullName,
            location_count = projections.Length,
            static_legal_tile_count = projections.Sum(row => row.StaticLegalTileCount),
            locations = projections.Select(row => row.Row).ToArray()
        };
    }

    private static CrabPotPlacementLocationProjection ReadCrabPotPlacementLocation(
        StardewValley.Object inventoryPot,
        MachineLocationRef locationRef,
        Farmer player,
        IDictionary<string, string> fishData)
    {
        var location = locationRef.Location;
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var forbidden = Utility.isPlacementForbiddenHere(location);
        var nativeLocationExcluded = location is Caldera or VolcanoDungeon or MineShaft;
        var ranges = new List<object>();
        var legalCount = 0;
        string status;
        try
        {
            var probe = (StardewValley.Object)inventoryPot.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            if (forbidden || nativeLocationExcluded || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = forbidden
                    ? "native_location_placement_forbidden"
                    : nativeLocationExcluded
                        ? "native_crab_pot_location_type_excluded"
                        : !probe.isPlaceable()
                            ? "crab_pot_not_placeable"
                            : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendCrabPotPlacementRanges(ranges, y, width, location, probe, player, fishData, ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_water_tiles_available" : "no_native_legal_water_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_crab_pot_placement_probe_exception:" + ex.GetType().Name;
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
            native_location_type_excluded = nativeLocationExcluded,
            placement_probe_status = status,
            static_legal_tile_count = legalCount,
            static_legal_tile_ranges = ranges.ToArray(),
            native_legality = "CrabPot.IsValidCrabPotLocationTile:water_target_and_axis_water_neighbors_and_no_object_and_no_Buildings_Passable_property",
            excluded_location_types = new[] { typeof(Caldera).FullName, typeof(VolcanoDungeon).FullName, typeof(MineShaft).FullName },
            transient_occupancy_policy = "actors_do_not_remove_water_layout_candidates;runtime_rechecks_exact_tile",
            route_and_time_owner = "small_model",
            runtime_recheck = "Utility.playerCanPlaceItemHere_at_exact_loaded_location_water_tile"
        };
        return new CrabPotPlacementLocationProjection(row, legalCount);
    }

    private static void AppendCrabPotPlacementRanges(
        ICollection<object> ranges,
        int y,
        int width,
        GameLocation location,
        StardewValley.Object probe,
        Farmer player,
        IDictionary<string, string> fishData,
        ref int legalCount)
    {
        int? start = null;
        CrabPotTileProductionContext? active = null;
        for (var x = 0; x <= width; x++)
        {
            var context = x < width && probe.canBePlacedHere(
                    location,
                    new Vector2(x, y),
                    ~(CollisionMask.Characters | CollisionMask.Farmers))
                ? ReadCrabPotTileProductionContext(location, x, y, player, fishData)
                : null;
            if (context is not null)
            {
                legalCount++;
            }
            if (context is not null && active is not null && string.Equals(context.Signature, active.Signature, StringComparison.Ordinal))
            {
                continue;
            }
            if (start.HasValue && active is not null)
            {
                ranges.Add(new
                {
                    y,
                    start_x = start.Value,
                    end_x = x - 1,
                    production_signature = active.Signature,
                    fish_area_id = active.FishAreaId,
                    fish_area_display_name = active.FishAreaDisplayName,
                    crab_pot_habitat_tags = active.HabitatTags,
                    base_junk_chance = active.BaseJunkChance,
                    owner_effective_junk_chance = active.OwnerEffectiveJunkChance,
                    catch_selection_mode = active.CatchSelectionMode,
                    native_order_catch_rows = active.CatchRows,
                    fallback_trash_qualified_item_ids = new[] { "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" }
                });
            }
            start = context is null ? null : x;
            active = context;
        }
    }

    private static CrabPotTileProductionContext ReadCrabPotTileProductionContext(
        GameLocation location,
        int x,
        int y,
        Farmer player,
        IDictionary<string, string> fishData)
    {
        var tile = new Vector2(x, y);
        var hasArea = location.TryGetFishAreaForTile(tile, out var areaId, out var areaData);
        var habitats = location.GetCrabPotFishForTile(tile).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var catchRows = fishData
            .Where(pair => pair.Value.Contains("trap", StringComparison.Ordinal))
            .Select((pair, nativeIndex) => ReadCrabPotCatchRow(pair.Key, pair.Value, nativeIndex, habitats))
            .Where(row => row is not null)
            .Cast<CrabPotCatchRow>()
            .ToArray();
        var baseJunkChance = hasArea && areaData is not null ? areaData.CrabPotJunkChance : 0.2;
        var selectionMode = player.professions.Contains(10)
            ? "mariner_uniform_over_native_eligible_rows"
            : "native_order_independent_chance_until_first_success_then_trash";
        var signature = (hasArea ? areaId : string.Empty) + "|" +
            baseJunkChance.ToString("R", CultureInfo.InvariantCulture) + "|" +
            string.Join(",", habitats) + "|" + string.Join(",", catchRows.Select(row => row.ItemId));
        return new CrabPotTileProductionContext(
            signature,
            hasArea ? areaId : string.Empty,
            hasArea ? location.GetFishingAreaDisplayName(areaId) : string.Empty,
            habitats,
            baseJunkChance,
            player.professions.Contains(10) ? 0 : baseJunkChance,
            selectionMode,
            catchRows);
    }

    private static CrabPotCatchRow? ReadCrabPotCatchRow(string itemId, string data, int nativeIndex, string[] habitats)
    {
        var fields = data.Split('/');
        if (fields.Length <= 4)
        {
            return null;
        }
        var fishHabitats = ArgUtility.SplitBySpace(fields[4]);
        if (!fishHabitats.Any(tag => habitats.Contains(tag, StringComparer.Ordinal)))
        {
            return null;
        }
        var chance = fields.Length > 2 && double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        return new CrabPotCatchRow(nativeIndex, itemId, "(O)" + itemId, chance, fishHabitats);
    }

    private sealed record InventoryCrabPotRef(StardewValley.Object Item, int SlotIndex);
    private sealed record CrabPotPlacementLocationProjection(object Row, int StaticLegalTileCount);
    private sealed record CrabPotCatchRow(int NativeOrder, string ItemId, string QualifiedItemId, double BaseChance, string[] HabitatTags);
    private sealed record CrabPotTileProductionContext(
        string Signature,
        string FishAreaId,
        string FishAreaDisplayName,
        string[] HabitatTags,
        double BaseJunkChance,
        double OwnerEffectiveJunkChance,
        string CatchSelectionMode,
        CrabPotCatchRow[] CatchRows);
}
