using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AuthoritativeRequirementInventoryBuilder
{
    private const string MissingBundleAreaName = "Abandoned Joja Mart";

    private static readonly IReadOnlyDictionary<string, string>
        CommunityCenterItemNameAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Battery"] = "Battery Pack"
            };

    private static readonly IReadOnlyDictionary<string, string>
        CommunityCenterCategoryIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["EggCategory"] = "-5",
                ["MilkCategory"] = "-6"
            };

    private static CommunityCenterDenominatorCatalog
        BuildCommunityCenterDenominatorCatalog(
            JsonElement standardBundles,
            JsonElement randomBundles,
            JsonElement objects,
            IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes)
    {
        RequireCommunityCenter(standardBundles.ValueKind == JsonValueKind.Object,
            "Data/Bundles payload is not an object.");
        RequireCommunityCenter(randomBundles.ValueKind == JsonValueKind.Array,
            "Data/RandomBundles payload is not an array.");
        RequireCommunityCenter(objects.ValueKind == JsonValueKind.Object,
            "Data/Objects payload is not an object.");

        var standardKeys = standardBundles.EnumerateObject()
            .Select(value => value.Name)
            .ToArray();
        var supplementalKeys = standardKeys
            .Where(key => BundleAreaName(key) == MissingBundleAreaName)
            .ToArray();
        var activeStandardKeys = standardKeys
            .Except(supplementalKeys, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var nativeTemplates = standardBundles.EnumerateObject()
            .Select(ParseNativeBundleTemplate)
            .ToDictionary(value => value.BundleDataKey, StringComparer.Ordinal);
        var objectIdsByName = BuildObjectIdsByName(objects);
        var areas = randomBundles.EnumerateArray()
            .Select(area => ParseRemixedArea(area, objectIdsByName, objects))
            .OrderBy(area => area.AreaName, StringComparer.Ordinal)
            .ToArray();
        RequireCommunityCenter(
            areas.Select(area => area.AreaName)
                .Distinct(StringComparer.Ordinal).Count() == areas.Length,
            "Data/RandomBundles contains duplicate area names.");

        var remixedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var area in areas)
        {
            foreach (var keyId in area.KeyIds)
            {
                var key = area.AreaName + "/" + keyId;
                RequireCommunityCenter(activeStandardKeys.Contains(key),
                    "A remixed bundle key is outside the standard active topology: " +
                    key);
                RequireCommunityCenter(remixedKeys.Add(key),
                    "Data/RandomBundles reuses an active key: " + key);
            }
        }

        var retainedStandardKeys = activeStandardKeys
            .Except(remixedKeys, StringComparer.Ordinal)
            .ToArray();
        var topologyComplete = remixedKeys.Count + retainedStandardKeys.Length ==
            activeStandardKeys.Count;
        RequireCommunityCenter(topologyComplete,
            "The standard/remixed Community Center key topology is incomplete.");
        RequireCommunityCenter(retainedStandardKeys.All(key => BundleAreaName(key) == "Vault"),
            "Remixed generation retains an unexpected standard area.");
        var ingredientCatalog = BuildCommunityCenterIngredientAcquisitionCatalog(
            nativeTemplates.Values.SelectMany(value => value.Ingredients)
                .Concat(areas.SelectMany(area => area.BundleSets
                    .SelectMany(set => set.Templates)
                    .Concat(area.PoolTemplates)
                    .SelectMany(template => template.IngredientSlots)
                    .SelectMany(slot => slot.Options))),
            objects,
            routes);
        var ingredientCatalogComplete = ingredientCatalog.All(value =>
            value.AcquisitionRouteComplete);
        RequireCommunityCenter(ingredientCatalogComplete,
            "A Community Center ingredient identity has no authoritative acquisition target route.");

        return new CommunityCenterDenominatorCatalog
        {
            Status = "complete_standard_and_remixed_catalog",
            StandardActiveBundleCount = activeStandardKeys.Count,
            SupplementalBundleCount = supplementalKeys.Length,
            RemixedAreaCount = areas.Length,
            RemixedKeyCount = remixedKeys.Count,
            RemixedTemplateCount = areas.Sum(area =>
                area.BundleSets.Sum(set => set.Templates.Length) +
                area.PoolTemplates.Length),
            RetainedStandardKeyCount = retainedStandardKeys.Length,
            IngredientAcquisitionIdentityCount = ingredientCatalog.Length,
            IngredientAcquisitionTargetCount = ingredientCatalog.Sum(value =>
                value.Targets.Length),
            StandardSupported = true,
            RemixedSupported = true,
            ActiveKeyTopologyComplete = topologyComplete,
            IngredientAcquisitionCatalogComplete = ingredientCatalogComplete,
            StandardActiveBundleKeys = activeStandardKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            SupplementalBundleKeys = supplementalKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            RetainedStandardBundleKeys = retainedStandardKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            StandardActiveTemplates = activeStandardKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(key => nativeTemplates[key])
                .ToArray(),
            SupplementalTemplates = supplementalKeys
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(key => nativeTemplates[key])
                .ToArray(),
            SupplementalAreaNames = supplementalKeys
                .Select(BundleAreaName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            RemixedAreas = areas,
            IngredientAcquisitionCatalog = ingredientCatalog
        };
    }

    private static CommunityCenterIngredientAcquisitionCatalogRow[]
        BuildCommunityCenterIngredientAcquisitionCatalog(
            IEnumerable<CommunityCenterTemplateIngredient> ingredients,
            JsonElement objects,
            IReadOnlyDictionary<string, RequirementAcquisitionRoute[]> routes)
    {
        return ingredients
            .GroupBy(value => (value.ItemIdOrCategory, value.QualifiedItemId,
                value.MatchKind))
            .Select(group =>
            {
                var identity = group.Key;
                CommunityCenterIngredientAcquisitionTarget[] targets;
                string displayName;
                if (identity.MatchKind == "money_payment")
                {
                    displayName = "Money";
                    targets = new[]
                    {
                        new CommunityCenterIngredientAcquisitionTarget(
                            "-1",
                            string.Empty,
                            displayName,
                            true,
                            new[]
                            {
                                new RequirementAcquisitionRoute(
                                    "native_money_payment",
                                    "money",
                                    "Data/Bundles",
                                    "dynamic_community_center_bundle")
                            })
                    };
                }
                else if (identity.MatchKind == "category")
                {
                    RequireCommunityCenter(int.TryParse(
                            identity.ItemIdOrCategory,
                            out var categoryId) && categoryId < 0,
                        "A Community Center category identity is invalid: " +
                        identity.ItemIdOrCategory);
                    displayName = "Object category " + identity.ItemIdOrCategory;
                    targets = objects.EnumerateObject()
                        .Where(item => item.Value.TryGetProperty(
                                "Category",
                                out var category) &&
                            category.ValueKind == JsonValueKind.Number &&
                            category.TryGetInt32(out var value) &&
                            value == categoryId)
                        .Select(item =>
                        {
                            var itemRoutes = RoutesFor(routes, item.Name);
                            return new CommunityCenterIngredientAcquisitionTarget(
                                item.Name,
                                "(O)" + item.Name,
                                String(item.Value, "Name"),
                                itemRoutes.Length > 0,
                                itemRoutes);
                        })
                        .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                        .ToArray();
                }
                else
                {
                    if (identity.MatchKind != "item_id" ||
                        !objects.TryGetProperty(identity.ItemIdOrCategory,
                            out var item))
                    {
                        throw new InvalidDataException(
                            "A Community Center item identity is invalid: " +
                            identity.ItemIdOrCategory);
                    }
                    displayName = String(item, "Name");
                    var itemRoutes = RoutesFor(routes, identity.ItemIdOrCategory);
                    targets = new[]
                    {
                        new CommunityCenterIngredientAcquisitionTarget(
                            identity.ItemIdOrCategory,
                            identity.QualifiedItemId,
                            displayName,
                            itemRoutes.Length > 0,
                            itemRoutes)
                    };
                }

                RequireCommunityCenter(targets.Length > 0,
                    "A Community Center ingredient identity has no native accepted target: " +
                    identity.ItemIdOrCategory);
                return new CommunityCenterIngredientAcquisitionCatalogRow
                {
                    ItemIdOrCategory = identity.ItemIdOrCategory,
                    QualifiedItemId = identity.QualifiedItemId,
                    MatchKind = identity.MatchKind,
                    DisplayName = displayName,
                    AcquisitionRouteComplete = targets.Any(value =>
                        value.RouteCovered),
                    Targets = targets
                };
            })
            .OrderBy(value => value.MatchKind, StringComparer.Ordinal)
            .ThenBy(value => value.ItemIdOrCategory, StringComparer.Ordinal)
            .ToArray();
    }

    private static CommunityCenterNativeBundleTemplate ParseNativeBundleTemplate(
        JsonProperty bundle)
    {
        RequireCommunityCenter(bundle.Value.ValueKind == JsonValueKind.String,
            "A Data/Bundles row is not a string: " + bundle.Name);
        var fields = bundle.Value.GetString()!.Split('/');
        RequireCommunityCenter(fields.Length >= 5 &&
                !string.IsNullOrWhiteSpace(fields[0]),
            "A Data/Bundles row is malformed: " + bundle.Name);
        var tokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        RequireCommunityCenter(tokens.Length > 0 && tokens.Length % 3 == 0,
            "A Data/Bundles ingredient list is malformed: " + bundle.Name);
        var ingredients = new List<CommunityCenterTemplateIngredient>();
        for (var index = 0; index < tokens.Length; index += 3)
        {
            if (!int.TryParse(tokens[index + 1], out var amount) || amount <= 0 ||
                !int.TryParse(tokens[index + 2], out var quality) || quality < 0)
            {
                throw new InvalidDataException(
                    "A Data/Bundles ingredient quantity or quality is invalid: " +
                    bundle.Name);
            }
            var itemId = tokens[index];
            ingredients.Add(new CommunityCenterTemplateIngredient(
                itemId,
                itemId == "-1" ? string.Empty : "(O)" + itemId,
                itemId == "-1"
                    ? "money_payment"
                    : itemId.StartsWith("-", StringComparison.Ordinal)
                        ? "category"
                        : "item_id",
                amount,
                quality));
        }
        var required = ingredients.Count;
        if (!string.IsNullOrWhiteSpace(fields[4]))
        {
            RequireCommunityCenter(int.TryParse(fields[4], out required) &&
                    required > 0 && required <= ingredients.Count,
                "A Data/Bundles required item count is invalid: " + bundle.Name);
        }
        return new CommunityCenterNativeBundleTemplate
        {
            BundleDataKey = bundle.Name,
            InternalName = fields[0],
            RequiredItemCount = required,
            Ingredients = ingredients.ToArray()
        };
    }

    private static CommunityCenterRemixedAreaCatalog ParseRemixedArea(
        JsonElement value,
        IReadOnlyDictionary<string, string> objectIdsByName,
        JsonElement objects)
    {
        var areaName = RequiredString(value, "AreaName");
        var keys = RequiredString(value, "Keys")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(key => int.TryParse(key, out var result) && result >= 0
                ? result
                : throw new InvalidDataException(
                    "A remixed Community Center key is invalid: " + key))
            .ToArray();
        RequireCommunityCenter(keys.Length > 0 && keys.Distinct().Count() == keys.Length,
            "A remixed Community Center area has duplicate or empty keys: " +
            areaName);

        var bundleSets = value.GetProperty("BundleSets")
            .EnumerateArray()
            .Select(set => new CommunityCenterRemixedBundleSet
            {
                SetId = RequiredString(set, "Id"),
                Templates = set.GetProperty("Bundles").EnumerateArray()
                    .Select(template => ParseBundleTemplate(
                        areaName,
                        "set:" + RequiredString(set, "Id"),
                        template,
                        objectIdsByName,
                        objects))
                    .OrderBy(template => template.Index)
                    .ThenBy(template => template.TemplateId, StringComparer.Ordinal)
                    .ToArray()
            })
            .OrderBy(set => set.SetId, StringComparer.Ordinal)
            .ToArray();
        foreach (var set in bundleSets)
        {
            RequireCommunityCenter(set.Templates.Length > 0,
                "A remixed Community Center bundle set is empty: " +
                areaName + ":" + set.SetId);
            RequireCommunityCenter(set.Templates.All(template =>
                    template.Index >= 0 && template.Index < keys.Length) &&
                set.Templates.Select(template => template.Index)
                    .Distinct().Count() == set.Templates.Length,
                "A remixed Community Center bundle set has invalid fixed indices: " +
                areaName + ":" + set.SetId);
        }

        var pool = value.GetProperty("Bundles").EnumerateArray()
            .Select(template => ParseBundleTemplate(
                areaName,
                "pool",
                template,
                objectIdsByName,
                objects))
            .OrderBy(template => template.Index)
            .ThenBy(template => template.TemplateId, StringComparer.Ordinal)
            .ToArray();
        RequireCommunityCenter(pool.Length > 0,
            "A remixed Community Center area has no pool templates: " + areaName);
        RequireCommunityCenter(pool.All(template =>
                template.Index == -1 ||
                template.Index >= 0 && template.Index < keys.Length),
            "A remixed Community Center pool template has an invalid index: " +
            areaName);

        return new CommunityCenterRemixedAreaCatalog
        {
            AreaName = areaName,
            KeyIds = keys,
            BundleSets = bundleSets,
            PoolTemplates = pool
        };
    }

    private static CommunityCenterBundleTemplate ParseBundleTemplate(
        string areaName,
        string source,
        JsonElement value,
        IReadOnlyDictionary<string, string> objectIdsByName,
        JsonElement objects)
    {
        var id = RequiredString(value, "Id");
        var internalName = RequiredString(value, "Name");
        var index = value.GetProperty("Index").GetInt32();
        var slots = SplitTopLevel(
                RequiredString(value, "Items"),
                ',')
            .Select(slot => new CommunityCenterTemplateIngredientSlot
            {
                Options = ExpandRandomTags(slot)
                    .Select(option => ParseTemplateIngredient(
                        option,
                        objectIdsByName,
                        objects))
                    .Distinct()
                    .ToArray()
            })
            .ToArray();
        RequireCommunityCenter(slots.Length > 0 && slots.All(slot => slot.Options.Length > 0),
            "A remixed Community Center template has no ingredients: " + id);

        var requestedPick = value.GetProperty("Pick").GetInt32();
        var pick = requestedPick < 0 ? slots.Length : requestedPick;
        var requestedRequired = value.GetProperty("RequiredItems").GetInt32();
        var required = requestedRequired < 0 ? pick : requestedRequired;
        RequireCommunityCenter(pick > 0 && pick <= slots.Length && required > 0 &&
                required <= pick,
            "A remixed Community Center template has invalid pick/required counts: " +
            id);

        return new CommunityCenterBundleTemplate
        {
            TemplateId = areaName + ":" + source + ":" + id,
            InternalName = internalName,
            Index = index,
            PickCount = pick,
            RequiredItemCount = required,
            IngredientSlots = slots
        };
    }

    private static CommunityCenterTemplateIngredient ParseTemplateIngredient(
        string raw,
        IReadOnlyDictionary<string, string> objectIdsByName,
        JsonElement objects)
    {
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !int.TryParse(parts[0], out var amount) ||
            amount <= 0)
        {
            throw new InvalidDataException(
                "A remixed Community Center ingredient amount is invalid: " + raw);
        }
        var index = 1;
        var quality = 0;
        if (parts[index] is "NQ" or "SQ" or "GQ" or "IQ")
        {
            quality = parts[index] switch
            {
                "NQ" => 0,
                "SQ" => 1,
                "GQ" => 2,
                "IQ" => 3,
                _ => 0
            };
            index++;
        }
        RequireCommunityCenter(index < parts.Length,
            "A remixed Community Center ingredient identity is missing: " + raw);
        var identity = string.Join(' ', parts[index..]);
        if (CommunityCenterCategoryIds.TryGetValue(identity, out var categoryId))
        {
            return new CommunityCenterTemplateIngredient(
                categoryId,
                string.Empty,
                "category",
                amount,
                quality);
        }

        string itemId;
        if (int.TryParse(identity, out _))
        {
            itemId = identity;
        }
        else if (objects.TryGetProperty(identity, out _))
        {
            itemId = identity;
        }
        else
        {
            var lookupName = CommunityCenterItemNameAliases
                .GetValueOrDefault(identity, identity);
            RequireCommunityCenter(objectIdsByName.TryGetValue(lookupName, out itemId!),
                "A remixed Community Center ingredient name is unresolved: " +
                identity);
        }
        return new CommunityCenterTemplateIngredient(
            itemId,
            "(O)" + itemId,
            "item_id",
            amount,
            quality);
    }

    private static Dictionary<string, string> BuildObjectIdsByName(
        JsonElement objects)
    {
        var candidates = new Dictionary<string, List<string>>(
            StringComparer.Ordinal);
        foreach (var item in objects.EnumerateObject())
        {
            var name = String(item.Value, "Name");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (!candidates.TryGetValue(name, out var values))
            {
                values = new List<string>();
                candidates.Add(name, values);
            }
            values.Add(item.Name);
        }
        var result = candidates.ToDictionary(
            value => value.Key,
            value => value.Value[0],
            StringComparer.Ordinal);
        if (!objects.TryGetProperty("390", out var stone) ||
            !string.Equals(String(stone, "Name"), "Stone",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Data/Objects does not contain the native Stone object 390.");
        }
        result["Stone"] = "390";
        return result;
    }

    private static string[] ExpandRandomTags(string raw)
    {
        var start = raw.LastIndexOf('[');
        if (start < 0)
            return new[] { raw.Trim() };
        var end = raw.IndexOf(']', start);
        RequireCommunityCenter(end > start,
            "A remixed Community Center random tag is malformed: " + raw);
        var prefix = raw[..start];
        var suffix = raw[(end + 1)..];
        return raw[(start + 1)..end]
            .Split('|', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .SelectMany(option => ExpandRandomTags(prefix + option + suffix))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] SplitTopLevel(string value, char separator)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            depth += value[index] switch
            {
                '[' => 1,
                ']' => -1,
                _ => 0
            };
            RequireCommunityCenter(depth >= 0,
                "A remixed Community Center item list has an unmatched bracket.");
            if (value[index] != separator || depth != 0)
                continue;
            result.Add(value[start..index].Trim());
            start = index + 1;
        }
        RequireCommunityCenter(depth == 0,
            "A remixed Community Center item list has an unmatched bracket.");
        result.Add(value[start..].Trim());
        return result.Where(part => part.Length > 0).ToArray();
    }

    private static string BundleAreaName(string key)
    {
        var separator = key.LastIndexOf('/');
        RequireCommunityCenter(separator > 0 && separator < key.Length - 1,
            "A Community Center bundle key is malformed: " + key);
        return key[..separator];
    }

    private static void GuardCommunityCenterDenominatorSources(
        string bundleGeneratorPath,
        string game1Path,
        string saveGamePath,
        string utilityPath)
    {
        var generator = File.ReadAllText(bundleGeneratorPath);
        RequireContains(generator,
            "new Dictionary<string, string>(DataLoader.Bundles(Game1.content))",
            bundleGeneratorPath);
        RequireContains(generator,
            "BundleSetData bundleSetData = random.ChooseFrom(randomBundleDatum.BundleSets)",
            bundleGeneratorPath);
        RequireContains(generator,
            "while (list.Count > pick_count)",
            bundleGeneratorPath);
        RequireContains(generator,
            "string value = random.ChooseFrom(text.Split('|'))",
            bundleGeneratorPath);

        var game = File.ReadAllText(game1Path);
        RequireContains(game,
            "new BundleGenerator().Generate(DataLoader.RandomBundles(content), rng)",
            game1Path);
        RequireContains(game,
            "netWorldState.Value.SetBundleData(DataLoader.Bundles(content))",
            game1Path);

        var save = File.ReadAllText(saveGamePath);
        RequireContains(save,
            "Game1.netWorldState.Value.SetBundleData(dictionary)",
            saveGamePath);

        var utility = File.ReadAllText(utilityPath);
        RequireContains(utility,
            "if (!dictionary.ContainsKey(key))",
            utilityPath);
        RequireContains(utility,
            "dictionary[key2] = \"(O)390\"",
            utilityPath);
    }

    private static void RequireCommunityCenter(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
