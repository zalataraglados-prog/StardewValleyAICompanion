using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter
{
    private static object ReadSocialRouteDateEvidence()
    {
        var locations = Game1.locations
            .Where(location => location is not null)
            .OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .Select(ReadSocialRouteDateLocationEvidence)
            .ToArray();
        return new
        {
            schema_version = "social_route_date_evidence.v2",
            capture_total_days = Game1.Date.TotalDays,
            capture_year = Game1.year,
            capture_season = Game1.currentSeason,
            capture_day_of_month = Game1.dayOfMonth,
            capture_weekday = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth),
            capture_time = Game1.timeOfDay,
            location_count = locations.Length,
            all_location_static_walkability_complete = locations.All(location =>
                string.Equals(
                    ReadStringProperty(location, "projection_status"),
                    "exact_current_date_static_native_walkability",
                    StringComparison.Ordinal)),
            collision_contract =
                "GameLocation.IsTileBlockedBy(tile,All_without_Characters_or_Farmers,ignorePassables:All)",
            dynamic_actor_policy =
                "characters_and_farmers_are_excluded_from_persistent_topology_and_require_fresh_execution_replan",
            timing_status = "player_route_movement_timing_not_yet_attached",
            projection_status =
                "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            locations
        };
    }

    private static object ReadSocialRouteDateLocationEvidence(
        GameLocation location)
    {
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
                    AppendSocialRouteWalkableRanges(
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
                status = "exact_current_date_static_native_walkability";
            }
        }
        catch (Exception ex)
        {
            status = "native_static_walkability_probe_exception:" +
                ex.GetType().Name;
        }

        var buildConditions = location.getMapProperty("BuildConditions");
        var unsupportedActionRecords = ReadMapActions(location)
            .Where(action =>
                ClassifyRouteActionBranch(action.branch) !=
                    "covered_for_read")
            .Select(action => new
            {
                tile_x = action.tile_x,
                tile_y = action.tile_y,
                action.source_property,
                action.raw_action,
                action.branch
            })
            .OrderBy(action => action.tile_y)
            .ThenBy(action => action.tile_x)
            .ThenBy(action => action.source_property, StringComparer.Ordinal)
            .ToArray();
        var unsupportedActionTiles = unsupportedActionRecords
            .GroupBy(action => (action.tile_x, action.tile_y))
            .Select(group => new
            {
                tile_x = group.Key.tile_x,
                tile_y = group.Key.tile_y,
                action_record_count = group.Count(),
                actions = group.Select(action => new
                {
                    action.source_property,
                    action.raw_action,
                    action.branch
                }).ToArray()
            })
            .OrderBy(tile => tile.tile_y)
            .ThenBy(tile => tile.tile_x)
            .ToArray();
        return new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_context_id = location.GetLocationContextId(),
            runtime_type = location.GetType().FullName,
            map_id = location.map?.Id,
            seeds_ignore_seasons_here = location.SeedsIgnoreSeasonsHere(),
            map_width = width,
            map_height = height,
            projection_status = status,
            static_walkable_tile_count = walkableCount,
            static_blocked_tile_count =
                Math.Max(0, width * height - walkableCount),
            static_walkable_tile_ranges = ranges.ToArray(),
            build_conditions = string.IsNullOrWhiteSpace(buildConditions)
                ? null
                : buildConditions,
            build_conditions_met = string.IsNullOrWhiteSpace(buildConditions)
                ? (bool?)null
                : GameStateQuery.CheckConditions(buildConditions, location),
            unsupported_route_action_record_count =
                unsupportedActionRecords.Length,
            unsupported_route_action_tile_count =
                unsupportedActionTiles.Length,
            unsupported_route_action_tiles = unsupportedActionTiles,
            cultivation_capacity = ReadCultivationCapacity(location),
            action_gates = ReadActionGates(location)
        };
    }

    private static object ReadCultivationCapacity(GameLocation location)
    {
        var preparedSoil = location.terrainFeatures.Pairs
            .Where(pair => pair.Value is HoeDirt)
            .Select(pair => ((int)pair.Key.X, (int)pair.Key.Y,
                Dirt: (HoeDirt)pair.Value, IsGardenPot: false))
            .Concat(location.objects.Pairs
                .Where(pair => pair.Value is IndoorPot { bush.Value: null })
                .Select(pair => ((int)pair.Key.X, (int)pair.Key.Y,
                    Dirt: ((IndoorPot)pair.Value).hoeDirt.Value,
                    IsGardenPot: true)))
            .ToArray();
        var occupied = preparedSoil
            .Where(row => row.Dirt.crop is not null)
            .Select(row => new
            {
                qualified_item_id = ItemRegistry.QualifyItemId(
                    row.Dirt.crop.indexOfHarvest.Value) ?? string.Empty,
                row.IsGardenPot
            })
            .GroupBy(row => (row.qualified_item_id, row.IsGardenPot))
            .Select(group => new
            {
                harvest_item_qualified_id = group.Key.qualified_item_id,
                is_garden_pot = group.Key.IsGardenPot,
                slot_count = group.Count()
            })
            .OrderBy(row => row.harvest_item_qualified_id,
                StringComparer.Ordinal)
            .ThenBy(row => row.is_garden_pot)
            .ToArray();
        var unresolvedHarvestItemSlotCount = occupied
            .Where(row => string.IsNullOrWhiteSpace(
                row.harvest_item_qualified_id))
            .Sum(row => row.slot_count);
        var open = preparedSoil.Count(row => row.Dirt.crop is null);
        return new
        {
            schema_version = "prepared_cultivation_capacity.v1",
            projection_status = "exact_current_snapshot_prepared_soil_slots",
            total_prepared_soil_slot_count = preparedSoil.Length,
            open_prepared_soil_slot_count = open,
            occupied_crop_slot_count = preparedSoil.Length - open,
            unresolved_harvest_item_slot_count =
                unresolvedHarvestItemSlotCount,
            garden_pot_slot_count = preparedSoil.Count(row => row.IsGardenPot),
            occupied_harvest_items = occupied,
            establishment_scope =
                "existing_HoeDirt_and_unbushed_IndoorPot_only",
            excluded_scope =
                "potential_new_tilling_clearance_time_energy_and_inputs"
        };
    }

    private static void AppendSocialRouteWalkableRanges(
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
