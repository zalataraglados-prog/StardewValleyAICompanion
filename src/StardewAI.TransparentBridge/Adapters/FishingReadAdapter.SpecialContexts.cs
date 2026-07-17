using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FishingReadAdapter : ReadAdapterBase
{
    private static SpecialCatchSourcesProjection ReadSpecialCatchSources(
        GameLocation location,
        Farmer player,
        FishingRod? selectedRod,
        FishingTileReadRow[] fishableTiles)
    {
        var tileIndices = fishableTiles
            .Select((tile, index) => (tile, index))
            .ToDictionary(pair => (pair.tile.TileX, pair.tile.TileY), pair => pair.index);
        var ponds = location.buildings
            .OfType<FishPond>()
            .Select(pond => new
            {
                building_type = pond.GetType().FullName,
                tile_x = pond.tileX.Value,
                tile_y = pond.tileY.Value,
                tiles_wide = pond.tilesWide.Value,
                tiles_high = pond.tilesHigh.Value,
                days_of_construction_left = pond.daysOfConstructionLeft.Value,
                fish_item_id = pond.fishType.Value,
                fish_qualified_item_id = string.IsNullOrWhiteSpace(pond.fishType.Value)
                    ? null
                    : "(O)" + pond.fishType.Value,
                fish_count = pond.FishCount,
                catch_available = pond.daysOfConstructionLeft.Value <= 0 && pond.FishCount > 0,
                catch_effect = "decrements_fish_count_by_one",
                fishable_tile_indices = fishableTiles
                    .Where(tile => pond.isTileFishable(new Vector2(tile.TileX, tile.TileY)))
                    .Select(tile => tileIndices[(tile.TileX, tile.TileY)])
                    .ToArray()
            })
            .ToArray();
        var frenzyItemId = location.fishFrenzyFish.Value;
        var frenzyPoint = location.fishSplashPoint.Value;
        var frenzyActive = !string.IsNullOrWhiteSpace(frenzyItemId);
        var overrideMethod = location.GetType()
            .GetMethods()
            .FirstOrDefault(method => method.Name == nameof(GameLocation.getFish) && method.GetParameters().Length == 7);
        var overrideDeclaringType = overrideMethod?.DeclaringType;
        var hasLocationOverride = overrideDeclaringType is not null && overrideDeclaringType != typeof(GameLocation);
        var overrideRead = ReadLocationOverride(
            location,
            player,
            selectedRod,
            fishableTiles,
            tileIndices,
            overrideDeclaringType);

        return new SpecialCatchSourcesProjection(new
        {
            priority_order = new[]
            {
                "location_get_fish_override",
                "fish_pond",
                "fish_frenzy",
                "data_locations_spawn_rules",
                "trash_fallback"
            },
            location_get_fish_override = new
            {
                present = hasLocationOverride,
                runtime_location_type = location.GetType().FullName,
                declaring_type = overrideDeclaringType?.FullName,
                transparent_handler_available = overrideRead.Complete,
                handlers = overrideRead.Handlers,
                reason = !overrideRead.Complete
                    ? "location_specific_or_modded_getFish_override_not_decoded"
                    : null
            },
            fish_ponds = ponds,
            fish_frenzy = new
            {
                active = frenzyActive,
                qualified_item_id = frenzyActive ? frenzyItemId : null,
                center_tile_x = frenzyPoint.X,
                center_tile_y = frenzyPoint.Y,
                radius_tiles = 2,
                eligible_fishable_tile_indices = frenzyActive
                    ? fishableTiles
                        .Where(tile => Vector2.Distance(
                            new Vector2(tile.TileX, tile.TileY),
                            new Vector2(frenzyPoint.X, frenzyPoint.Y)) <= 2f)
                        .Select(tile => tileIndices[(tile.TileX, tile.TileY)])
                        .ToArray()
                    : Array.Empty<int>()
            },
            fallbacks = new
            {
                tutorial_location_data_fallback_qualified_item_id = "(O)145",
                no_location_data_match_qualified_item_id = "(O)168"
            }
        }, !hasLocationOverride || overrideRead.Complete);
    }

    private static RodFishingContextsProjection ReadRodContexts(
        GameLocation location,
        Farmer player,
        FishingRod? currentRod,
        FishingTileReadRow[] fishableTiles)
    {
        var rows = new List<object>();
        var complete = true;
        foreach (var entry in player.Items.Select((item, slotIndex) => new { item, slotIndex }))
        {
            if (entry.item is not FishingRod rod)
            {
                continue;
            }

            object? spawnRules = null;
            SpecialCatchSourcesProjection? specialSources = null;
            string? failure = null;
            try
            {
                spawnRules = ReadSpawnRules(location, player, rod, fishableTiles);
                specialSources = ReadSpecialCatchSources(location, player, rod, fishableTiles);
            }
            catch (Exception ex)
            {
                failure = $"{ex.GetType().Name}: {ex.Message}";
            }

            var contextComplete = spawnRules is not null && specialSources is not null && specialSources.Complete;
            complete &= contextComplete;
            rows.Add(new
            {
                rod_slot_index = entry.slotIndex,
                rod_qualified_item_id = rod.QualifiedItemId,
                rod_upgrade_level = rod.UpgradeLevel,
                uses_training_rod = rod.QualifiedItemId == "(T)TrainingRod",
                quality_bobber_count = rod.GetTackle().Count(item => item?.QualifiedItemId == "(O)877"),
                tackle_qualified_item_ids = rod.GetTackle()
                    .Where(item => item is not null)
                    .Select(item => item!.QualifiedItemId)
                    .ToArray(),
                selected = ReferenceEquals(rod, currentRod),
                complete = contextComplete,
                failure,
                spawn_rules = spawnRules,
                special_catch_sources = specialSources?.Value,
                special_catch_sources_complete = specialSources?.Complete == true
            });
        }

        return new RodFishingContextsProjection(rows.ToArray(), complete);
    }

}
