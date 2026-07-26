using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadMachineRelocationRouteReachability(
        IReadOnlyList<MachineLocationRef> locations)
    {
        var rows = locations
            .Select(ReadMachineRelocationRouteReachabilityLocation)
            .ToArray();
        return new
        {
            schema_version =
                "machine_relocation_route_reachability.v1",
            projection_status =
                "complete_static_native_walkability_for_relocation_scope",
            location_count = rows.Length,
            collision_contract =
                "GameLocation.IsTileBlockedBy(tile,All_without_Characters_or_Farmers,ignorePassables:All)",
            dynamic_obstacle_policy =
                "characters_farmers_and_animals_do_not_split_persistent_components;fresh_current_map_collision_recheck_after_route",
            compression = "walkable_row_ranges",
            locations = rows
        };
    }

    private static object
        ReadMachineRelocationRouteReachabilityLocation(
            MachineLocationRef locationRef)
    {
        var location = locationRef.Location;
        var layers = location.map?.Layers?
            .Cast<xTile.Layers.Layer>()
            .ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0
            ? 0
            : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0
            ? 0
            : layers.Max(layer => layer.LayerHeight);
        var ranges = new List<object>();
        var walkableCount = 0;
        string status;

        try
        {
            if (width <= 0 || height <= 0)
            {
                status = "location_map_dimensions_unavailable";
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    AppendWalkableRanges(
                        ranges,
                        y,
                        width,
                        x => !location.IsTileBlockedBy(
                            new Vector2(x, y),
                            ~(CollisionMask.Characters |
                              CollisionMask.Farmers),
                            CollisionMask.All),
                        ref walkableCount);
                }
                status = walkableCount > 0
                    ? "native_static_walkable_tiles_available"
                    : "no_native_static_walkable_tile";
            }
        }
        catch (Exception ex)
        {
            status =
                "native_static_walkability_probe_exception:" +
                ex.GetType().Name;
        }

        return new
        {
            location_id = location.NameOrUniqueName,
            location_kind = locationRef.Kind,
            location_is_player_controlled =
                locationRef.IsPlayerControlled,
            map_width = width,
            map_height = height,
            projection_status = status,
            static_walkable_tile_count = walkableCount,
            static_blocked_tile_count =
                Math.Max(0, width * height - walkableCount),
            static_walkable_tile_ranges = ranges.ToArray(),
            collision_mask =
                "CollisionMask.All_without_Characters_or_Farmers",
            ignore_passables_mask = "CollisionMask.All",
            runtime_recheck =
                "current_location_collision_grid_then_native_movement_and_exact_placement"
        };
    }

    private static void AppendWalkableRanges(
        ICollection<object> ranges,
        int y,
        int width,
        Func<int, bool> isWalkable,
        ref int walkableCount)
    {
        int? start = null;
        for (var x = 0; x <= width; x++)
        {
            var walkable = x < width && isWalkable(x);
            if (walkable)
            {
                walkableCount++;
                start ??= x;
            }
            else if (start.HasValue)
            {
                ranges.Add(new
                {
                    y,
                    start_x = start.Value,
                    end_x = x - 1
                });
                start = null;
            }
        }
    }
}
