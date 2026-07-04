using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class FarmReadAdapter : ReadAdapterBase
{
    private static readonly string[] FarmFields =
    {
        "farm_type",
        "farm_identity",
        "buildings",
        "crops",
        "terrain_features",
        "objects",
        "machines",
        "chests",
        "animals",
        "resource_clumps",
        "debris",
        "warps"
    };

    public override string Domain => "farm";
    public override int Priority => 30;

    public override StateAdapterResult Collect(long tick)
    {
        var farm = Context.IsWorldReady ? Game1.getFarm() : null;
        if (farm is null)
        {
            return Section(
                "farm",
                FarmFields.ToDictionary(
                    field => field,
                    field => (object)Unavailable("world_not_ready_or_farm_unavailable", "Context.IsWorldReady; Game1.getFarm()", tick, "vanilla_1_6_farm")),
                FarmFields.Select(field => "farm." + field).ToArray(),
                "unavailable");
        }

        return Section("farm", new Dictionary<string, object>
        {
            ["farm_type"] = Field(Game1.whichFarm, "Game1.whichFarm", tick, "vanilla_1_6_farm"),
            ["farm_identity"] = Field(new
            {
                location_name = farm.Name,
                location_id = farm.NameOrUniqueName,
                is_farm = farm.IsFarm,
                greenhouse_unlocked = farm.greenhouseUnlocked.Value
            }, "Game1.getFarm().Name/NameOrUniqueName/IsFarm/greenhouseUnlocked", tick, "vanilla_1_6_farm"),
            ["buildings"] = Field(ReadBuildings(farm), "Game1.getFarm().buildings", tick, "vanilla_1_6_farm"),
            ["crops"] = Field(ReadCrops(farm), "Game1.getFarm().terrainFeatures[*] as HoeDirt.crop", tick, "vanilla_1_6_farm"),
            ["terrain_features"] = Field(ReadTerrainFeatures(farm), "Game1.getFarm().terrainFeatures", tick, "vanilla_1_6_farm"),
            ["objects"] = Field(ReadObjects(farm), "Game1.getFarm().objects", tick, "vanilla_1_6_farm"),
            ["machines"] = Field(ReadMachines(farm), "Game1.getFarm().objects[*] machine-shaped objects", tick, "vanilla_1_6_farm"),
            ["chests"] = Field(ReadChests(farm), "Game1.getFarm().objects[*] as Chest", tick, "vanilla_1_6_farm"),
            ["animals"] = Field(ReadAnimals(farm), "Game1.getFarm().animals", tick, "vanilla_1_6_farm"),
            ["resource_clumps"] = Field(ReadResourceClumps(farm), "Game1.getFarm().resourceClumps", tick, "vanilla_1_6_farm"),
            ["debris"] = Field(ReadDebris(farm), "Game1.getFarm().debris", tick, "vanilla_1_6_farm"),
            ["warps"] = Field(ReadWarps(farm), "Game1.getFarm().warps", tick, "vanilla_1_6_farm")
        });
    }

    private static object[] ReadBuildings(Farm farm)
    {
        return farm.buildings
            .Select(building => new
            {
                type = building.buildingType.Value,
                tile_x = building.tileX.Value,
                tile_y = building.tileY.Value,
                tiles_wide = building.tilesWide.Value,
                tiles_high = building.tilesHigh.Value,
                days_of_construction_left = building.daysOfConstructionLeft.Value,
                is_under_construction = building.isUnderConstruction()
            })
            .OrderBy(building => building.tile_y)
            .ThenBy(building => building.tile_x)
            .ThenBy(building => building.type)
            .ToArray();
    }

    private static object[] ReadCrops(Farm farm)
    {
        return farm.terrainFeatures.Pairs
            .Where(pair => pair.Value is HoeDirt { crop: not null })
            .Select(pair =>
            {
                var dirt = (HoeDirt)pair.Value;
                var crop = dirt.crop;
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    harvest_item_id = crop.indexOfHarvest.Value,
                    current_phase = crop.currentPhase.Value,
                    phase_count = crop.phaseDays.Count,
                    day_of_current_phase = crop.dayOfCurrentPhase.Value,
                    dead = crop.dead.Value,
                    forage_crop = crop.forageCrop.Value,
                    forage_crop_id = crop.whichForageCrop.Value,
                    fully_grown = crop.fullyGrown.Value,
                    ready_for_harvest = dirt.readyForHarvest(),
                    watered = dirt.isWatered(),
                    needs_watering = dirt.needsWatering()
                };
            })
            .OrderBy(crop => crop.tile_y)
            .ThenBy(crop => crop.tile_x)
            .ToArray();
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

    private static object[] ReadMachines(Farm farm)
    {
        return farm.objects.Pairs
            .Where(pair => pair.Value.bigCraftable.Value &&
                (pair.Value.MinutesUntilReady > 0 ||
                 pair.Value.readyForHarvest.Value ||
                 pair.Value.heldObject.Value is not null))
            .Select(pair => new
            {
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                qualified_item_id = pair.Value.QualifiedItemId,
                display_name = pair.Value.DisplayName,
                ready_for_harvest = pair.Value.readyForHarvest.Value,
                minutes_until_ready = pair.Value.MinutesUntilReady,
                held_item = SummarizeItem(pair.Value.heldObject.Value)
            })
            .OrderBy(machine => machine.tile_y)
            .ThenBy(machine => machine.tile_x)
            .ToArray();
    }

    private static object[] ReadChests(Farm farm)
    {
        return farm.objects.Pairs
            .Where(pair => pair.Value is Chest)
            .Select(pair =>
            {
                var chest = (Chest)pair.Value;
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    qualified_item_id = chest.QualifiedItemId,
                    display_name = chest.DisplayName,
                    special_chest_type = chest.SpecialChestType.ToString(),
                    item_count = chest.Items.Count,
                    items = chest.Items
                        .Select((item, index) => new
                        {
                            slot_index = index,
                            item = SummarizeItem(item)
                        })
                        .ToArray()
                };
            })
            .OrderBy(chest => chest.tile_y)
            .ThenBy(chest => chest.tile_x)
            .ToArray();
    }

    private static object[] ReadAnimals(Farm farm)
    {
        return farm.animals.Pairs
            .Select(pair => new
            {
                animal_id = pair.Key,
                name = pair.Value.Name,
                display_name = pair.Value.displayName,
                type = pair.Value.type.Value,
                building_type_i_live_in = pair.Value.buildingTypeILiveIn.Value,
                age = pair.Value.age.Value,
                friendship_toward_farmer = pair.Value.friendshipTowardFarmer.Value,
                produce_quality = pair.Value.produceQuality.Value,
                tile_x = pair.Value.TilePoint.X,
                tile_y = pair.Value.TilePoint.Y
            })
            .OrderBy(animal => animal.animal_id)
            .ToArray();
    }

    private static object[] ReadResourceClumps(Farm farm)
    {
        return farm.resourceClumps
            .Select(clump => new
            {
                tile_x = (int)clump.Tile.X,
                tile_y = (int)clump.Tile.Y,
                parent_sheet_index = clump.parentSheetIndex.Value,
                width = clump.width.Value,
                height = clump.height.Value,
                health = clump.health.Value
            })
            .OrderBy(clump => clump.tile_y)
            .ThenBy(clump => clump.tile_x)
            .ToArray();
    }

    private static object[] ReadDebris(Farm farm)
    {
        return farm.debris
            .Select((debris, index) => new
            {
                debris_index = index,
                debris_type = debris.debrisType.Value.ToString(),
                chunk_type = debris.chunkType.Value,
                item_id = debris.itemId.Value,
                item = SummarizeItem(debris.item),
                chunk_count = debris.Chunks.Count
            })
            .ToArray();
    }

    private static object[] ReadWarps(Farm farm)
    {
        return farm.warps
            .Select(warp => new
            {
                x = warp.X,
                y = warp.Y,
                target_name = warp.TargetName,
                target_x = warp.TargetX,
                target_y = warp.TargetY
            })
            .OrderBy(warp => warp.y)
            .ThenBy(warp => warp.x)
            .ToArray();
    }

    private static object? SummarizeItem(Item? item)
    {
        return item is null
            ? null
            : new
            {
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality
            };
    }
}
