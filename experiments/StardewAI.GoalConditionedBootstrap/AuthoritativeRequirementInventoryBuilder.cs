using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AuthoritativeRequirementInventoryBuilder
{
    private static readonly HashSet<int> ExcludedShipmentCategories = new()
    {
        -999, -103, -102, -96, -74, -29, -24, -22, -21, -20, -19, -14, -12, -8, -7, -2, 0
    };

    private static readonly HashSet<string> ExcludedShipmentTypes = new(StringComparer.Ordinal)
    {
        "Arch", "Fish", "Minerals", "Cooking"
    };

    private static readonly HashSet<string> AdmittedGraphRouteKinds = new(StringComparer.Ordinal)
    {
        "harvests_as", "machine_output", "sells", "recipe_output", "creates_reward_item"
    };

    public static AuthoritativeRequirementInventoryReport Build(
        string manifestPath,
        string goalDependencyPath,
        string authoritativeGraphPath,
        string decompileRoot)
    {
        var manifestFullPath = Path.GetFullPath(manifestPath);
        var goalFullPath = Path.GetFullPath(goalDependencyPath);
        var graphFullPath = Path.GetFullPath(authoritativeGraphPath);
        var decompileFullPath = Path.GetFullPath(decompileRoot);
        var rawRoot = Path.GetDirectoryName(manifestFullPath)!;

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestFullPath));
        var manifest = manifestDocument.RootElement;
        var gameVersion = RequiredString(manifest, "gameVersion");
        var objectsPath = ResolveExport(rawRoot, manifest, "Data/Objects");
        var fishPath = ResolveExport(rawRoot, manifest, "Data/Fish");
        var locationsPath = ResolveExport(rawRoot, manifest, "Data/Locations");
        var farmAnimalsPath = ResolveExport(rawRoot, manifest, "Data/FarmAnimals");
        var fruitTreesPath = ResolveExport(rawRoot, manifest, "Data/FruitTrees");
        var wildTreesPath = ResolveExport(rawRoot, manifest, "Data/WildTrees");
        var machinesPath = ResolveExport(rawRoot, manifest, "Data/Machines");
        var fishPondsPath = ResolveExport(rawRoot, manifest, "Data/FishPondData");
        var monstersPath = ResolveExport(rawRoot, manifest, "Data/Monsters");
        var objectSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Object.cs");
        var utilitySourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Utility.cs");
        var museumSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Locations", "LibraryMuseum.cs");
        var gameLocationSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "GameLocation.cs");
        var farmAnimalSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "FarmAnimal.cs");
        var fruitTreeSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "TerrainFeatures", "FruitTree.cs");
        var wildTreeSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "TerrainFeatures", "Tree.cs");
        var itemQuerySourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Internal", "ItemQueryResolver.cs");
        var objectDefinitionSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "ItemTypeDefinitions", "ObjectDataDefinition.cs");
        var fishPondSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Buildings", "FishPond.cs");
        var monsterSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Monsters", "Monster.cs");
        var bushSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "TerrainFeatures", "Bush.cs");
        var cropSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Crop.cs");
        var mineShaftSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Locations", "MineShaft.cs");
        var crabPotSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Objects", "CrabPot.cs");
        var gameStateQuerySourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "GameStateQuery.cs");
        var farmFishingSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Farm.cs");
        var islandFishingSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Locations", "IslandLocation.cs");
        var islandSouthEastFishingSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Locations", "IslandSouthEast.cs");
        var railroadFishingSourcePath = Path.Combine(decompileFullPath, "StardewValley", "StardewValley", "Locations", "Railroad.cs");

        GuardNativeSources(objectSourcePath, utilitySourcePath, museumSourcePath);
        GuardStructuredAcquisitionSources(
            gameLocationSourcePath,
            farmAnimalSourcePath,
            fruitTreeSourcePath,
            wildTreeSourcePath,
            utilitySourcePath);
        GuardMachineAndFishPondSources(
            itemQuerySourcePath,
            objectDefinitionSourcePath,
            fishPondSourcePath,
            objectSourcePath);
        GuardMonsterAndNativeSpecialSources(
            monsterSourcePath,
            bushSourcePath,
            cropSourcePath,
            mineShaftSourcePath,
            gameLocationSourcePath,
            wildTreeSourcePath);
        GuardFishingAcquisitionSources(
            decompileFullPath,
            mineShaftSourcePath,
            crabPotSourcePath,
            gameStateQuerySourcePath,
            farmFishingSourcePath,
            islandFishingSourcePath,
            islandSouthEastFishingSourcePath,
            railroadFishingSourcePath);

        using var objectsDocument = JsonDocument.Parse(File.ReadAllText(objectsPath));
        using var fishDocument = JsonDocument.Parse(File.ReadAllText(fishPath));
        using var locationsDocument = JsonDocument.Parse(File.ReadAllText(locationsPath));
        using var farmAnimalsDocument = JsonDocument.Parse(File.ReadAllText(farmAnimalsPath));
        using var fruitTreesDocument = JsonDocument.Parse(File.ReadAllText(fruitTreesPath));
        using var wildTreesDocument = JsonDocument.Parse(File.ReadAllText(wildTreesPath));
        using var machinesDocument = JsonDocument.Parse(File.ReadAllText(machinesPath));
        using var fishPondsDocument = JsonDocument.Parse(File.ReadAllText(fishPondsPath));
        using var monstersDocument = JsonDocument.Parse(File.ReadAllText(monstersPath));
        using var goalDocument = JsonDocument.Parse(File.ReadAllText(goalFullPath));
        using var graphDocument = JsonDocument.Parse(File.ReadAllText(graphFullPath));

        var objects = objectsDocument.RootElement.GetProperty("payload");
        var fish = fishDocument.RootElement.GetProperty("payload");
        var locations = locationsDocument.RootElement.GetProperty("payload");
        var farmAnimals = farmAnimalsDocument.RootElement.GetProperty("payload");
        var fruitTrees = fruitTreesDocument.RootElement.GetProperty("payload");
        var wildTrees = wildTreesDocument.RootElement.GetProperty("payload");
        var machines = machinesDocument.RootElement.GetProperty("payload");
        var fishPonds = fishPondsDocument.RootElement.GetProperty("payload");
        var monsters = monstersDocument.RootElement.GetProperty("payload");
        var routeIndex = BuildRouteIndex(
            graphDocument.RootElement,
            objects,
            fish,
            locations,
            farmAnimals,
            fruitTrees,
            wildTrees,
            machines,
            fishPonds,
            monsters);

        var requirementSets = new[]
        {
            BuildFullShipment(objects, routeIndex),
            BuildMasterAngler(objects, routeIndex),
            BuildMuseumCollection(objects, routeIndex),
            BuildCommunityCenter(goalDocument.RootElement, objects, routeIndex)
        };
        var unresolved = requirementSets
            .SelectMany(set => set.Groups)
            .Where(group => !group.RouteCovered)
            .Select(group => group.RequirementId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new AuthoritativeRequirementInventoryReport
        {
            Status = unresolved.Length == 0 ? "complete" : "denominators_complete_routes_incomplete",
            GoalId = RequiredString(goalDocument.RootElement.GetProperty("grandpa_goal"), "goalId"),
            GameVersion = gameVersion,
            DenominatorComplete = requirementSets.All(set => set.DenominatorStatus == "complete"),
            AcquisitionRoutesComplete = unresolved.Length == 0,
            SourceEvidence = new[]
            {
                Evidence("raw_manifest", manifestFullPath, "runtime export manifest"),
                Evidence("runtime_data_objects", objectsPath, "runtime DataLoader.Objects export"),
                Evidence("runtime_data_fish", fishPath, "runtime DataLoader.Fish export"),
                Evidence("runtime_data_locations", locationsPath, "runtime DataLoader.Locations export"),
                Evidence("runtime_data_farm_animals", farmAnimalsPath, "runtime DataLoader.FarmAnimals export"),
                Evidence("runtime_data_fruit_trees", fruitTreesPath, "runtime DataLoader.FruitTrees export"),
                Evidence("runtime_data_wild_trees", wildTreesPath, "runtime DataLoader.WildTrees export"),
                Evidence("runtime_data_machines", machinesPath, "runtime DataLoader.Machines export"),
                Evidence("runtime_data_fish_ponds", fishPondsPath, "runtime DataLoader.FishPondData export"),
                Evidence("runtime_data_monsters", monstersPath, "runtime DataLoader.Monsters export"),
                Evidence("goal_dependency_index", goalFullPath, "compiled native bundle and Grandpa rules"),
                Evidence("authoritative_dependency_graph", graphFullPath, "compiled runtime acquisition identity edges"),
                Evidence("native_shipping_rule", objectSourcePath, "decompiled Object.isPotentialBasicShipped"),
                Evidence("native_fish_completion_rule", utilitySourcePath, "decompiled Utility.getFishCaughtPercent"),
                Evidence("native_museum_rule", museumSourcePath, "decompiled LibraryMuseum.IsItemSuitableForDonation"),
                Evidence("native_location_spawn_rules", gameLocationSourcePath, "decompiled forage and artifact-spot resolution"),
                Evidence("native_farm_animal_produce_rule", farmAnimalSourcePath, "decompiled FarmAnimal produce selection"),
                Evidence("native_fruit_tree_produce_rule", fruitTreeSourcePath, "decompiled FruitTree fruit selection"),
                Evidence("native_wild_tree_drop_rule", wildTreeSourcePath, "decompiled Tree drop and tapper selection"),
                Evidence("native_flavored_item_query_rule", itemQuerySourcePath, "decompiled FLAVORED_ITEM query resolution"),
                Evidence("native_flavored_item_identity_rule", objectDefinitionSourcePath, "decompiled preserve-type base object mapping"),
                Evidence("native_fish_pond_output_rule", fishPondSourcePath, "decompiled FishPond produced-item selection"),
                Evidence("native_solar_panel_output_rule", objectSourcePath, "decompiled Object.OutputSolarPanel"),
                Evidence("native_monster_drop_rule", monsterSourcePath, "decompiled Monster.parseMonsterInfo drop parsing"),
                Evidence("native_bush_produce_rule", bushSourcePath, "decompiled Bush.GetShakeOffItem"),
                Evidence("native_forage_crop_rule", cropSourcePath, "decompiled Crop forage harvest branches"),
                Evidence("native_mine_buried_item_rule", mineShaftSourcePath, "decompiled MineShaft.checkForBuriedItem"),
                Evidence("native_mine_fishing_override_rule", mineShaftSourcePath, "decompiled MineShaft.getFish"),
                Evidence("native_crab_pot_output_rule", crabPotSourcePath, "decompiled CrabPot.DayUpdate"),
                Evidence("native_game_state_query_rule", gameStateQuerySourcePath, "decompiled GameStateQuery calendar predicates"),
                Evidence("native_farm_fishing_override_rule", farmFishingSourcePath, "decompiled Farm.getFish location redirect"),
                Evidence("native_island_fishing_override_rule", islandFishingSourcePath, "decompiled IslandLocation.getFish walnut branch"),
                Evidence("native_island_southeast_fishing_override_rule", islandSouthEastFishingSourcePath, "decompiled IslandSouthEast.getFish walnut branch"),
                Evidence("native_railroad_fishing_override_rule", railroadFishingSourcePath, "decompiled Railroad.getFish necklace branch")
            },
            RequirementSets = requirementSets,
            UnresolvedAcquisitionRequirementIds = unresolved
        };
    }

    private static GoalRequirementSet BuildFullShipment(
        JsonElement objects,
        IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes)
    {
        var groups = objects.EnumerateObject()
            .Where(pair => IsFullShipmentItem(pair.Name, pair.Value))
            .Select(pair => ItemGroup(
                "full_shipment:item:" + pair.Name,
                pair.Name,
                pair.Value,
                routes,
                $"world_progress.full_shipment_progress.items[item_id={pair.Name}].shipped"))
            .OrderBy(group => group.RequirementId, StringComparer.Ordinal)
            .ToArray();
        return Set(
            "full_shipment",
            "achievement_full_shipment",
            "Utility.getFarmerItemsShippedPercent>=1 and achievement 34",
            "world_progress.full_shipment_progress",
            groups);
    }

    private static GoalRequirementSet BuildMasterAngler(
        JsonElement objects,
        IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes)
    {
        var groups = objects.EnumerateObject()
            .Where(pair => String(pair.Value, "Type") == "Fish" &&
                pair.Value.GetProperty("ExcludeFromFishingCollection").ValueKind == JsonValueKind.False)
            .Select(pair => ItemGroup(
                "master_angler:item:" + pair.Name,
                pair.Name,
                pair.Value,
                routes,
                $"world_progress.fish_collection_progress.items[item_id={pair.Name}].caught"))
            .OrderBy(group => group.RequirementId, StringComparer.Ordinal)
            .ToArray();
        return Set(
            "master_angler",
            "achievement_master_angler",
            "Utility.getFishCaughtPercent>=1 and achievement 26",
            "world_progress.fish_collection_progress",
            groups);
    }

    private static GoalRequirementSet BuildMuseumCollection(
        JsonElement objects,
        IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes)
    {
        var groups = objects.EnumerateObject()
            .Where(pair => IsMuseumItem(pair.Value))
            .Select(pair => ItemGroup(
                "museum_collection:item:" + pair.Name,
                pair.Name,
                pair.Value,
                routes,
                $"world_progress.museum.donatable_items[item_id={pair.Name}].donated"))
            .OrderBy(group => group.RequirementId, StringComparer.Ordinal)
            .ToArray();
        return Set(
            "museum_collection",
            "achievement_complete_collection",
            "LibraryMuseum.museumPieces.Count>=LibraryMuseum.totalArtifacts and achievement 5",
            "world_progress.museum",
            groups);
    }

    private static GoalRequirementSet BuildCommunityCenter(
        JsonElement goalRoot,
        JsonElement objects,
        IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes)
    {
        var groups = goalRoot.GetProperty("bundles").EnumerateArray()
            .Where(bundle => RequiredString(bundle, "area") != "Abandoned Joja Mart")
            .Select(bundle =>
            {
                var key = RequiredString(bundle, "key");
                var alternatives = bundle.GetProperty("ingredients").EnumerateArray()
                    .Select(ingredient =>
                    {
                        var matchKind = RequiredString(ingredient, "matchKind");
                        var itemId = RequiredString(ingredient, "itemIdOrCategory");
                        var item = matchKind == "item_id" && objects.TryGetProperty(itemId, out var value)
                            ? value
                            : default;
                        var itemRoutes = matchKind == "money_payment"
                            ? new[] { new RequirementAcquisitionRoute("native_money_payment", "money", "Data/Bundles", "payload." + key) }
                            : RoutesFor(routes, itemId);
                        return new GoalRequirementAlternative
                        {
                            ItemId = itemId,
                            QualifiedItemId = matchKind == "item_id" ? "(O)" + itemId : string.Empty,
                            DisplayName = item.ValueKind == JsonValueKind.Object ? String(item, "Name") : string.Empty,
                            MatchKind = matchKind,
                            Amount = ingredient.GetProperty("amount").GetInt32(),
                            MinimumQuality = ingredient.GetProperty("minimumQuality").GetInt32(),
                            AcquisitionRoutes = itemRoutes
                        };
                    })
                    .ToArray();
                var required = bundle.GetProperty("requiredSlots").GetInt32();
                return new GoalRequirementGroup
                {
                    RequirementId = "community_center:bundle:" + key,
                    SelectionRule = "choose_at_least_required_slots",
                    RequiredAlternativeCount = required,
                    TransparentCompletionPath = $"world_progress.community_center.bundle_rows[bundle_data_key={key}].complete",
                    RouteCovered = alternatives.Count(value => value.AcquisitionRoutes.Length > 0) >= required,
                    Alternatives = alternatives
                };
            })
            .OrderBy(group => group.RequirementId, StringComparer.Ordinal)
            .ToArray();
        return Set(
            "community_center_standard",
            "community_center_access_or_completion",
            "CommunityCenter.ccIsComplete and event/mail settlement on the selected standard bundle set",
            "world_progress.community_center.bundle_rows",
            groups);
    }

    private static GoalRequirementSet Set(
        string setId,
        string criterionId,
        string nativeRule,
        string statePath,
        GoalRequirementGroup[] groups) => new()
    {
        RequirementSetId = setId,
        CriterionId = criterionId,
        NativeCompletionRule = nativeRule,
        TransparentStatePath = statePath,
        DenominatorStatus = groups.Length > 0 ? "complete" : "blocked_empty_denominator",
        RequiredGroupCount = groups.Length,
        RouteCoveredGroupCount = groups.Count(group => group.RouteCovered),
        AcquisitionRoutesComplete = groups.All(group => group.RouteCovered),
        Groups = groups
    };

    private static GoalRequirementGroup ItemGroup(
        string requirementId,
        string itemId,
        JsonElement item,
        IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes,
        string completionPath)
    {
        var alternative = new GoalRequirementAlternative
        {
            ItemId = itemId,
            QualifiedItemId = "(O)" + itemId,
            DisplayName = String(item, "Name"),
            MatchKind = "item_id",
            Amount = 1,
            MinimumQuality = 0,
            AcquisitionRoutes = RoutesFor(routes, itemId)
        };
        return new GoalRequirementGroup
        {
            RequirementId = requirementId,
            SelectionRule = "all_required",
            RequiredAlternativeCount = 1,
            TransparentCompletionPath = completionPath,
            RouteCovered = alternative.AcquisitionRoutes.Length > 0,
            Alternatives = new[] { alternative }
        };
    }

    private static Dictionary<string, RequirementAcquisitionRoute[]> BuildRouteIndex(
        JsonElement graph,
        JsonElement objects,
        JsonElement fish,
        JsonElement locations,
        JsonElement farmAnimals,
        JsonElement fruitTrees,
        JsonElement wildTrees,
        JsonElement machines,
        JsonElement fishPonds,
        JsonElement monsters)
    {
        var routes = new Dictionary<string, List<RequirementAcquisitionRoute>>(StringComparer.Ordinal);
        foreach (var edge in graph.GetProperty("edges").EnumerateArray())
        {
            var kind = RequiredString(edge, "kind");
            var target = RequiredString(edge, "to");
            var source = RequiredString(edge, "from");
            if (!AdmittedGraphRouteKinds.Contains(kind) || !TryObjectItemId(target, out var itemId) ||
                !IsIdentitySafeRoute(kind, source, target))
            {
                continue;
            }
            AddRoute(routes, itemId, new RequirementAcquisitionRoute(
                kind,
                source,
                String(edge, "sourceAsset"),
                String(edge, "sourcePath")));
        }

        foreach (var location in locations.EnumerateObject())
        {
            if (!location.Value.TryGetProperty("Fish", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var index = 0;
            foreach (var row in rows.EnumerateArray())
            {
                foreach (var itemId in LocationFishItemIds(row))
                {
                    AddRoute(routes, itemId, new RequirementAcquisitionRoute(
                        "native_location_fish_spawn",
                        "location_fish:" + location.Name + ":" + index,
                        "Data/Locations",
                        $"payload.{location.Name}.Fish[{index}]"));
                }
                index++;
            }
        }

        AddStructuredRuntimeRoutes(
            routes,
            objects,
            locations,
            farmAnimals,
            fruitTrees,
            wildTrees,
            machines,
            fishPonds,
            monsters);
        AddFishingAcquisitionRoutes(routes, fish);

        return routes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Distinct()
                .OrderBy(route => route.Kind, StringComparer.Ordinal)
                .ThenBy(route => route.SourceId, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static IEnumerable<string> LocationFishItemIds(JsonElement row)
    {
        if (row.TryGetProperty("ItemId", out var itemId) && itemId.ValueKind == JsonValueKind.String)
        {
            foreach (var value in SplitObjectIds(itemId.GetString()))
                yield return value;
        }
        if (row.TryGetProperty("RandomItemId", out var random) && random.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in random.EnumerateArray().Select(value => value.GetString()))
            foreach (var item in SplitObjectIds(value))
                yield return item;
        }
    }

    private static IEnumerable<string> SplitObjectIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;
        foreach (var token in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var value = token.StartsWith("(O)", StringComparison.Ordinal) ? token[3..] : token;
            if (value.All(character => char.IsLetterOrDigit(character) || character == '_'))
                yield return value;
        }
    }

    private static void AddRoute(
        IDictionary<string, List<RequirementAcquisitionRoute>> routes,
        string itemId,
        RequirementAcquisitionRoute route)
    {
        if (!routes.TryGetValue(itemId, out var values))
        {
            values = new List<RequirementAcquisitionRoute>();
            routes.Add(itemId, values);
        }
        values.Add(route);
    }

    private static RequirementAcquisitionRoute[] RoutesFor(
        IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes,
        string itemId) => routes.TryGetValue(itemId, out var values) ? values : Array.Empty<RequirementAcquisitionRoute>();

    private static bool IsIdentitySafeRoute(string kind, string source, string target) => kind switch
    {
        "harvests_as" => source.StartsWith("crop:", StringComparison.Ordinal),
        "machine_output" or "sells" or "creates_reward_item" => target.StartsWith("item:(O)", StringComparison.Ordinal),
        "recipe_output" => source.StartsWith("cooking_recipe:", StringComparison.Ordinal),
        _ => false
    };

    private static bool TryObjectItemId(string target, out string itemId)
    {
        const string qualifiedPrefix = "item:(O)";
        const string rawPrefix = "item:";
        itemId = target.StartsWith(qualifiedPrefix, StringComparison.Ordinal)
            ? target[qualifiedPrefix.Length..]
            : target.StartsWith(rawPrefix, StringComparison.Ordinal)
                ? target[rawPrefix.Length..]
                : string.Empty;
        return itemId.Length > 0 && itemId.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static bool IsFullShipmentItem(string itemId, JsonElement item)
    {
        if (itemId == "433")
            return true;
        var type = String(item, "Type");
        var category = item.GetProperty("Category").GetInt32();
        return !ExcludedShipmentTypes.Contains(type) &&
            !ExcludedShipmentCategories.Contains(category) &&
            item.GetProperty("ExcludeFromShippingCollection").ValueKind == JsonValueKind.False;
    }

    private static bool IsMuseumItem(JsonElement item)
    {
        var tags = item.TryGetProperty("ContextTags", out var contextTags) && contextTags.ValueKind == JsonValueKind.Array
            ? contextTags.EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (tags.Contains("not_museum_donatable"))
            return false;
        return tags.Contains("museum_donatable") || String(item, "Type") is "Arch" or "Minerals";
    }

    private static void GuardNativeSources(string objectPath, string utilityPath, string museumPath)
    {
        var objectSource = File.ReadAllText(objectPath);
        RequireContains(objectSource, "isPotentialBasicShipped(string itemId, int category, string objectType)", objectPath);
        RequireContains(objectSource, "case \"Arch\":", objectPath);
        RequireContains(objectSource, "value.ExcludeFromShippingCollection", objectPath);
        RequireContains(objectSource, "itemId == \"433\"", objectPath);

        var utilitySource = File.ReadAllText(utilityPath);
        RequireContains(utilitySource, "getFishCaughtPercent(Farmer who = null)", utilityPath);
        RequireContains(utilitySource, "allDatum.ObjectType == \"Fish\"", utilityPath);
        RequireContains(utilitySource, "ExcludeFromFishingCollection: not false", utilityPath);

        var museumSource = File.ReadAllText(museumPath);
        RequireContains(museumSource, "IsItemSuitableForDonation(string itemId, bool checkDonatedItems = true)", museumPath);
        RequireContains(museumSource, "not_museum_donatable", museumPath);
        RequireContains(museumSource, "item_type_arch", museumPath);
        RequireContains(museumSource, "item_type_minerals", museumPath);
    }

    private static string ResolveExport(string rawRoot, JsonElement manifest, string assetName)
    {
        var export = manifest.GetProperty("exports").EnumerateArray()
            .Single(value => RequiredString(value, "assetName") == assetName);
        if (RequiredString(export, "status") != "available")
            throw new InvalidDataException("Required runtime export is unavailable: " + assetName);
        var outputFile = RequiredString(export, "outputFile");
        var path = Path.GetFullPath(Path.Combine(rawRoot, outputFile));
        if (!path.StartsWith(Path.GetFullPath(rawRoot).TrimEnd('\\') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new InvalidDataException("Runtime export path is missing or escapes the raw root: " + outputFile);
        return path;
    }

    private static RequirementSourceEvidence Evidence(string id, string path, string authority) =>
        new(id, Path.GetFullPath(path), ContentInventoryVerifier.HashFile(path), authority);

    private static string RequiredString(JsonElement value, string property)
    {
        var result = String(value, property);
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException("Required string is empty: " + property)
            : result;
    }

    private static string String(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString() ?? string.Empty
            : string.Empty;

    private static void RequireContains(string source, string expected, string path)
    {
        if (!source.Contains(expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Native source guard failed for {path}: {expected}");
    }
}
