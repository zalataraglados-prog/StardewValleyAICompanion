using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.GameData.Fences;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly object FencePlacementCacheLock = new();
    private static readonly HashSet<int> FunctionalGateDrawSums = new() { 10, 100, 500, 1000, 110, 1500 };
    private static string cachedFencePlacementFingerprint = string.Empty;
    private static object? cachedFencePlacementContext;

    private static object ReadFencePlacementContext(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new
            {
                projection_status = "unavailable_world_player_or_farm",
                inventory_fence_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var lookup = Fence.GetFenceLookup();
        var inventoryFences = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                entry.item is StardewValley.Object item && item.IsFenceItem() &&
                lookup.ContainsKey(item.ItemId))
            .Select(entry => new InventoryFenceRef((StardewValley.Object)entry.item!, entry.slot))
            .ToArray();
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                inventory_fence_count = inventoryFences.Length,
                inventory_fence_slots = inventoryFences.Select(row => row.SlotIndex).ToArray(),
                rows = Array.Empty<object>()
            };
        }

        var locations = MachineLocationTopology.ReadPersistentLocations(farm, player);
        var fingerprintRows = inventoryFences.Select(row =>
                "fence|" + row.SlotIndex + "|" + row.Item.QualifiedItemId + "|" +
                row.Item.Stack + "|" + row.Item.GetType().FullName)
            .Concat(locations.SelectMany(location => location.Location.objects.Pairs
                .Where(pair => pair.Value is Fence)
                .OrderBy(pair => pair.Key.Y)
                .ThenBy(pair => pair.Key.X)
                .Select(pair =>
                {
                    var fence = (Fence)pair.Value;
                    return "fence_topology|" + location.Location.NameOrUniqueName + "|" +
                        (int)pair.Key.X + "," + (int)pair.Key.Y + "|" + fence.ItemId + "|" +
                        fence.isGate.Value + "|" + fence.health.Value + "|" +
                        fence.repairQueued.Value + "|" + fence.gatePosition.Value;
                })))
            .Append("current_location|" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty));
        var fingerprint = PersistentPlacementTopologyFingerprint(fingerprintRows, locations);
        lock (FencePlacementCacheLock)
        {
            if (cachedFencePlacementContext is not null &&
                string.Equals(cachedFencePlacementFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cachedFencePlacementContext;
            }
        }

        var rows = inventoryFences
            .Select(row => ReadFencePlacementRow(row, lookup, locations))
            .ToArray();
        var context = new
        {
            schema_version = "fence_placement.v1",
            projection_status = "complete_inventory_fences_across_loaded_persistent_locations",
            inventory_fence_count = inventoryFences.Length,
            location_count = locations.Length,
            static_projection_fingerprint = fingerprint,
            static_projection_tick = unchecked((long)Game1.ticks),
            source_runtime_type = typeof(StardewValley.Object).FullName,
            placed_runtime_type = typeof(Fence).FullName,
            fence_data_source = "Data/Fences via Fence.GetFenceLookup",
            native_runtime_contract = "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFenceItem)->Fence(tile,item_id,is_gate)",
            route_safety_owner = "shared_collision_grid_virtual_occupancy_bfs_and_protected_access",
            gate_policy = "native_placeable_but_compiler_requires_functional_cardinal_fence_topology",
            rows
        };
        lock (FencePlacementCacheLock)
        {
            cachedFencePlacementFingerprint = fingerprint;
            cachedFencePlacementContext = context;
        }
        return context;
    }

    private static object ReadFencePlacementRow(
        InventoryFenceRef inventoryFence,
        IReadOnlyDictionary<string, FenceData> lookup,
        IReadOnlyList<MachineLocationRef> locations)
    {
        var item = inventoryFence.Item;
        var data = lookup[item.ItemId];
        var isGate = string.Equals(item.ItemId, Fence.gateId, StringComparison.Ordinal);
        var healthMin = isGate ? data.Health * 4f : (data.Health - 1f) * 2f;
        var healthMax = isGate ? data.Health * 4f : (data.Health + 1f) * 2f;
        var maxHealthMin = isGate ? data.Health * 2f : healthMin;
        var maxHealthMax = isGate ? data.Health * 2f : healthMax;
        var projections = locations
            .Select(location => ReadFencePlacementLocation(item, location, isGate))
            .ToArray();
        return new
        {
            inventory_slot_index = inventoryFence.SlotIndex,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            display_name = item.DisplayName,
            stack = item.Stack,
            inventory_runtime_type = item.GetType().FullName,
            placed_runtime_type = typeof(Fence).FullName,
            is_gate = isGate,
            fence_data_key = item.ItemId,
            fence_data_health = data.Health,
            fence_data_placement_sound = data.PlacementSound,
            fence_data_removal_sound = data.RemovalSound,
            fence_data_removal_tool_ids = data.RemovalToolIds?.ToArray() ?? Array.Empty<string>(),
            fence_data_removal_tool_types = data.RemovalToolTypes?.ToArray() ?? Array.Empty<string>(),
            expected_health_min = healthMin,
            expected_health_max = healthMax,
            expected_max_health_min = maxHealthMin,
            expected_max_health_max = maxHealthMax,
            expected_initial_gate_position = 0,
            expected_initial_passable = false,
            location_count = projections.Length,
            static_legal_tile_count = projections.Sum(row => row.StaticLegalTileCount),
            locations = projections.Select(row => row.Row).ToArray()
        };
    }

    private static FencePlacementLocationProjection ReadFencePlacementLocation(
        StardewValley.Object inventoryFence,
        MachineLocationRef locationRef,
        bool isGate)
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
            var probe = (StardewValley.Object)inventoryFence.getOne();
            probe.Location = location;
            probe.TileLocation = Vector2.Zero;
            if (probe.GetType() != typeof(StardewValley.Object) || forbidden || !probe.isPlaceable() || width <= 0 || height <= 0)
            {
                status = probe.GetType() != typeof(StardewValley.Object)
                    ? "custom_inventory_runtime_type_blocked"
                    : forbidden
                        ? "native_location_placement_forbidden"
                        : !probe.isPlaceable()
                            ? "fence_item_not_placeable"
                            : "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendFenceLegalRanges(ranges, y, width, x =>
                    {
                        var tile = new Vector2(x, y);
                        var legal = probe.canBePlacedHere(
                            location,
                            tile,
                            ~(CollisionMask.Characters | CollisionMask.Farmers));
                        var drawSum = legal ? ReadExpectedFenceDrawSum(location, tile, inventoryFence.ItemId) : 0;
                        return new FenceTileSignature(legal, drawSum, isGate && FunctionalGateDrawSums.Contains(drawSum));
                    }, ref legalCount);
                }
                status = legalCount > 0 ? "native_legal_tiles_available" : "no_native_legal_tile";
            }
        }
        catch (Exception ex)
        {
            status = "native_fence_placement_probe_exception:" + ex.GetType().Name;
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
            closed_gate_and_fence_route_policy = "target_is_virtual_block_for_route_safety",
            runtime_recheck = "Utility.playerCanPlaceItemHere_then_live_neighbor_draw_sum_at_exact_loaded_tile"
        };
        return new FencePlacementLocationProjection(row, legalCount);
    }

    internal static int ReadExpectedFenceDrawSum(GameLocation location, Vector2 tile, string itemId)
    {
        var sum = 0;
        AddFenceDrawWeight(location, tile + new Vector2(1, 0), itemId, 100, ref sum);
        AddFenceDrawWeight(location, tile + new Vector2(-1, 0), itemId, 10, ref sum);
        AddFenceDrawWeight(location, tile + new Vector2(0, 1), itemId, 500, ref sum);
        AddFenceDrawWeight(location, tile + new Vector2(0, -1), itemId, 1000, ref sum);
        return sum;
    }

    private static void AddFenceDrawWeight(GameLocation location, Vector2 tile, string itemId, int weight, ref int sum)
    {
        if (location.objects.TryGetValue(tile, out var neighbor) &&
            neighbor is Fence fence && fence.countsForDrawing(itemId))
        {
            sum += weight;
        }
    }

    private static void AppendFenceLegalRanges(
        ICollection<object> ranges,
        int y,
        int width,
        Func<int, FenceTileSignature> read,
        ref int legalCount)
    {
        int? start = null;
        var current = default(FenceTileSignature);
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
                    ranges.Add(FenceRange(y, start.Value, x - 1, current));
                    start = x;
                    current = next;
                }
            }
            else if (start.HasValue)
            {
                ranges.Add(FenceRange(y, start.Value, x - 1, current));
                start = null;
            }
        }
    }

    private static object FenceRange(int y, int startX, int endX, FenceTileSignature signature) => new
    {
        y,
        start_x = startX,
        end_x = endX,
        expected_draw_sum_after = signature.DrawSum,
        expected_gate_functional = signature.GateFunctional
    };

    private sealed record InventoryFenceRef(StardewValley.Object Item, int SlotIndex);
    private sealed record FencePlacementLocationProjection(object Row, int StaticLegalTileCount);
    private readonly record struct FenceTileSignature(bool Legal, int DrawSum, bool GateFunctional);
}
