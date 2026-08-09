using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadPlantingContext(
        GameLocation location)
    {
        var seedCandidates = Game1.player?.Items
            .Select((item, index) =>
                ReadSeedCandidate(item, index, location))
            .Where(item => item is not null)
            .Cast<object>()
            .ToArray() ?? Array.Empty<object>();
        var fertilizerCandidates = Game1.player?.Items
            .Select((item, index) => ReadFertilizerCandidate(item, index))
            .Where(item => item is not null)
            .Cast<object>()
            .ToArray() ?? Array.Empty<object>();

        return new
        {
            location_id = location.NameOrUniqueName,
            season = location.GetSeason()
                .ToString()
                .ToLowerInvariant(),
            is_outdoors = location.IsOutdoors,
            is_farm = location.IsFarm,
            is_greenhouse = location.IsGreenhouse,
            seeds_ignore_seasons_here =
                location.SeedsIgnoreSeasonsHere(),
            can_plant_here_default =
                location.GetData()?.CanPlantHere ??
                location.IsFarm,
            candidate_seed_count = seedCandidates.Length,
            candidate_fertilizer_count = fertilizerCandidates.Length,
            hoe_dirt_tiles =
                ReadHoeDirtPlantingTiles(
                    location,
                    seedCandidates,
                    fertilizerCandidates)
        };
    }

    private static object? ReadFertilizerCandidate(Item? item, int index)
    {
        return item is StardewObject { Category: StardewObject.fertilizerCategory } fertilizer
            ? new
            {
                slot_index = index,
                item_id = fertilizer.ItemId,
                qualified_item_id = fertilizer.QualifiedItemId,
                stack = fertilizer.Stack,
                category = fertilizer.Category
            }
            : null;
    }

    private static object? ReadSeedCandidate(
        Item? item,
        int index,
        GameLocation location)
    {
        if (item is null)
        {
            return null;
        }

        var seedId = Crop.ResolveSeedId(
            item.ItemId,
            location);
        var cropCatalogMatch =
            Game1.cropData.TryGetValue(
                seedId,
                out var cropData);
        if (item.Category != StardewObject.SeedsCategory &&
            !cropCatalogMatch)
        {
            return null;
        }

        return new
        {
            slot_index = index,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            stack = item.Stack,
            category = item.Category,
            seed_id = seedId,
            crop_catalog_match = cropCatalogMatch,
            crop_seasons = cropData?.Seasons?
                .Select(season =>
                    season.ToString()
                        .ToLowerInvariant())
                .ToArray() ??
                Array.Empty<string>()
        };
    }

    private static object[] ReadHoeDirtPlantingTiles(
        GameLocation location,
        object[] seedCandidates,
        object[] fertilizerCandidates)
    {
        return ReadHoeDirtRows(location)
            .OrderBy(row => row.Tile.Y)
            .ThenBy(row => row.Tile.X)
            .Select(row =>
                ReadHoeDirtPlantingTile(
                    location,
                    row.Tile,
                    row.Dirt,
                    row.IsGardenPot,
                    seedCandidates,
                    fertilizerCandidates))
            .ToArray();
    }

    private static IEnumerable<PlantingDirtRow> ReadHoeDirtRows(GameLocation location)
    {
        foreach (var pair in location.terrainFeatures.Pairs)
        {
            if (pair.Value is HoeDirt dirt)
            {
                yield return new PlantingDirtRow(pair.Key, dirt, false);
            }
        }

        foreach (var pair in location.objects.Pairs)
        {
            if (pair.Value is IndoorPot { bush.Value: null } pot)
            {
                yield return new PlantingDirtRow(pair.Key, pot.hoeDirt.Value, true);
            }
        }
    }

    private static object ReadHoeDirtPlantingTile(
        GameLocation location,
        Vector2 tile,
        HoeDirt dirt,
        bool isGardenPot,
        object[] seedCandidates,
        object[] fertilizerCandidates)
    {
        location.objects.TryGetValue(tile, out var tileObject);
        var indoorPotBypass =
            isGardenPot &&
            !location.IsOutdoors;
        var paddyWaterEligible =
            PaddyWaterEligible(
                location,
                tile,
                isGardenPot);
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            has_crop = dirt.crop is not null,
            is_garden_pot = isGardenPot,
            indoor_pot_season_bypass =
                indoorPotBypass,
            occupied_object_qualified_id =
                tileObject?.QualifiedItemId,
            fertilizer_id = dirt.fertilizer.Value,
            fertilizer_speed_boost =
                dirt.GetFertilizerSpeedBoost(),
            agriculturist_speed_boost =
                Game1.player?.professions.Contains(
                    Farmer.agriculturist) == true
                    ? 0.1f
                    : 0f,
            has_paddy_crop = dirt.hasPaddyCrop(),
            paddy_near_water_cache =
                dirt.nearWaterForPaddy.Value,
            paddy_water_scan_radius = 3,
            paddy_water_eligible =
                paddyWaterEligible,
            paddy_speed_boost =
                dirt.hasPaddyCrop() &&
                paddyWaterEligible
                    ? 0.25f
                    : 0f,
            fertilizer_results = fertilizerCandidates
                .Select(fertilizer => ReadFertilizerTileResult(dirt, fertilizer))
                .ToArray(),
            seed_results = seedCandidates
                .Select(seed =>
                    ReadSeedTilePlantingResult(
                        location,
                        tile,
                        dirt,
                        seed,
                        isGardenPot,
                        indoorPotBypass))
                .ToArray()
        };
    }

    private static object ReadFertilizerTileResult(HoeDirt dirt, object fertilizerCandidate)
    {
        var type = fertilizerCandidate.GetType();
        var itemId = (string?)type.GetProperty("item_id")?.GetValue(fertilizerCandidate) ?? string.Empty;
        var qualifiedItemId = (string?)type.GetProperty("qualified_item_id")?.GetValue(fertilizerCandidate) ?? string.Empty;
        var slotIndex = (int?)type.GetProperty("slot_index")?.GetValue(fertilizerCandidate);
        var stack = (int?)type.GetProperty("stack")?.GetValue(fertilizerCandidate) ?? 0;
        var status = dirt.CheckApplyFertilizerRules(qualifiedItemId);
        return new
        {
            slot_index = slotIndex,
            item_id = itemId,
            qualified_item_id = qualifiedItemId,
            stack,
            apply_status = status.ToString(),
            hard_rule_allows_application = status == HoeDirtFertilizerApplyStatus.Okay
        };
    }

    private sealed record PlantingDirtRow(Vector2 Tile, HoeDirt Dirt, bool IsGardenPot);

    private static object ReadSeedTilePlantingResult(
        GameLocation location,
        Vector2 tile,
        HoeDirt dirt,
        object seedCandidate,
        bool isGardenPot,
        bool indoorPotBypass)
    {
        var seedType = seedCandidate.GetType();
        var seedId =
            (string?)seedType
                .GetProperty("seed_id")
                ?.GetValue(seedCandidate) ??
            string.Empty;
        var slotIndex =
            (int?)seedType
                .GetProperty("slot_index")
                ?.GetValue(seedCandidate);
        var season = location.GetSeason();
        var cropDataFound =
            Game1.cropData.TryGetValue(
                seedId,
                out var cropData);
        var seasonAllowed =
            indoorPotBypass ||
            location.SeedsIgnoreSeasonsHere() ||
            (cropData?.Seasons?.Contains(season) ??
                false);
        string? deniedMessage = null;
        var canPlantSeedsHere =
            indoorPotBypass ||
            location.CanPlantSeedsHere(
                seedId,
                (int)tile.X,
                (int)tile.Y,
                isGardenPot,
                out deniedMessage);
        var baseGrowDays = cropData?.DaysInPhase?
            .Where(day => day < 99999)
            .Sum();
        var isPaddyCrop =
            cropData is not null &&
            ReadBool(cropData, "IsPaddyCrop") == true;
        var paddyEligible =
            isPaddyCrop &&
            PaddyWaterEligible(
                location,
                tile,
                isGardenPot);
        var speedBoostWithoutPaddy =
            dirt.GetFertilizerSpeedBoost() +
            (Game1.player?.professions.Contains(
                Farmer.agriculturist) == true
                ? 0.1f
                : 0f);
        var speedBoostWithPaddy =
            speedBoostWithoutPaddy +
            (paddyEligible ? 0.25f : 0f);
        int? adjustedGrowDays =
            cropData is null
                ? null
                : AdjustedGrowDays(
                    cropData.DaysInPhase,
                    speedBoostWithPaddy);
        var daysRemainingInSeason =
            Math.Max(0, 28 - Game1.dayOfMonth);
        var seasonBypass =
            indoorPotBypass ||
            location.SeedsIgnoreSeasonsHere();

        return new
        {
            slot_index = slotIndex,
            seed_id = seedId,
            crop_catalog_match = cropDataFound,
            can_plant_seeds_here =
                canPlantSeedsHere,
            denied_message_present =
                !string.IsNullOrWhiteSpace(
                    deniedMessage),
            season_allowed = seasonAllowed,
            current_day_of_month =
                Game1.dayOfMonth,
            days_remaining_in_season =
                daysRemainingInSeason,
            is_paddy_crop = isPaddyCrop,
            paddy_water_eligible = paddyEligible,
            speed_boost_without_paddy =
                speedBoostWithoutPaddy,
            speed_boost_with_paddy =
                speedBoostWithPaddy,
            base_grow_days = baseGrowDays,
            adjusted_grow_days_with_paddy_if_eligible =
                adjustedGrowDays,
            can_mature_before_season_end_with_paddy_if_eligible =
                seasonBypass ||
                (adjustedGrowDays.HasValue &&
                    adjustedGrowDays.Value <=
                        daysRemainingInSeason),
            hard_rule_allows_planting =
                cropDataFound &&
                canPlantSeedsHere &&
                seasonAllowed
        };
    }

    private static bool PaddyWaterEligible(
        GameLocation location,
        Vector2 tile,
        bool isGardenPot)
    {
        if (isGardenPot)
        {
            return false;
        }

        const int radius = 3;
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (location.isWaterTile(
                        (int)tile.X + x,
                        (int)tile.Y + y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool? ReadBool(
        object source,
        string propertyName)
    {
        return source.GetType()
            .GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public)
            ?.GetValue(source) as bool?;
    }

    private static int AdjustedGrowDays(
        IReadOnlyList<int> daysInPhase,
        float speedBoost)
    {
        var phaseDays = daysInPhase.ToList();
        phaseDays.Add(99999);
        var totalGrowDays = phaseDays
            .Where(day => day != 99999)
            .Sum();
        var daysToRemove =
            (int)Math.Ceiling(
                totalGrowDays * speedBoost);
        var passes = 0;
        while (daysToRemove > 0 && passes < 3)
        {
            for (var index = 0;
                index < phaseDays.Count;
                index++)
            {
                if ((index > 0 ||
                        phaseDays[index] > 1) &&
                    phaseDays[index] != 99999 &&
                    phaseDays[index] > 0)
                {
                    phaseDays[index]--;
                    daysToRemove--;
                }

                if (daysToRemove <= 0)
                {
                    break;
                }
            }

            passes++;
        }

        return phaseDays
            .Where(day => day != 99999)
            .Sum();
    }
}
