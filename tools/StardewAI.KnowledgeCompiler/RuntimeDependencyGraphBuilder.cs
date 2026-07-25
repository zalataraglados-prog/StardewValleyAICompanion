using System.Globalization;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class RuntimeDependencyGraphBuilder
{
    private readonly List<GraphNode> nodes = new();
    private readonly List<GraphEdge> edges = new();

    public RuntimeDependencyGraph Build(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        AddTopLevelNodes(assets);
        AddCropEdges(assets);
        AddMachineEdges(assets);
        AddShopEdges(assets);
        AddBuildingEdges(assets);
        AddSpecialOrderEdges(assets);
        AddBundleEdges(assets);
        AddLegacyRecipeEdges(assets, "Data/CookingRecipes", "cooking_recipe", outputSegment: 2, unlockSegment: 3);
        AddLegacyRecipeEdges(assets, "Data/CraftingRecipes", "crafting_recipe", outputSegment: 2, unlockSegment: 4);
        AddReferencedEntityNodes();

        return new(
            "stardewai.runtime_dependency_graph.v1",
            "runtime-loaded game content",
            "Only directly encoded relationships are edges. Conditions and custom methods are joined in authoritative-dependency-graph.json.",
            nodes.OrderBy(row => row.Id, StringComparer.Ordinal).ToArray(),
            edges.OrderBy(row => row.From, StringComparer.Ordinal)
                .ThenBy(row => row.To, StringComparer.Ordinal)
                .ThenBy(row => row.Kind, StringComparer.Ordinal)
                .ToArray());
    }

    private void AddReferencedEntityNodes()
    {
        var known = nodes.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            AddReference(edge.From, edge.SourceAsset, edge.SourcePath, known);
            AddReference(edge.To, edge.SourceAsset, edge.SourcePath, known);
        }
    }

    private void AddReference(
        string id,
        string sourceAsset,
        string sourcePath,
        ISet<string> known)
    {
        if (!known.Add(id))
            return;

        var separator = id.IndexOf(':');
        var prefix = separator > 0 ? id[..separator] : string.Empty;
        var key = separator > 0 ? id[(separator + 1)..] : id;
        var kind = prefix switch
        {
            "item" => "item_reference",
            "context_tag" => "context_tag_reference",
            "code_method" => "code_method_reference",
            "currency" => "currency",
            "item_category" => "item_category",
            _ => "dependency_reference"
        };
        nodes.Add(new(
            id,
            kind,
            sourceAsset,
            key,
            new Dictionary<string, object?>
            {
                ["reference_origin"] = sourcePath,
                ["identity_status"] = "runtime_data_reference"
            }));
    }

    private void AddTopLevelNodes(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        foreach (var asset in assets.Values.OrderBy(row => row.AssetName, StringComparer.OrdinalIgnoreCase))
        {
            if (asset.Payload.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in asset.Payload.EnumerateObject())
                    AddNode(EntryId(asset.AssetName, property.Name), "runtime_asset_entry", asset.AssetName, property.Name);
            }
            else if (asset.Payload.ValueKind == JsonValueKind.Array)
            {
                for (var index = 0; index < asset.Payload.GetArrayLength(); index++)
                    AddNode(EntryId(asset.AssetName, index.ToString(CultureInfo.InvariantCulture)), "runtime_asset_entry", asset.AssetName, index.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                AddNode(EntryId(asset.AssetName, "value"), "runtime_asset_value", asset.AssetName, "value");
            }
        }
    }

    private void AddCropEdges(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        if (!TryObject(assets, "Data/Crops", out var crops)) return;
        foreach (var crop in crops.EnumerateObject())
        {
            var cropId = "crop:" + crop.Name;
            var value = crop.Value;
            var harvest = String(value, "HarvestItemId");
            var days = value.TryGetProperty("DaysInPhase", out var phases) && phases.ValueKind == JsonValueKind.Array
                ? phases.EnumerateArray().Where(row => row.TryGetInt32(out _)).Sum(row => row.GetInt32())
                : 0;
            AddNode(cropId, "crop", "Data/Crops", crop.Name, new Dictionary<string, object?>
            {
                ["seed_item_id"] = crop.Name,
                ["harvest_item_id"] = harvest,
                ["base_growth_days"] = days,
                ["regrow_days"] = Int(value, "RegrowDays"),
                ["seasons"] = value.TryGetProperty("Seasons", out var seasons) ? seasons.Clone() : null
            });
            AddEdge(Item(crop.Name), cropId, "plants_as", "Data/Crops", $"payload.{crop.Name}");
            if (!string.IsNullOrWhiteSpace(harvest))
                AddEdge(cropId, Item(harvest), "harvests_as", "Data/Crops", $"payload.{crop.Name}.HarvestItemId");
        }
    }

    private void AddMachineEdges(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        if (!TryObject(assets, "Data/Machines", out var machines)) return;
        foreach (var machine in machines.EnumerateObject())
        {
            var machineId = "machine:" + machine.Name;
            AddNode(machineId, "machine", "Data/Machines", machine.Name);
            if (!machine.Value.TryGetProperty("OutputRules", out var rules) || rules.ValueKind != JsonValueKind.Array) continue;
            var ruleIndex = 0;
            foreach (var rule in rules.EnumerateArray())
            {
                var ruleId = machineId + ":rule:" + (String(rule, "Id") ?? ruleIndex.ToString(CultureInfo.InvariantCulture));
                AddNode(ruleId, "machine_rule", "Data/Machines", machine.Name, new Dictionary<string, object?>
                {
                    ["minutes_until_ready"] = Int(rule, "MinutesUntilReady"),
                    ["days_until_ready"] = Int(rule, "DaysUntilReady"),
                    ["condition"] = String(rule, "Condition")
                });
                AddEdge(machineId, ruleId, "has_output_rule", "Data/Machines", $"payload.{machine.Name}.OutputRules[{ruleIndex}]");
                AddMachineTriggers(machine.Name, ruleIndex, ruleId, rule);
                AddMachineOutputs(machine.Name, ruleIndex, ruleId, rule);
                ruleIndex++;
            }
        }
    }

    private void AddMachineTriggers(string machineKey, int ruleIndex, string ruleId, JsonElement rule)
    {
        if (!rule.TryGetProperty("Triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array) return;
        var triggerIndex = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            var requiredItem = String(trigger, "RequiredItemId");
            if (!string.IsNullOrWhiteSpace(requiredItem))
            {
                AddEdge(Item(requiredItem), ruleId, "machine_input", "Data/Machines",
                    $"payload.{machineKey}.OutputRules[{ruleIndex}].Triggers[{triggerIndex}].RequiredItemId",
                    new Dictionary<string, object?> { ["required_count"] = Int(trigger, "RequiredCount"), ["condition"] = String(trigger, "Condition") });
            }
            if (trigger.TryGetProperty("RequiredTags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tags.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.String))
                    AddEdge("context_tag:" + tag.GetString(), ruleId, "machine_input_tag", "Data/Machines",
                        $"payload.{machineKey}.OutputRules[{ruleIndex}].Triggers[{triggerIndex}].RequiredTags");
            }
            triggerIndex++;
        }
    }

    private void AddMachineOutputs(string machineKey, int ruleIndex, string ruleId, JsonElement rule)
    {
        if (!rule.TryGetProperty("OutputItem", out var outputs) || outputs.ValueKind != JsonValueKind.Array) return;
        var outputIndex = 0;
        foreach (var output in outputs.EnumerateArray())
        {
            var itemId = String(output, "ItemId");
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                AddEdge(ruleId, Item(itemId), "machine_output", "Data/Machines",
                    $"payload.{machineKey}.OutputRules[{ruleIndex}].OutputItem[{outputIndex}].ItemId",
                    new Dictionary<string, object?> { ["min_stack"] = Int(output, "MinStack"), ["max_stack"] = Int(output, "MaxStack"), ["condition"] = String(output, "Condition") });
            }
            else if (!string.IsNullOrWhiteSpace(String(output, "OutputMethod")))
            {
                AddEdge(ruleId, "code_method:" + String(output, "OutputMethod"), "dynamic_machine_output", "Data/Machines",
                    $"payload.{machineKey}.OutputRules[{ruleIndex}].OutputItem[{outputIndex}].OutputMethod",
                    new Dictionary<string, object?> { ["semantic_status"] = "requires_decompiled_method" });
            }
            outputIndex++;
        }
    }

    private void AddShopEdges(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        if (!TryObject(assets, "Data/Shops", out var shops)) return;
        foreach (var shop in shops.EnumerateObject())
        {
            var shopId = "shop:" + shop.Name;
            AddNode(shopId, "shop", "Data/Shops", shop.Name);
            if (!shop.Value.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array) continue;
            var index = 0;
            foreach (var stock in items.EnumerateArray())
            {
                var itemId = String(stock, "ItemId");
                if (!string.IsNullOrWhiteSpace(itemId))
                {
                    AddEdge(shopId, Item(itemId), "sells", "Data/Shops", $"payload.{shop.Name}.Items[{index}]",
                        new Dictionary<string, object?>
                        {
                            ["price"] = Int(stock, "Price"),
                            ["condition"] = String(stock, "Condition"),
                            ["available_stock"] = Int(stock, "AvailableStock"),
                            ["trade_item_id"] = String(stock, "TradeItemId"),
                            ["trade_item_amount"] = Int(stock, "TradeItemAmount")
                        });
                }
                index++;
            }
        }
    }

    private void AddBuildingEdges(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        if (!TryObject(assets, "Data/Buildings", out var buildings)) return;
        foreach (var building in buildings.EnumerateObject())
        {
            var buildingId = "building:" + building.Name;
            AddNode(buildingId, "building", "Data/Buildings", building.Name, new Dictionary<string, object?>
            {
                ["builder"] = String(building.Value, "Builder"),
                ["build_condition"] = String(building.Value, "BuildCondition"),
                ["build_days"] = Int(building.Value, "BuildDays"),
                ["build_cost"] = Int(building.Value, "BuildCost"),
                ["building_to_upgrade"] = String(building.Value, "BuildingToUpgrade")
            });
            if (!building.Value.TryGetProperty("BuildMaterials", out var materials) || materials.ValueKind != JsonValueKind.Array) continue;
            var index = 0;
            foreach (var material in materials.EnumerateArray())
            {
                var itemId = String(material, "ItemId");
                if (!string.IsNullOrWhiteSpace(itemId))
                    AddEdge(Item(itemId), buildingId, "building_material", "Data/Buildings", $"payload.{building.Name}.BuildMaterials[{index}]",
                        new Dictionary<string, object?> { ["amount"] = Int(material, "Amount") });
                index++;
            }
        }
    }

    private void AddSpecialOrderEdges(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        if (!TryObject(assets, "Data/SpecialOrders", out var orders)) return;
        foreach (var order in orders.EnumerateObject())
        {
            var orderId = "special_order:" + order.Name;
            AddNode(orderId, "special_order", "Data/SpecialOrders", order.Name, new Dictionary<string, object?>
            {
                ["requester"] = String(order.Value, "Requester"),
                ["duration"] = Int(order.Value, "Duration"),
                ["condition"] = String(order.Value, "Condition"),
                ["required_tags"] = String(order.Value, "RequiredTags"),
                ["repeatable"] = Bool(order.Value, "Repeatable")
            });
            AddOrderRows(order.Name, orderId, order.Value, "Objectives", "objective");
            AddOrderRows(order.Name, orderId, order.Value, "Rewards", "reward");
        }
    }

    private void AddOrderRows(string orderKey, string orderId, JsonElement order, string propertyName, string kind)
    {
        if (!order.TryGetProperty(propertyName, out var rows) || rows.ValueKind != JsonValueKind.Array) return;
        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var rowId = orderId + ":" + kind + ":" + index;
            AddNode(rowId, "special_order_" + kind, "Data/SpecialOrders", orderKey, new Dictionary<string, object?>
            {
                ["type"] = String(row, "Type"),
                ["required_count"] = String(row, "RequiredCount"),
                ["data"] = row.TryGetProperty("Data", out var data) ? data.Clone() : null
            });
            AddEdge(kind == "objective" ? rowId : orderId, kind == "objective" ? orderId : rowId,
                kind == "objective" ? "required_by_order" : "rewards", "Data/SpecialOrders", $"payload.{orderKey}.{propertyName}[{index}]");
            index++;
        }
    }

    private void AddBundleEdges(IReadOnlyDictionary<string, PayloadAsset> assets)
    {
        if (!TryObject(assets, "Data/Bundles", out var bundles)) return;
        foreach (var bundle in bundles.EnumerateObject())
        {
            if (bundle.Value.ValueKind != JsonValueKind.String) continue;
            var raw = bundle.Value.GetString() ?? string.Empty;
            var segments = raw.Split('/');
            if (segments.Length < 7) continue;
            var bundleId = "bundle:" + bundle.Name;
            var keyParts = bundle.Name.Split('/', 2);
            var ingredients = Tokens(segments[2]);
            var requiredSlots = int.TryParse(segments[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slots)
                ? slots
                : ingredients.Length / 3;
            AddNode(bundleId, "bundle", "Data/Bundles", bundle.Name, new Dictionary<string, object?>
            {
                ["area"] = keyParts[0],
                ["bundle_index"] = keyParts.Length > 1 ? keyParts[1] : string.Empty,
                ["name"] = segments[0],
                ["reward_description"] = segments[1],
                ["color"] = int.TryParse(segments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var color) ? color : null,
                ["required_ingredient_slots"] = requiredSlots,
                ["sprite_override"] = segments[5],
                ["label"] = segments[6],
                ["raw_definition"] = raw
            });
            for (var index = 0; index + 2 < ingredients.Length; index += 3)
            {
                var ingredientId = ingredients[index];
                var attributes = new Dictionary<string, object?>
                {
                    ["amount"] = int.TryParse(ingredients[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ? amount : null,
                    ["quality"] = int.TryParse(ingredients[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quality) ? quality : null,
                    ["required_slots"] = requiredSlots
                };
                if (ingredientId == "-1")
                {
                    AddNode("currency:money", "currency", "Data/Bundles", "money");
                    AddEdge("currency:money", bundleId, "bundle_currency_payment", "Data/Bundles",
                        $"payload.{bundle.Name}[ingredients:{index / 3}]", attributes);
                }
                else if (int.TryParse(ingredientId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var category) && category < 0)
                {
                    var categoryId = "item_category:" + ingredientId;
                    AddNode(categoryId, "item_category", "Data/Bundles", ingredientId);
                    AddEdge(categoryId, bundleId, "bundle_ingredient_category", "Data/Bundles",
                        $"payload.{bundle.Name}[ingredients:{index / 3}]", attributes);
                }
                else
                {
                    AddEdge(Item(ingredientId), bundleId, "bundle_ingredient", "Data/Bundles",
                        $"payload.{bundle.Name}[ingredients:{index / 3}]", attributes);
                }
            }
            var reward = Tokens(segments[1]);
            if (reward.Length >= 3)
            {
                var rewardId = bundleId + ":reward";
                var qualifiedItemId = QualifyStandardDescription(reward[0], reward[1]);
                AddNode(rewardId, "standard_item_description", "Data/Bundles", bundle.Name,
                    new Dictionary<string, object?>
                    {
                        ["raw"] = segments[1],
                        ["type"] = reward[0],
                        ["item_id"] = reward[1],
                        ["qualified_item_id"] = qualifiedItemId,
                        ["amount"] = int.TryParse(reward[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rewardAmount)
                            ? rewardAmount
                            : null,
                        ["native_consumer"] = "Utility.getItemFromStandardTextDescription"
                    });
                AddEdge(bundleId, rewardId, "bundle_reward_description", "Data/Bundles",
                    $"payload.{bundle.Name}[reward]",
                    new Dictionary<string, object?>());
                if (qualifiedItemId is not null)
                    AddEdge(rewardId, Item(qualifiedItemId), "creates_reward_item", "Data/Bundles",
                        $"payload.{bundle.Name}[reward]");
            }
        }
    }

    private void AddLegacyRecipeEdges(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        string assetName,
        string kind,
        int outputSegment,
        int unlockSegment)
    {
        if (!TryObject(assets, assetName, out var recipes)) return;
        foreach (var recipe in recipes.EnumerateObject())
        {
            if (recipe.Value.ValueKind != JsonValueKind.String) continue;
            var segments = (recipe.Value.GetString() ?? string.Empty).Split('/');
            if (segments.Length <= outputSegment) continue;
            var recipeId = kind + ":" + recipe.Name;
            AddNode(recipeId, kind, assetName, recipe.Name, new Dictionary<string, object?>
            {
                ["raw_definition"] = recipe.Value.GetString(),
                ["unlock_segment"] = segments.Length > unlockSegment ? segments[unlockSegment] : string.Empty,
                ["big_craftable"] = kind == "crafting_recipe" &&
                                    segments.Length > 3 &&
                                    bool.TryParse(segments[3], out var bigCraftable) &&
                                    bigCraftable
            });
            foreach (var pair in ParsePairs(segments[0]))
            {
                if (int.TryParse(pair.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var category) && category < 0)
                {
                    var requirementId = recipeId + ":ingredient:" + pair.Id;
                    AddNode(requirementId, "recipe_ingredient_matcher", assetName, recipe.Name,
                        new Dictionary<string, object?>
                        {
                            ["raw_match_key"] = pair.Id,
                            ["amount"] = pair.Count,
                            ["native_matcher"] = "CraftingRecipe.ItemMatchesForCrafting",
                            ["match_kind"] = pair.Id == "-777" ? "special_ingredient_rule" : "item_category"
                        });
                    AddEdge(requirementId, recipeId, "recipe_ingredient_requirement", assetName,
                        $"payload.{recipe.Name}[ingredients]");
                    if (pair.Id == "-777")
                    {
                        foreach (var seasonalSeed in new[] { "495", "496", "497", "498" })
                            AddEdge(Item(seasonalSeed), requirementId, "matches_special_ingredient_rule", assetName,
                                $"payload.{recipe.Name}[ingredients]");
                    }
                    else
                    {
                        var categoryId = "item_category:" + pair.Id;
                        AddNode(categoryId, "item_category", assetName, pair.Id);
                        AddEdge(categoryId, requirementId, "matches_item_category", assetName,
                            $"payload.{recipe.Name}[ingredients]");
                    }
                }
                else
                {
                    AddEdge(Item(pair.Id), recipeId, "recipe_ingredient", assetName, $"payload.{recipe.Name}[ingredients]",
                        new Dictionary<string, object?>
                        {
                            ["amount"] = pair.Count,
                            ["native_matcher"] = "CraftingRecipe.ItemMatchesForCrafting"
                        });
                }
            }
            var outputTokens = Tokens(segments[outputSegment]);
            var numberProducedPerCraft = outputTokens.Length % 2 == 0 &&
                                         int.TryParse(outputTokens[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var finalCount)
                ? finalCount
                : 1;
            for (var index = 0; index < outputTokens.Length; index += 2)
            {
                AddEdge(recipeId, Item(outputTokens[index]), "recipe_output", assetName,
                    $"payload.{recipe.Name}[output:{index / 2}]",
                    new Dictionary<string, object?>
                    {
                        ["amount"] = numberProducedPerCraft,
                        ["selection"] = outputTokens.Length > 2 ? "Game1.random.ChooseFrom(itemToProduce)" : "single",
                        ["native_amount_semantics"] = "CraftingRecipe.numberProducedPerCraft is overwritten for each pair; the final pair count applies to the selected output"
                    });
            }
            if (segments.Length > unlockSegment)
            {
                var unlock = segments[unlockSegment];
                var unlockId = recipeId + ":unlock";
                AddNode(unlockId, "recipe_unlock_requirement", assetName, recipe.Name,
                    new Dictionary<string, object?>
                    {
                        ["raw"] = unlock,
                        ["tokens"] = Tokens(unlock),
                        ["native_consumers"] = kind == "cooking_recipe"
                            ? "Farmer.LearnDefaultRecipes, Farmer.AddMissedMailAndRecipes, LevelUpMenu, Stats.checkForCookingAchievements"
                            : "Farmer.LearnDefaultRecipes, Farmer.AddMissedMailAndRecipes, LevelUpMenu, Stats.checkForCookingAchievements"
                    });
                AddEdge(unlockId, recipeId, "unlocks_recipe", assetName,
                    $"payload.{recipe.Name}[unlock]");
            }
        }
    }

    private static IEnumerable<(string Id, int Count)> ParsePairs(string value)
    {
        var tokens = Tokens(value);
        for (var index = 0; index + 1 < tokens.Length; index += 2)
            if (int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                yield return (tokens[index], count);
    }

    private static string[] Tokens(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? QualifyStandardDescription(string type, string itemId) =>
        type switch
        {
            "O" or "Object" or "R" or "Ring" or "BL" or "Blueprint" => "(O)" + itemId,
            "BO" or "BigObject" or "BBL" or "BBl" or "BigBlueprint" => "(BC)" + itemId,
            "F" or "Furniture" => "(F)" + itemId,
            "B" or "Boot" => "(B)" + itemId,
            "W" or "Weapon" => "(W)" + itemId,
            "H" or "Hat" => "(H)" + itemId,
            "C" when int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId) =>
                (numericId >= 1000 ? "(S)" : "(P)") + itemId,
            "C" => itemId,
            _ => null
        };

    private static bool TryObject(IReadOnlyDictionary<string, PayloadAsset> assets, string name, out JsonElement payload)
    {
        if (assets.TryGetValue(name, out var asset) && asset.Payload.ValueKind == JsonValueKind.Object)
        {
            payload = asset.Payload;
            return true;
        }
        payload = default;
        return false;
    }

    private void AddNode(string id, string kind, string asset, string key, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        nodes.RemoveAll(row => row.Id == id && row.Kind == "runtime_asset_entry");
        if (nodes.Any(row => row.Id == id)) return;
        nodes.Add(new(id, kind, asset, key, attributes ?? new Dictionary<string, object?>()));
    }

    private void AddEdge(string from, string to, string kind, string asset, string path, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        edges.Add(new(from, to, kind, asset, path, attributes ?? new Dictionary<string, object?>()));
    }

    private static string EntryId(string asset, string key) => "asset:" + asset + "#" + key;
    private static string Item(string? id) => "item:" + (id ?? string.Empty);
    private static string? String(JsonElement value, string property) => value.TryGetProperty(property, out var row) && row.ValueKind == JsonValueKind.String ? row.GetString() : null;
    private static int? Int(JsonElement value, string property) => value.TryGetProperty(property, out var row) && row.TryGetInt32(out var number) ? number : null;
    private static bool? Bool(JsonElement value, string property) => value.TryGetProperty(property, out var row) && row.ValueKind is JsonValueKind.True or JsonValueKind.False ? row.GetBoolean() : null;
}

internal sealed record RuntimeDependencyGraph(
    string SchemaVersion,
    string Authority,
    string SemanticLimit,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges)
{
    public int NodeCount => Nodes.Count;
    public int EdgeCount => Edges.Count;
}
