using Microsoft.Xna.Framework;
using System.Security.Cryptography;
using System.Text;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly object MachinePlacementCacheLock = new();
    private static string cachedMachinePlacementFingerprint = string.Empty;
    private static object? cachedMachinePlacementContext;

    private static object ReadMachinePlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new
            {
                projection_status = "unavailable_world_player_or_farm",
                inventory_machine_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var inventoryMachines = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item is StardewValley.Object machine &&
                machine.bigCraftable.Value &&
                machine.GetMachineData() is not null)
            .Select(entry => new InventoryMachineRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not ("machine" or "training_machine" or "full"))
        {
            return new
            {
                projection_status = "blocked_requires_machine_training_machine_or_full_profile",
                inventory_machine_count = inventoryMachines.Length,
                inventory_machine_slots = inventoryMachines.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locations = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var currentLocationId =
            Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var relocationLocations = locations
            .Where(location => string.Equals(
                location.Location.NameOrUniqueName,
                currentLocationId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var relocationMachines = relocationLocations
            .SelectMany(location => location.Location.objects.Pairs
                .Where(pair => pair.Value.bigCraftable.Value &&
                    pair.Value.GetMachineData() is not null &&
                    string.Equals(pair.Value.Type, "Crafting", StringComparison.Ordinal) &&
                    pair.Value.Fragility == 0 &&
                    (location.IsPlayerControlled ||
                     pair.Value.owner.Value == player.UniqueMultiplayerID))
                .Select(pair => pair.Value))
            .GroupBy(
                machine => machine.QualifiedItemId + "\n" +
                    (machine.GetType().FullName ?? machine.GetType().Name),
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var fingerprint = MachinePlacementFingerprint(
            inventoryMachines,
            relocationMachines,
            currentLocationId,
            locations);
        lock (MachinePlacementCacheLock)
        {
            if (cachedMachinePlacementContext is not null &&
                string.Equals(cachedMachinePlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedMachinePlacementContext;
            }
        }

        var rows = inventoryMachines
            .Select(machine => ReadMachinePlacementRow(
                machine.Machine,
                machine.SlotIndex,
                "inventory_machine",
                locations))
            .ToArray();
        var relocationRows = relocationMachines
            .Select(machine => ReadMachinePlacementRow(
                machine,
                -1,
                "placed_machine_relocation_probe",
                relocationLocations))
            .ToArray();
        var context = new
        {
            projection_status = "complete_inventory_and_relocation_machine_types_across_loaded_persistent_locations",
            inventory_machine_count = inventoryMachines.Length,
            relocation_machine_type_count = relocationRows.Length,
            relocation_location_id = currentLocationId,
            relocation_scope = "current_loaded_location_only_cross_location_relocation_pending",
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            location_scope = "Utility.ForEachLocation(includeInteriors:true,includeGenerated:false)_no_map_name_allowlist",
            planning_ownership_policy = "ownership_is_evidence_not_a_native_placement_gate",
            runtime_contract = "small_model_selects_location_and_tile_then_executor_routes_and_rechecks_Utility.playerCanPlaceItemHere_before_Object.placementAction",
            rows,
            relocation_rows = relocationRows
        };
        lock (MachinePlacementCacheLock)
        {
            cachedMachinePlacementFingerprint = fingerprint;
            cachedMachinePlacementContext = context;
        }
        return context;
    }

    private static object ReadMachinePlacementRow(
        StardewValley.Object machine,
        int inventorySlotIndex,
        string projectionRole,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var locationProjections = locations
            .Select(location => ReadMachinePlacementLocation(machine, location))
            .ToArray();
        var locationRows = locationProjections.Select(projection => projection.Row).ToArray();
        return new
        {
            projection_role = projectionRole,
            inventory_slot_index = inventorySlotIndex,
            item_id = machine.ItemId,
            qualified_item_id = machine.QualifiedItemId,
            display_name = machine.DisplayName,
            stack = machine.Stack,
            runtime_type = machine.GetType().FullName,
            is_cask = machine is Cask,
            location_count = locationRows.Length,
            static_legal_tile_count = locationProjections.Sum(projection => projection.StaticLegalTileCount),
            locations = locationRows
        };
    }

    private static MachinePlacementLocationProjection ReadMachinePlacementLocation(
        StardewValley.Object inventoryMachine,
        MachineLocationRef locationRef)
    {
        var location = locationRef.Location;
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var forbidden = Utility.isPlacementForbiddenHere(location);
        var staticRanges = new List<object>();
        var staticCount = 0;
        var operationalContext = true;
        string status;

        try
        {
            var probe = (StardewValley.Object)inventoryMachine.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            operationalContext = probe is not Cask cask || cask.IsValidCaskLocation();
            if (forbidden || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = forbidden
                    ? "native_location_placement_forbidden"
                    : !probe.isPlaceable()
                        ? "item_not_placeable"
                        : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendPlacementLegalRanges(
                        staticRanges,
                        y,
                        width,
                        x => probe.canBePlacedHere(
                            location,
                            new Vector2(x, y),
                            ~(CollisionMask.Characters | CollisionMask.Farmers)),
                        ref staticCount);
                }
                status = staticCount > 0
                    ? operationalContext
                        ? "native_legal_tiles_available"
                        : "placement_legal_but_machine_operation_invalid_in_location"
                    : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_placement_probe_exception:" + ex.GetType().Name;
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
            location_is_greenhouse = location.IsGreenhouse,
            map_width = width,
            map_height = height,
            native_location_placement_forbidden = forbidden,
            machine_operational_context_valid = operationalContext,
            placement_probe_status = status,
            static_legal_tile_count = staticCount,
            static_legal_tile_ranges = staticRanges.ToArray(),
            static_collision_mask = "CollisionMask.All_without_Characters_or_Farmers",
            transient_occupancy_policy = "characters_and_farmers_do_not_remove_layout_candidates;exact_current_occupancy_is_rechecked_after_route",
            route_and_time_owner = "small_model",
            runtime_recheck = "Utility.playerCanPlaceItemHere_at_exact_location_tile_after_route"
        };
        return new MachinePlacementLocationProjection(row, staticCount);
    }

    private static void AppendPlacementLegalRanges(
        ICollection<object> ranges,
        int y,
        int width,
        Func<int, bool> isLegal,
        ref int legalCount)
    {
        int? start = null;
        for (var x = 0; x <= width; x++)
        {
            var legal = x < width && isLegal(x);
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

    private sealed record InventoryMachineRef(StardewValley.Object Machine, int SlotIndex);

    private sealed record MachinePlacementLocationProjection(
        object Row,
        int StaticLegalTileCount);

    private static string MachinePlacementFingerprint(
        IReadOnlyList<InventoryMachineRef> inventoryMachines,
        IReadOnlyList<StardewValley.Object> relocationMachines,
        string relocationLocationId,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var inventoryRows = inventoryMachines.Select(machine =>
            "machine|" + machine.SlotIndex + "|" +
            machine.Machine.QualifiedItemId + "|" +
            machine.Machine.Stack + "|" +
            machine.Machine.GetType().FullName);
        var relocationRows = relocationMachines.Select(machine =>
            "relocation_machine_type|" + machine.QualifiedItemId + "|" +
            machine.GetType().FullName);
        return PersistentPlacementTopologyFingerprint(
            inventoryRows
                .Append("relocation_location|" + relocationLocationId)
                .Concat(relocationRows),
            locations);
    }

    private static string PersistentPlacementTopologyFingerprint(
        IEnumerable<string> inventoryRows,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var source = new StringBuilder();
        foreach (var row in inventoryRows)
        {
            source.AppendLine(row);
        }
        foreach (var locationRef in locations)
        {
            var location = locationRef.Location;
            source.Append("location|").Append(location.NameOrUniqueName).Append('|')
                .Append(location.map?.Id).Append('|')
                .Append(Utility.isPlacementForbiddenHere(location)).AppendLine();
            foreach (var pair in location.objects.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
            {
                source.Append("object|").Append((int)pair.Key.X).Append(',').Append((int)pair.Key.Y).Append('|')
                    .Append(pair.Value.QualifiedItemId).Append('|').Append(pair.Value.isPassable()).AppendLine();
            }
            foreach (var pair in location.terrainFeatures.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
            {
                source.Append("terrain|").Append((int)pair.Key.X).Append(',').Append((int)pair.Key.Y).Append('|')
                    .Append(pair.Value.GetType().FullName);
                if (pair.Value is HoeDirt dirt)
                {
                    source.Append('|').Append(dirt.crop?.indexOfHarvest.Value ?? string.Empty)
                        .Append('|').Append(dirt.crop?.currentPhase.Value ?? -1)
                        .Append('|').Append(dirt.crop?.dead.Value ?? false);
                }
                source.AppendLine();
            }
            foreach (var furniture in location.furniture.OrderBy(item => item.TileLocation.Y).ThenBy(item => item.TileLocation.X))
            {
                source.Append("furniture|").Append((int)furniture.TileLocation.X).Append(',')
                    .Append((int)furniture.TileLocation.Y).Append('|').Append(furniture.QualifiedItemId).AppendLine();
            }
            foreach (var feature in location.largeTerrainFeatures.OrderBy(item => item.Tile.Y).ThenBy(item => item.Tile.X))
            {
                source.Append("large_terrain|").Append((int)feature.Tile.X).Append(',').Append((int)feature.Tile.Y).Append('|')
                    .Append(feature.GetType().FullName).AppendLine();
            }
            foreach (var clump in location.resourceClumps.OrderBy(item => item.Tile.Y).ThenBy(item => item.Tile.X))
            {
                source.Append("resource_clump|").Append((int)clump.Tile.X).Append(',').Append((int)clump.Tile.Y).Append('|')
                    .Append(clump.parentSheetIndex.Value).Append('|').Append(clump.width.Value).Append('x').Append(clump.height.Value).AppendLine();
            }
            foreach (var building in location.buildings.OrderBy(item => item.tileY.Value).ThenBy(item => item.tileX.Value))
            {
                source.Append("building|").Append(building.tileX.Value).Append(',').Append(building.tileY.Value).Append('|')
                    .Append(building.GetType().FullName).Append('|').Append(building.buildingType.Value).AppendLine();
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()))).ToLowerInvariant();
    }
}
