using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Network;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadCookingContext(Farmer? player)
    {
        if (player is null || CraftingRecipe.cookingRecipes is null)
        {
            return new
            {
                projection_status = "unavailable_world_or_cooking_catalog",
                learned_recipe_count = 0,
                source_count = 0,
                rows = Array.Empty<object>()
            };
        }
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                learned_recipe_count = player.cookingRecipes.Count(),
                source_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var sources = ReadCookingSources(player);
        var rows = new List<object>();
        var failures = new List<object>();
        foreach (var recipeName in player.cookingRecipes.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!CraftingRecipe.cookingRecipes.TryGetValue(recipeName, out var rawRecipe))
            {
                failures.Add(new { recipe_name = recipeName, reason = "learned_recipe_missing_cooking_data_entry" });
                continue;
            }

            CraftingRecipe recipe;
            Item output;
            try
            {
                recipe = new CraftingRecipe(recipeName, isCookingRecipe: true);
                output = recipe.createItem();
            }
            catch (Exception ex)
            {
                failures.Add(new { recipe_name = recipeName, reason = "cooking_recipe_parse_or_output_failed:" + ex.GetType().Name });
                continue;
            }

            var deterministicOutput = recipe.itemToProduce.Count == 1;
            foreach (var source in sources)
            {
                var projection = ProjectCookingIngredients(recipe, player, source);
                var projectedOutput = output.getOne();
                projectedOutput.Stack = recipe.numberProducedPerCraft;
                projectedOutput.Quality = projection.SeasoningConsumed ? 2 : output.Quality;
                var acceptsOutput = projection.Satisfied &&
                    InventoryAcceptsAfterConsumption(player.Items, projection.PlayerRemaining, projectedOutput);
                var lockedByOther = source.Mutexes.Any(mutex => mutex.IsLocked() && !mutex.IsLockHeld());
                var ready = deterministicOutput && projection.Satisfied && acceptsOutput && !lockedByOther;
                rows.Add(new
                {
                    recipe_name = recipe.name,
                    raw_recipe_data = rawRecipe,
                    known_recipe = true,
                    cooking_source_id = source.SourceId,
                    cooking_source_kind = source.Kind,
                    location_id = source.Location.NameOrUniqueName,
                    interaction_tile_x = source.Tile.X,
                    interaction_tile_y = source.Tile.Y,
                    material_container_ids = source.ContainerIds,
                    material_container_count = source.Containers.Count,
                    material_container_topology_json = JsonSerializer.Serialize(source.ContainerIds),
                    mutex_count = source.Mutexes.Count,
                    mutex_locked_by_other = lockedByOther,
                    output_selection_status = deterministicOutput
                        ? "exact_single_output"
                        : "blocked_multiple_or_random_outputs",
                    output_item_id = output.ItemId,
                    output_qualified_item_id = output.QualifiedItemId,
                    output_display_name = output.DisplayName,
                    output_count_per_craft = recipe.numberProducedPerCraft,
                    output_quality = projectedOutput.Quality,
                    qi_cooking_rule_active = player.team.SpecialOrderRuleActive("QI_COOKING"),
                    output_order_data = output is StardewValley.Object obj ? obj.orderData.Value ?? string.Empty : string.Empty,
                    recipes_cooked_before = player.recipesCooked.TryGetValue(output.ItemId, out var cooked) ? cooked : 0,
                    ingredient_rows = projection.IngredientRows,
                    ingredient_rows_json = JsonSerializer.Serialize(projection.IngredientRows),
                    seasoning_consumed = projection.SeasoningConsumed,
                    seasoning_rows = projection.SeasoningRows,
                    seasoning_rows_json = JsonSerializer.Serialize(projection.SeasoningRows),
                    output_inventory_acceptance_after_material_consumption = acceptsOutput,
                    craft_candidate_status = ready
                        ? "ready_for_native_cooking_page"
                        : !deterministicOutput
                            ? "blocked_non_deterministic_output"
                            : lockedByOther
                                ? "blocked_native_material_mutex_locked_by_other"
                                : !projection.Satisfied
                                    ? "blocked_insufficient_native_materials"
                                    : "blocked_output_cannot_fit_after_material_consumption",
                    native_contract = source.Kind == "kitchen"
                        ? "GameLocation.checkAction(kitchen)->ActivateKitchen->MultipleMutexRequest->CraftingPage(cooking:true)->clickCraftingRecipe"
                        : "Torch.checkForAction((BC)278)->CraftingPage(cooking:true)->clickCraftingRecipe"
                });
            }
        }

        return new
        {
            projection_status = failures.Count == 0
                ? "complete_learned_cooking_recipe_and_native_source_projection"
                : "partial_unclassified_learned_cooking_recipes",
            learned_recipe_count = player.cookingRecipes.Count(),
            source_count = sources.Length,
            row_count = rows.Count,
            unclassified_recipe_count = failures.Count,
            unclassified_recipes = failures.ToArray(),
            consumption_order = "player_inventory_reverse_slots_then_kitchen_main_fridge_then_native_object_enumeration_mini_fridges_reverse_slots_then_qi_seasoning",
            rows = rows.ToArray()
        };
    }

    private static CookingSource[] ReadCookingSources(Farmer player)
    {
        var farm = Game1.getFarm();
        if (farm is null)
        {
            return Array.Empty<CookingSource>();
        }

        var rows = new List<CookingSource>();
        foreach (var locationRef in MachineLocationTopology.ReadPersistentLocations(farm, player))
        {
            var location = locationRef.Location;
            var buildings = location.Map?.GetLayer("Buildings");
            if (buildings is not null)
            {
                for (var y = 0; y < buildings.LayerHeight; y++)
                for (var x = 0; x < buildings.LayerWidth; x++)
                {
                    var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                    if (!string.Equals(action, "kitchen", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var containers = new List<IInventory>();
                    var containerIds = new List<string>();
                    var mutexes = new List<NetMutex>();
                    var fridge = location.GetFridge();
                    if (fridge is not null)
                    {
                        containers.Add(fridge.Items);
                        containerIds.Add("kitchen-fridge:" + location.NameOrUniqueName);
                        mutexes.Add(fridge.mutex);
                    }
                    foreach (var pair in location.objects.Pairs)
                    {
                        if (pair.Value is not Chest chest || !chest.bigCraftable.Value || !chest.fridge.Value)
                        {
                            continue;
                        }
                        containers.Add(chest.Items);
                        containerIds.Add("mini-fridge:" + location.NameOrUniqueName + ":" + (int)pair.Key.X + "," + (int)pair.Key.Y);
                        mutexes.Add(chest.mutex);
                    }
                    rows.Add(new CookingSource(
                        "kitchen:" + location.NameOrUniqueName + ":" + x + "," + y,
                        "kitchen",
                        location,
                        new Point(x, y),
                        containers,
                        containerIds,
                        mutexes));
                }
            }

            foreach (var pair in location.objects.Pairs)
            {
                if (pair.Value is Torch && string.Equals(pair.Value.QualifiedItemId, "(BC)278", StringComparison.Ordinal))
                {
                    rows.Add(new CookingSource(
                        "cookout:" + location.NameOrUniqueName + ":" + (int)pair.Key.X + "," + (int)pair.Key.Y,
                        "cookout_kit",
                        location,
                        new Point((int)pair.Key.X, (int)pair.Key.Y),
                        Array.Empty<IInventory>(),
                        Array.Empty<string>(),
                        Array.Empty<NetMutex>()));
                }
            }
        }
        return rows
            .GroupBy(row => row.SourceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(row => row.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CookingIngredientProjection ProjectCookingIngredients(
        CraftingRecipe recipe,
        Farmer player,
        CookingSource source)
    {
        var playerRemaining = player.Items.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        var containerRemaining = source.Containers
            .Select(inventory => inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray())
            .ToArray();
        var seasoningAvailableBeforeRecipe = CookingMatchingTotal(player.Items, playerRemaining, "917") +
            Enumerable.Range(0, source.Containers.Count)
                .Sum(index => CookingMatchingTotal(source.Containers[index], containerRemaining[index], "917"));
        var ingredientRows = new List<object>();
        var satisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var required = ingredient.Value;
            var available = CookingMatchingTotal(player.Items, playerRemaining, ingredient.Key);
            for (var index = 0; index < source.Containers.Count; index++)
            {
                available += CookingMatchingTotal(source.Containers[index], containerRemaining[index], ingredient.Key);
            }
            var consumed = new List<object>();
            ConsumeProjectedCookingIngredient(player.Items, playerRemaining, ingredient.Key, ref required,
                "player:" + player.UniqueMultiplayerID, consumed);
            for (var index = 0; index < source.Containers.Count && required > 0; index++)
            {
                ConsumeProjectedCookingIngredient(source.Containers[index], containerRemaining[index], ingredient.Key,
                    ref required, source.ContainerIds[index], consumed);
            }
            ingredientRows.Add(new
            {
                requirement_id_or_category = ingredient.Key,
                required_count = ingredient.Value,
                available_count_before_this_ingredient = available,
                satisfied = required == 0,
                native_consumption_plan = consumed.ToArray()
            });
            satisfied &= required == 0;
        }

        var seasoningRows = new List<object>();
        if (seasoningAvailableBeforeRecipe > 0)
        {
            var required = 1;
            var consumed = new List<object>();
            ConsumeProjectedCookingIngredient(player.Items, playerRemaining, "917", ref required,
                "player:" + player.UniqueMultiplayerID, consumed);
            for (var index = 0; index < source.Containers.Count && required > 0; index++)
            {
                ConsumeProjectedCookingIngredient(source.Containers[index], containerRemaining[index], "917",
                    ref required, source.ContainerIds[index], consumed);
            }
            seasoningRows.Add(new
            {
                requirement_id_or_category = "917",
                required_count = 1,
                available_count_before_seasoning = seasoningAvailableBeforeRecipe,
                satisfied = required == 0,
                native_consumption_plan = consumed.ToArray()
            });
        }
        return new CookingIngredientProjection(
            ingredientRows.ToArray(),
            seasoningRows.ToArray(),
            playerRemaining,
            satisfied,
            seasoningRows.Count > 0);
    }

    private static int CookingMatchingTotal(IList<Item?> inventory, int[] remaining, string requirement)
    {
        var result = 0;
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            if (remaining[slot] > 0 && CraftingRecipe.ItemMatchesForCrafting(inventory[slot], requirement))
            {
                result += remaining[slot];
            }
        }
        return result;
    }

    private static void ConsumeProjectedCookingIngredient(
        IList<Item?> inventory,
        int[] remaining,
        string requirement,
        ref int required,
        string sourceId,
        ICollection<object> consumed)
    {
        for (var slot = inventory.Count - 1; slot >= 0 && required > 0; slot--)
        {
            var item = inventory[slot];
            if (item is null || remaining[slot] <= 0 || !CraftingRecipe.ItemMatchesForCrafting(item, requirement))
            {
                continue;
            }
            var amount = Math.Min(required, remaining[slot]);
            remaining[slot] -= amount;
            required -= amount;
            consumed.Add(new
            {
                source_id = sourceId,
                slot_index = slot,
                qualified_item_id = item.QualifiedItemId,
                amount,
                unit_sale_price = Math.Max(0, item.salePrice()),
                total_sale_value = (long)Math.Max(0, item.salePrice()) * amount
            });
        }
    }

    private sealed record CookingSource(
        string SourceId,
        string Kind,
        GameLocation Location,
        Point Tile,
        IReadOnlyList<IInventory> Containers,
        IReadOnlyList<string> ContainerIds,
        IReadOnlyList<NetMutex> Mutexes);

    private sealed record CookingIngredientProjection(
        object[] IngredientRows,
        object[] SeasoningRows,
        int[] PlayerRemaining,
        bool Satisfied,
        bool SeasoningConsumed);
}
