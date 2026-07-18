using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter : ReadAdapterBase
{
    private static object[] ReadCropCatalog()
    {
        return Game1.cropData
            .Select(pair =>
            {
                var harvestItemId = ReadString(pair.Value, "HarvestItemId") ?? string.Empty;
                return new
                {
                    seed_id = pair.Key,
                    seasons = ReadStringList(pair.Value, "Seasons"),
                    days_in_phase = ReadIntList(pair.Value, "DaysInPhase"),
                    grow_days = ReadIntList(pair.Value, "DaysInPhase").Where(day => day < 99999).Sum(),
                    regrow_days = ReadIntNullable(pair.Value, "RegrowDays"),
                    harvest_item_id = harvestItemId,
                    harvest_item_qualified_id = QualifyObjectId(harvestItemId),
                    harvest_unit_sale_price = ReadHarvestUnitSalePrice(harvestItemId),
                    harvest_min_stack = ReadIntNullable(pair.Value, "HarvestMinStack"),
                    harvest_max_stack = ReadIntNullable(pair.Value, "HarvestMaxStack"),
                    harvest_max_increase_per_farming_level = ReadFloatNullable(pair.Value, "HarvestMaxIncreasePerFarmingLevel"),
                    extra_harvest_chance = ReadFloatNullable(pair.Value, "ExtraHarvestChance"),
                    harvest_min_quality = ReadIntNullable(pair.Value, "HarvestMinQuality"),
                    harvest_max_quality = ReadIntNullable(pair.Value, "HarvestMaxQuality"),
                    harvest_method = ReadString(pair.Value, "HarvestMethod"),
                    is_paddy_crop = ReadBoolNullable(pair.Value, "IsPaddyCrop"),
                    needs_watering = ReadBoolNullable(pair.Value, "NeedsWatering"),
                    plantable_location_rule_count = ReadCount(pair.Value, "PlantableLocationRules")
                };
            })
            .OrderBy(crop => crop.seed_id)
            .ToArray();
    }

    private static string QualifyObjectId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return ItemRegistry.QualifyItemId(itemId) ?? "(O)" + itemId;
    }

    private static int? ReadHarvestUnitSalePrice(string itemId)
    {
        var qualifiedId = QualifyObjectId(itemId);
        if (string.IsNullOrWhiteSpace(qualifiedId))
        {
            return null;
        }

        try
        {
            return ItemRegistry.Create(qualifiedId).salePrice();
        }
        catch
        {
            return null;
        }
    }

    private static object[] ReadCrops(Farm farm)
    {
        return farm.terrainFeatures.Pairs
            .Where(pair => pair.Value is HoeDirt { crop: not null })
            .Select(pair =>
            {
                var dirt = (HoeDirt)pair.Value;
                var crop = dirt.crop;
                var cropData = crop.GetData();
                var experience = ReadCropHarvestExperience(crop);
                var readyForHarvest = dirt.readyForHarvest();
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    harvest_item_id = crop.indexOfHarvest.Value,
                    harvest_item_qualified_id = QualifyObjectId(crop.indexOfHarvest.Value),
                    phase_days = crop.phaseDays.ToArray(),
                    current_phase = crop.currentPhase.Value,
                    phase_count = crop.phaseDays.Count,
                    day_of_current_phase = crop.dayOfCurrentPhase.Value,
                    dead = crop.dead.Value,
                    forage_crop = crop.forageCrop.Value,
                    forage_crop_id = crop.whichForageCrop.Value,
                    fully_grown = crop.fullyGrown.Value,
                    ready_for_harvest = readyForHarvest,
                    days_until_next_harvest_if_watered = ReadDaysUntilNextHarvestIfWatered(crop, readyForHarvest),
                    next_harvest_projection_condition = "crop_remains_in_season_and_receives_each_required_daily_growth_update",
                    harvest_min_stack = cropData?.HarvestMinStack,
                    harvest_max_stack = cropData?.HarvestMaxStack,
                    extra_harvest_chance = cropData?.ExtraHarvestChance,
                    watered = dirt.isWatered(),
                    needs_watering = dirt.needsWatering(),
                    harvest_experience_skill_id = experience.SkillId,
                    harvest_experience_skill_index = experience.SkillIndex,
                    harvest_experience_on_success_min = experience.Minimum,
                    harvest_experience_on_success_max = experience.Maximum,
                    harvest_experience_condition = experience.Condition,
                    harvest_experience_projection_status = experience.Status
                };
            })
            .OrderBy(crop => crop.tile_y)
            .ThenBy(crop => crop.tile_x)
            .ToArray();
    }

    private static int? ReadDaysUntilNextHarvestIfWatered(Crop crop, bool readyForHarvest)
    {
        if (crop.dead.Value || crop.phaseDays.Count == 0)
        {
            return null;
        }
        if (readyForHarvest)
        {
            return 0;
        }
        if (crop.fullyGrown.Value)
        {
            return Math.Max(0, crop.dayOfCurrentPhase.Value);
        }

        var finalGrowthPhase = crop.phaseDays.Count - 1;
        var currentPhase = Math.Clamp(crop.currentPhase.Value, 0, finalGrowthPhase);
        var remaining = 0;
        for (var phase = currentPhase; phase < finalGrowthPhase; phase++)
        {
            var phaseDays = crop.phaseDays[phase];
            if (phaseDays <= 0 || phaseDays >= 99999)
            {
                continue;
            }
            remaining += phase == currentPhase
                ? Math.Max(0, phaseDays - crop.dayOfCurrentPhase.Value)
                : phaseDays;
        }
        return remaining;
    }

    private static object[] ReadTerrainFeatures(Farm farm)
    {
        return farm.terrainFeatures.Pairs
            .Select(pair => new
            {
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                type = pair.Value.GetType().FullName
            })
            .OrderBy(feature => feature.tile_y)
            .ThenBy(feature => feature.tile_x)
            .ToArray();
    }

    private static object[] ReadObjects(Farm farm)
    {
        return farm.objects.Pairs
            .Select(pair => new
            {
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                item_id = pair.Value.ItemId,
                qualified_item_id = pair.Value.QualifiedItemId,
                name = pair.Value.Name,
                display_name = pair.Value.DisplayName,
                stack = pair.Value.Stack,
                quality = pair.Value.Quality,
                big_craftable = pair.Value.bigCraftable.Value,
                ready_for_harvest = pair.Value.readyForHarvest.Value,
                minutes_until_ready = pair.Value.MinutesUntilReady,
                held_item = SummarizeItem(pair.Value.heldObject.Value)
            })
            .OrderBy(item => item.tile_y)
            .ThenBy(item => item.tile_x)
            .ToArray();
    }

}
