using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AuthoritativeRequirementInventoryBuilder
{
    private static readonly IReadOnlyDictionary<string, string> FlavoredMachineOutputItemIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AgedRoe"] = "447",
            ["Honey"] = "340",
            ["Jelly"] = "344",
            ["Juice"] = "350",
            ["Pickle"] = "342",
            ["Roe"] = "812",
            ["Wine"] = "348",
            ["Bait"] = "SpecificBait",
            ["DriedFruit"] = "DriedFruit",
            ["DriedMushroom"] = "DriedMushrooms",
            ["SmokedFish"] = "SmokedFish"
        };

    private static void AddStructuredRuntimeRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement objects,
        JsonElement locations,
        JsonElement farmAnimals,
        JsonElement fruitTrees,
        JsonElement wildTrees,
        JsonElement machines,
        JsonElement fishPonds,
        JsonElement monsters)
    {
        AddFarmAnimalRoutes(routes, farmAnimals);
        AddFruitTreeRoutes(routes, fruitTrees);
        AddWildTreeRoutes(routes, wildTrees);
        AddLocationSpawnRoutes(routes, locations);
        AddObjectAcquisitionRoutes(routes, objects);
        AddMachineRoutes(routes, machines);
        AddFishPondRoutes(routes, fishPonds);
        AddMonsterRoutes(routes, monsters);
        AddNativeSpecialRoutes(routes);
    }

    private static void AddFarmAnimalRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement farmAnimals)
    {
        foreach (var animal in farmAnimals.EnumerateObject())
        {
            AddDropRows(routes, animal.Value, "ProduceItemIds", "native_farm_animal_produce",
                "farm_animal:" + animal.Name, "Data/FarmAnimals", $"payload.{animal.Name}.ProduceItemIds");
            AddDropRows(routes, animal.Value, "DeluxeProduceItemIds", "native_farm_animal_deluxe_produce",
                "farm_animal:" + animal.Name, "Data/FarmAnimals", $"payload.{animal.Name}.DeluxeProduceItemIds");
        }
    }

    private static void AddFruitTreeRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement fruitTrees)
    {
        foreach (var tree in fruitTrees.EnumerateObject())
        {
            AddDropRows(routes, tree.Value, "Fruit", "native_fruit_tree_produce",
                "fruit_tree:" + tree.Name, "Data/FruitTrees", $"payload.{tree.Name}.Fruit");
        }
    }

    private static void AddWildTreeRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement wildTrees)
    {
        foreach (var tree in wildTrees.EnumerateObject())
        {
            AddDropRows(routes, tree.Value, "SeedDropItems", "native_wild_tree_seed_drop",
                "wild_tree:" + tree.Name, "Data/WildTrees", $"payload.{tree.Name}.SeedDropItems");
            AddDropRows(routes, tree.Value, "ChopItems", "native_wild_tree_chop_drop",
                "wild_tree:" + tree.Name, "Data/WildTrees", $"payload.{tree.Name}.ChopItems");
            AddDropRows(routes, tree.Value, "TapItems", "native_wild_tree_tapper_output",
                "wild_tree:" + tree.Name, "Data/WildTrees", $"payload.{tree.Name}.TapItems");
            AddDropRows(routes, tree.Value, "ShakeItems", "native_wild_tree_shake_drop",
                "wild_tree:" + tree.Name, "Data/WildTrees", $"payload.{tree.Name}.ShakeItems");

            var seedItemId = String(tree.Value, "SeedItemId");
            if (seedItemId.Length > 0 &&
                (PositiveNumber(tree.Value, "SeedOnShakeChance") || PositiveNumber(tree.Value, "SeedOnChopChance")))
            {
                foreach (var id in SplitObjectIds(seedItemId))
                {
                    AddRoute(routes, id, new RequirementAcquisitionRoute(
                        "native_wild_tree_seed",
                        "wild_tree:" + tree.Name,
                        "Data/WildTrees",
                        $"payload.{tree.Name}.SeedItemId"));
                }
            }
        }
    }

    private static void AddMachineRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement machines)
    {
        foreach (var machine in machines.EnumerateObject())
        {
            if (!machine.Value.TryGetProperty("OutputRules", out var rules) || rules.ValueKind != JsonValueKind.Array)
                continue;

            var ruleIndex = 0;
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.TryGetProperty("OutputItem", out var outputs) && outputs.ValueKind == JsonValueKind.Array)
                {
                    var outputIndex = 0;
                    foreach (var output in outputs.EnumerateArray())
                    {
                        var path = $"payload.{machine.Name}.OutputRules[{ruleIndex}].OutputItem[{outputIndex}]";
                        var itemQuery = String(output, "ItemId");
                        foreach (var id in MachineOutputItemIds(itemQuery))
                        {
                            AddRoute(routes, id, new RequirementAcquisitionRoute(
                                itemQuery.StartsWith("FLAVORED_ITEM ", StringComparison.Ordinal)
                                    ? "native_machine_flavored_output"
                                    : "native_machine_item_query_output",
                                $"machine:{machine.Name}:rule:{ruleIndex}:output:{outputIndex}",
                                "Data/Machines",
                                path + ".ItemId"));
                        }

                        if (String(output, "OutputMethod") == "StardewValley.Object, Stardew Valley: OutputSolarPanel")
                        {
                            AddRoute(routes, "787", new RequirementAcquisitionRoute(
                                "native_solar_panel_output",
                                "machine:" + machine.Name + ":OutputSolarPanel",
                                "Data/Machines + Object.OutputSolarPanel",
                                path + ".OutputMethod"));
                        }
                        outputIndex++;
                    }
                }
                ruleIndex++;
            }
        }
    }

    private static void AddFishPondRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement fishPonds)
    {
        if (fishPonds.ValueKind != JsonValueKind.Array)
            return;

        var pondIndex = 0;
        foreach (var pond in fishPonds.EnumerateArray())
        {
            AddDropRows(routes, pond, "ProducedItems", "native_fish_pond_output",
                "fish_pond:" + String(pond, "Id"), "Data/FishPondData", $"payload[{pondIndex}].ProducedItems");
            pondIndex++;
        }
    }

    private static void AddMonsterRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement monsters)
    {
        foreach (var monster in monsters.EnumerateObject())
        {
            var fields = (monster.Value.GetString() ?? string.Empty).Split('/');
            if (fields.Length <= 6)
                throw new InvalidDataException("Malformed Data/Monsters row: " + monster.Name);

            var drops = fields[6].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (drops.Length % 2 != 0)
                throw new InvalidDataException("Malformed Data/Monsters drop pairs: " + monster.Name);

            for (var index = 0; index < drops.Length; index += 2)
            {
                if (!double.TryParse(
                        drops[index + 1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var chance))
                {
                    throw new InvalidDataException("Malformed Data/Monsters drop chance: " + monster.Name);
                }
                if (chance <= 0)
                    continue;

                foreach (var id in SplitObjectIds(drops[index]))
                {
                    AddRoute(routes, id, new RequirementAcquisitionRoute(
                        "native_monster_drop_table",
                        "monster:" + monster.Name,
                        "Data/Monsters",
                        $"payload.{monster.Name}[6:drop_pair:{index / 2}]"));
                }
            }
        }
    }

    private static void AddNativeSpecialRoutes(IDictionary<string, List<RequirementAcquisitionRoute>> routes)
    {
        AddNativeSpecialRoute(routes, "296", "native_bush_shake", "Bush.GetShakeOffItem", "Season.Spring => (O)296");
        AddNativeSpecialRoute(routes, "815", "native_tea_bush_harvest", "Bush.GetShakeOffItem", "size 3 => (O)815");
        AddNativeSpecialRoute(routes, "399", "native_spring_onion_harvest", "Crop.harvest", "whichForageCrop 1 => (O)399");
        AddNativeSpecialRoute(routes, "829", "native_ginger_harvest", "Crop.hitWithHoe", "whichForageCrop 2 => (O)829");
        AddNativeSpecialRoute(routes, "Moss", "native_tree_moss_harvest", "Tree.CreateMossItem", "(O)Moss");
        AddNativeSpecialRoute(routes, "909", "native_radioactive_ore_node", "GameLocation.breakStone", "stone 95 => (O)909");
        AddNativeSpecialRoute(routes, "585", "native_mine_buried_item", "MineShaft.checkForBuriedItem", "buried item branch => (O)585");
        AddNativeSpecialRoute(routes, "82", "native_geode_default_drop", "Utility.getTreasureFromGeode", "default geode mineral branch => (O)82");
    }

    private static void AddFishingAcquisitionRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement fish)
    {
        foreach (var entry in fish.EnumerateObject())
        {
            var segments = (entry.Value.GetString() ?? string.Empty).Split('/');
            if (segments.Length <= 1 || !string.Equals(segments[1], "trap", StringComparison.Ordinal))
                continue;

            AddRoute(routes, entry.Name, new RequirementAcquisitionRoute(
                "native_crab_pot_output",
                "crab_pot_fish:" + entry.Name,
                "Data/Fish + CrabPot.DayUpdate",
                "payload." + entry.Name));
        }

        AddNativeSpecialRoute(
            routes,
            "158",
            "native_mine_fishing_override",
            "MineShaft.getFish",
            "mine area 0 or 10 => (O)158");
        AddNativeSpecialRoute(
            routes,
            "161",
            "native_mine_fishing_override",
            "MineShaft.getFish",
            "mine area 40 => (O)161");
        AddNativeSpecialRoute(
            routes,
            "162",
            "native_mine_fishing_override",
            "MineShaft.getFish",
            "mine area 80 => (O)162");
    }

    private static void AddNativeSpecialRoute(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        string itemId,
        string kind,
        string sourceId,
        string sourcePath) =>
        AddRoute(routes, itemId, new RequirementAcquisitionRoute(kind, sourceId, "decompiled native method", sourcePath));

    private static IEnumerable<string> MachineOutputItemIds(string query)
    {
        const string flavoredPrefix = "FLAVORED_ITEM ";
        if (!query.StartsWith(flavoredPrefix, StringComparison.Ordinal))
            return SplitObjectIds(query);

        var preserveType = query[flavoredPrefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        return FlavoredMachineOutputItemIds.TryGetValue(preserveType, out var itemId)
            ? new[] { itemId }
            : Array.Empty<string>();
    }

    private static void AddLocationSpawnRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement locations)
    {
        foreach (var location in locations.EnumerateObject())
        {
            AddDropRows(routes, location.Value, "Forage", "native_location_forage_spawn",
                "location:" + location.Name, "Data/Locations", $"payload.{location.Name}.Forage");
            AddDropRows(routes, location.Value, "ArtifactSpots", "native_location_artifact_spot",
                "location:" + location.Name, "Data/Locations", $"payload.{location.Name}.ArtifactSpots");
        }
    }

    private static void AddObjectAcquisitionRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement objects)
    {
        foreach (var item in objects.EnumerateObject())
        {
            AddDropRows(routes, item.Value, "GeodeDrops", "native_geode_drop",
                "geode:" + item.Name, "Data/Objects", $"payload.{item.Name}.GeodeDrops");

            if (item.Value.TryGetProperty("ArtifactSpotChances", out var chances) &&
                chances.ValueKind == JsonValueKind.Object && chances.EnumerateObject().Any())
            {
                AddRoute(routes, item.Name, new RequirementAcquisitionRoute(
                    "native_object_artifact_spot_chance",
                    "artifact_item:" + item.Name,
                    "Data/Objects",
                    $"payload.{item.Name}.ArtifactSpotChances"));
            }
        }
    }

    private static void AddDropRows(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        JsonElement owner,
        string property,
        string kind,
        string sourceId,
        string sourceAsset,
        string sourcePath)
    {
        if (!owner.TryGetProperty(property, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object)
            {
                AddItemQueryRoutes(
                    routes,
                    String(row, "ItemId"),
                    row.TryGetProperty("RandomItemId", out var random) ? random : default,
                    kind,
                    sourceId + ":" + index,
                    sourceAsset,
                    sourcePath + $"[{index}]");
            }
            index++;
        }
    }

    private static void AddItemQueryRoutes(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        string itemId,
        JsonElement randomItemIds,
        string kind,
        string sourceId,
        string sourceAsset,
        string sourcePath)
    {
        foreach (var id in SplitObjectIds(itemId))
            AddRoute(routes, id, new RequirementAcquisitionRoute(kind, sourceId, sourceAsset, sourcePath + ".ItemId"));

        if (randomItemIds.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var value in randomItemIds.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                foreach (var id in SplitObjectIds(value.GetString()))
                {
                    AddRoute(routes, id, new RequirementAcquisitionRoute(
                        kind,
                        sourceId + ":random:" + index,
                        sourceAsset,
                        sourcePath + $".RandomItemId[{index}]"));
                }
            }
            index++;
        }
    }

    private static bool PositiveNumber(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number) &&
        number > 0;

    private static void GuardStructuredAcquisitionSources(
        string gameLocationPath,
        string farmAnimalPath,
        string fruitTreePath,
        string wildTreePath,
        string utilityPath)
    {
        var locationSource = File.ReadAllText(gameLocationPath);
        RequireContains(locationSource, "GetData(\"Default\").Forage.Concat(data.Forage)", gameLocationPath);
        RequireContains(locationSource, "enumerable = enumerable.Concat(data.ArtifactSpots)", gameLocationPath);

        var animalSource = File.ReadAllText(farmAnimalPath);
        RequireContains(animalSource, "list.AddRange(animalData.ProduceItemIds)", farmAnimalPath);
        RequireContains(animalSource, "list.AddRange(animalData.DeluxeProduceItemIds)", farmAnimalPath);

        var fruitSource = File.ReadAllText(fruitTreePath);
        RequireContains(fruitSource, "foreach (FruitTreeFruitData item2 in data.Fruit)", fruitTreePath);
        RequireContains(fruitSource, "Item item = TryCreateFruit(item2)", fruitTreePath);

        var treeSource = File.ReadAllText(wildTreePath);
        RequireContains(treeSource, "TryGetTapperOutput(data.TapItems", wildTreePath);
        RequireContains(treeSource, "foreach (WildTreeChopItemData chopItem in data.ChopItems)", wildTreePath);
        RequireContains(treeSource, "Game1.createMultipleObjectDebris(data.SeedItemId", wildTreePath);

        var utilitySource = File.ReadAllText(utilityPath);
        RequireContains(utilitySource, "foreach (ObjectGeodeDropData drop in value.GeodeDrops.OrderBy", utilityPath);
        RequireContains(utilitySource, "_ => ItemRegistry.Create(\"(O)82\")", utilityPath);
    }

    private static void GuardMachineAndFishPondSources(
        string itemQueryPath,
        string objectDefinitionPath,
        string fishPondPath,
        string objectPath)
    {
        var querySource = File.ReadAllText(itemQueryPath);
        RequireContains(querySource, "IEnumerable<ItemQueryResult> FLAVORED_ITEM", itemQueryPath);
        RequireContains(querySource, "CreateFlavoredItem(parsed, obj)", itemQueryPath);

        var definitionSource = File.ReadAllText(objectDefinitionPath);
        RequireContains(definitionSource, "GetBaseItemIdForFlavoredItem", objectDefinitionPath);
        foreach (var mapping in FlavoredMachineOutputItemIds)
        {
            RequireContains(
                definitionSource,
                $"Object.PreserveType.{mapping.Key} => \"(O){mapping.Value}\"",
                objectDefinitionPath);
        }

        var fishPondSource = File.ReadAllText(fishPondPath);
        RequireContains(fishPondSource, "foreach (FishPondReward producedItem in fishPondData.ProducedItems)", fishPondPath);
        RequireContains(fishPondSource, "ItemQueryResolver.TryResolveRandomItem(selectedOutput", fishPondPath);

        var objectSource = File.ReadAllText(objectPath);
        RequireContains(objectSource, "public static Item OutputSolarPanel", objectPath);
        RequireContains(objectSource, "ItemRegistry.Create<Object>(\"(O)787\")", objectPath);
    }

    private static void GuardMonsterAndNativeSpecialSources(
        string monsterPath,
        string bushPath,
        string cropPath,
        string mineShaftPath,
        string gameLocationPath,
        string wildTreePath)
    {
        var monsterSource = File.ReadAllText(monsterPath);
        RequireContains(monsterSource, "DataLoader.Monsters(Game1.content)[name].Split('/')", monsterPath);
        RequireContains(monsterSource, "string[] array2 = ArgUtility.SplitBySpace(array[6])", monsterPath);
        RequireContains(monsterSource, "objectsToDrop.Add(array2[i])", monsterPath);

        var bushSource = File.ReadAllText(bushPath);
        RequireContains(bushSource, "public string GetShakeOffItem()", bushPath);
        RequireContains(bushSource, "3 => \"(O)815\"", bushPath);
        RequireContains(bushSource, "Season.Spring => \"(O)296\"", bushPath);

        var cropSource = File.ReadAllText(cropPath);
        RequireContains(cropSource, "whichForageCrop.Value == \"2\"", cropPath);
        RequireContains(cropSource, "ItemRegistry.Create<Object>(\"(O)829\")", cropPath);
        RequireContains(cropSource, "ItemRegistry.Create<Object>(\"(O)399\")", cropPath);

        var mineSource = File.ReadAllText(mineShaftPath);
        RequireContains(mineSource, "public override string checkForBuriedItem", mineShaftPath);
        RequireContains(mineSource, "id = \"(O)585\"", mineShaftPath);

        var locationSource = File.ReadAllText(gameLocationPath);
        RequireContains(locationSource, "case \"95\":", gameLocationPath);
        RequireContains(locationSource, "Game1.createMultipleObjectDebris(\"(O)909\"", gameLocationPath);

        var treeSource = File.ReadAllText(wildTreePath);
        RequireContains(treeSource, "public static Item CreateMossItem()", wildTreePath);
        RequireContains(treeSource, "ItemRegistry.Create(\"(O)Moss\"", wildTreePath);
    }

    private static void GuardCropPlanningSources(
        string cropPath,
        string hoeDirtPath)
    {
        var cropSource = File.ReadAllText(cropPath);
        RequireContains(cropSource, "public bool IsInSeason(GameLocation location)", cropPath);
        RequireContains(cropSource, "location.SeedsIgnoreSeasonsHere()", cropPath);
        RequireContains(cropSource, "GetData()?.Seasons?.Contains(location.GetSeason())", cropPath);
        RequireContains(cropSource, "phaseDays.AddRange(data.DaysInPhase)", cropPath);
        RequireContains(cropSource, "data?.RegrowDays ?? (-1)", cropPath);
        RequireContains(cropSource, "public virtual bool isWildSeedCrop()", cropPath);
        RequireContains(cropSource, "case \"495\":", cropPath);
        RequireContains(cropSource, "return getRandomWildCropForSeason(Season.Spring)", cropPath);
        RequireContains(cropSource, "case \"496\":", cropPath);
        RequireContains(cropSource, "return getRandomWildCropForSeason(Season.Summer)", cropPath);
        RequireContains(cropSource, "case \"497\":", cropPath);
        RequireContains(cropSource, "return getRandomWildCropForSeason(Season.Fall)", cropPath);
        RequireContains(cropSource, "case \"498\":", cropPath);
        RequireContains(cropSource, "return getRandomWildCropForSeason(Season.Winter)", cropPath);
        RequireContains(cropSource, "Season.Spring => Game1.random.Choose(\"(O)16\"", cropPath);
        RequireContains(cropSource, "Season.Summer => Game1.random.Choose(\"(O)396\"", cropPath);
        RequireContains(cropSource, "Season.Fall => Game1.random.Choose(\"(O)404\"", cropPath);
        RequireContains(cropSource, "Season.Winter => Game1.random.Choose(\"(O)412\"", cropPath);

        var hoeDirtSource = File.ReadAllText(hoeDirtPath);
        RequireContains(hoeDirtSource, "Crop.TryGetData(itemId, out var data)", hoeDirtPath);
        RequireContains(hoeDirtSource, "location.SeedsIgnoreSeasonsHere()", hoeDirtPath);
        RequireContains(hoeDirtSource, "data.Seasons?.Contains(season)", hoeDirtPath);
        RequireContains(hoeDirtSource, "public void applySpeedIncreases(Farmer who)", hoeDirtPath);
        RequireContains(hoeDirtSource, "int num3 = (int)Math.Ceiling((float)num * num2)", hoeDirtPath);
    }

    private static void GuardFishingAcquisitionSources(
        string decompileRoot,
        string mineShaftPath,
        string crabPotPath,
        string gameStateQueryPath,
        string farmPath,
        string islandPath,
        string islandSouthEastPath,
        string railroadPath)
    {
        var expectedOverrides = new[]
        {
            farmPath,
            islandPath,
            islandSouthEastPath,
            mineShaftPath,
            railroadPath
        }
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actualOverrides = Directory
            .EnumerateFiles(decompileRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "override Item getFish(float millisecondsAfterNibble",
                StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!actualOverrides.SequenceEqual(expectedOverrides, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Native getFish override inventory drifted. Actual: " +
                string.Join(",", actualOverrides.Select(path => Path.GetRelativePath(decompileRoot, path))));
        }

        var mineSource = File.ReadAllText(mineShaftPath);
        RequireContains(mineSource, "public override Item getFish(", mineShaftPath);
        RequireContains(mineSource, "case 0:", mineShaftPath);
        RequireContains(mineSource, "case 10:", mineShaftPath);
        RequireContains(mineSource, "text = \"(O)158\"", mineShaftPath);
        RequireContains(mineSource, "case 40:", mineShaftPath);
        RequireContains(mineSource, "text = \"(O)161\"", mineShaftPath);
        RequireContains(mineSource, "case 80:", mineShaftPath);
        RequireContains(mineSource, "text = \"(O)162\"", mineShaftPath);
        RequireContains(mineSource, "0.02 + 0.01 * num", mineShaftPath);
        RequireContains(mineSource, "0.015 + 0.009 * num", mineShaftPath);
        RequireContains(mineSource, "0.01 + 0.008 * num", mineShaftPath);

        var crabPotSource = File.ReadAllText(crabPotPath);
        RequireContains(crabPotSource, "public override void DayUpdate()", crabPotPath);
        RequireContains(crabPotSource, "DataLoader.Fish(Game1.content)", crabPotPath);
        RequireContains(crabPotSource, "if (!item.Value.Contains(\"trap\"))", crabPotPath);
        RequireContains(crabPotSource, "location.GetCrabPotFishForTile", crabPotPath);

        var querySource = File.ReadAllText(gameStateQueryPath);
        RequireContains(querySource, "public static bool SEASON(string[] query, GameStateQueryContext context)", gameStateQueryPath);
        RequireContains(querySource, "public static bool YEAR(string[] query, GameStateQueryContext context)", gameStateQueryPath);
        RequireContains(querySource, "int maxYear", gameStateQueryPath);
        RequireContains(querySource, "year >= value", gameStateQueryPath);
        RequireContains(querySource, "year <= value2", gameStateQueryPath);
        RequireContains(querySource, "public static bool TIME(string[] query, GameStateQueryContext context)", gameStateQueryPath);
        RequireContains(querySource, "int maxTime", gameStateQueryPath);

        var farmSource = File.ReadAllText(farmPath);
        RequireContains(farmSource, "FarmFishLocationOverride", farmPath);
        RequireContains(farmSource, "base.getFish(millisecondsAfterNibble, bait, waterDepth, who, baitPotency, bobberTile, _fishLocationOverride)", farmPath);

        var islandSource = File.ReadAllText(islandPath);
        RequireContains(islandSource, "limitedNutDrops[\"IslandFishing\"]", islandPath);
        RequireContains(islandSource, "return ItemRegistry.Create(\"(O)73\")", islandPath);

        var islandSouthEastSource = File.ReadAllText(islandSouthEastPath);
        RequireContains(islandSouthEastSource, "MarkCollectedNut(\"StardropPool\")", islandSouthEastPath);
        RequireContains(islandSouthEastSource, "return ItemRegistry.Create(\"(O)73\")", islandSouthEastPath);

        var railroadSource = File.ReadAllText(railroadPath);
        RequireContains(railroadSource, "GameLocation.CAROLINES_NECKLACE_ITEM_QID", railroadPath);
        RequireContains(railroadSource, "return base.getFish(millisecondsAfterNibble", railroadPath);
    }
}
