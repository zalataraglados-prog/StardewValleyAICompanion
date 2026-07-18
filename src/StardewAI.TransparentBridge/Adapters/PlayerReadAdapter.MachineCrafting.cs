using StardewValley;
using StardewValley.Objects;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const int MaxProjectedMachineCraftCount = 999;

    private static object ReadMachineCraftingContext(Farmer? player)
    {
        if (player is null || CraftingRecipe.craftingRecipes is null)
        {
            return new
            {
                projection_status = "unavailable_world_or_crafting_catalog",
                machine_recipe_count = 0,
                unclassified_known_recipe_count = 0,
                unclassified_known_recipe_names = Array.Empty<string>(),
                unclassified_known_recipes = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        var profile = SnapshotProfileContext.Current;
        var houseLevel = ReadFarmhouseUpgradeLevel(player) ?? 0;
        if (houseLevel < 2 && profile is not ("machine" or "training_machine" or "full"))
        {
            return new
            {
                projection_status = "blocked_profile_and_house_level_not_relevant",
                machine_recipe_count = 0,
                unclassified_known_recipe_count = 0,
                unclassified_known_recipe_names = Array.Empty<string>(),
                unclassified_known_recipes = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        var rows = new List<object>();
        var unclassified = new List<UnclassifiedRecipe>();
        foreach (var recipeName in player.craftingRecipes.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!CraftingRecipe.craftingRecipes.TryGetValue(recipeName, out var rawRecipe))
            {
                unclassified.Add(new UnclassifiedRecipe(recipeName, "known_recipe_missing_crafting_data_entry"));
                continue;
            }

            CraftingRecipe recipe;
            try
            {
                recipe = new CraftingRecipe(recipeName, isCookingRecipe: false);
            }
            catch (Exception ex)
            {
                unclassified.Add(new UnclassifiedRecipe(recipeName, "crafting_recipe_parse_failed:" + ex.GetType().Name));
                continue;
            }

            var outputFailures = new List<string>();
            var outputs = ReadMachineRecipeOutputs(recipe, outputFailures).ToArray();
            if (outputFailures.Count > 0)
            {
                unclassified.Add(new UnclassifiedRecipe(
                    recipeName,
                    "recipe_output_item_creation_failed:" + string.Join(",", outputFailures.OrderBy(id => id, StringComparer.Ordinal))));
                continue;
            }
            if (outputs.Length == 0)
            {
                continue;
            }

            var remaining = player.Items.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
            var firstPlan = TryConsumeOneCraft(recipe, player.Items, remaining, out var materialRows)
                ? materialRows
                : ReadUnavailableMaterialRows(recipe, player.Items);
            var craftable = ProjectCraftableCount(recipe, player.Items);
            var output = outputs[0];
            var acceptsOutputAfterConsumption = craftable.Count > 0 &&
                InventoryAcceptsAfterConsumption(player.Items, remaining, output.Item);
            rows.Add(new
            {
                recipe_name = recipe.name,
                raw_recipe_data = rawRecipe,
                times_crafted = recipe.timesCrafted,
                output_selection_status = outputs.Length == 1 && recipe.itemToProduce.Count == 1
                    ? "exact_single_machine_output"
                    : "blocked_multiple_or_mixed_outputs",
                output_item_id = output.ItemId,
                output_qualified_item_id = output.QualifiedItemId,
                output_display_name = output.DisplayName,
                output_runtime_type = output.RuntimeType,
                output_big_craftable = output.BigCraftable,
                output_machine_data_status = output.MachineDataStatus,
                output_count_per_craft = recipe.numberProducedPerCraft,
                output_is_cask = output.IsCask,
                placement_location_rule = output.IsCask
                    ? "Cellar_or_location_map_property_CanCaskHere"
                    : "Object.canBePlacedHere_and_GameLocation.CanItemBePlacedHere",
                known_recipe = true,
                ingredients_player_inventory_only = true,
                ingredient_rows = firstPlan,
                has_ingredients_for_one = craftable.Count > 0,
                craftable_count_from_player_inventory = craftable.Count,
                craftable_count_status = craftable.Capped
                    ? "bounded_at_999_requires_larger_projection"
                    : "exact_native_match_and_reverse_slot_consumption",
                output_inventory_acceptance_for_one = player.couldInventoryAcceptThisItem(output.Item),
                output_inventory_acceptance_after_material_consumption = acceptsOutputAfterConsumption,
                output_inventory_acceptance_status = craftable.Count == 0
                    ? "not_applicable_materials_unavailable"
                    : acceptsOutputAfterConsumption
                        ? "exact_acceptance_after_projected_native_consumption"
                        : "blocked_output_cannot_fit_after_projected_native_consumption",
                craft_candidate_status = outputs.Length == 1 && recipe.itemToProduce.Count == 1 && craftable.Count > 0
                    ? acceptsOutputAfterConsumption
                        ? "ready_for_native_personal_crafting_menu"
                        : "blocked_output_cannot_fit_after_material_consumption"
                    : outputs.Length != 1 || recipe.itemToProduce.Count != 1
                        ? "blocked_non_deterministic_output"
                        : "blocked_insufficient_player_inventory_materials",
                native_contract = "CraftingPage.receiveLeftClick_then_clickCraftingRecipe_then_CraftingRecipe.consumeIngredients_reverse_inventory_order"
            });
        }

        return new
        {
            projection_status = unclassified.Count == 0
                ? "complete_known_machine_recipe_projection"
                : "partial_unclassified_known_recipe_catalog_entries",
            machine_recipe_count = rows.Count,
            unclassified_known_recipe_count = unclassified.Count,
            unclassified_known_recipe_names = unclassified.Select(row => row.RecipeName).ToArray(),
            unclassified_known_recipes = unclassified.Select(row => new { recipe_name = row.RecipeName, reason = row.Reason }).ToArray(),
            max_projected_craft_count = MaxProjectedMachineCraftCount,
            rows = rows.ToArray()
        };
    }

    private static IEnumerable<MachineRecipeOutput> ReadMachineRecipeOutputs(
        CraftingRecipe recipe,
        ICollection<string> outputFailures)
    {
        foreach (var itemId in recipe.itemToProduce)
        {
            var qualifiedId = ItemRegistry.ManuallyQualifyItemId(itemId, recipe.bigCraftable ? "(BC)" : "(O)");
            Item item;
            try
            {
                item = ItemRegistry.Create(qualifiedId, Math.Max(1, recipe.numberProducedPerCraft));
            }
            catch
            {
                outputFailures.Add(itemId);
                continue;
            }
            if (item is not StardewValley.Object obj || !obj.bigCraftable.Value || obj.GetMachineData() is null)
            {
                continue;
            }
            yield return new MachineRecipeOutput(
                item,
                item.ItemId,
                item.QualifiedItemId,
                item.DisplayName,
                item.GetType().FullName ?? item.GetType().Name,
                obj.bigCraftable.Value,
                "available_Object.GetMachineData",
                item is Cask);
        }
    }

    private static CraftCountProjection ProjectCraftableCount(CraftingRecipe recipe, IList<Item?> inventory)
    {
        var remaining = inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        var count = 0;
        while (count < MaxProjectedMachineCraftCount)
        {
            var attempt = (int[])remaining.Clone();
            if (!TryConsumeOneCraft(recipe, inventory, attempt, out _))
            {
                return new CraftCountProjection(count, false);
            }
            remaining = attempt;
            count++;
        }

        var overflowAttempt = (int[])remaining.Clone();
        return new CraftCountProjection(count, TryConsumeOneCraft(recipe, inventory, overflowAttempt, out _));
    }

    private static bool TryConsumeOneCraft(
        CraftingRecipe recipe,
        IList<Item?> inventory,
        int[] remainingStacks,
        out object[] materialRows)
    {
        var rows = new List<object>();
        var allSatisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var requiredRemaining = ingredient.Value;
            var consumed = new List<object>();
            var availableBefore = MatchingStackTotal(inventory, remainingStacks, ingredient.Key);
            for (var slot = inventory.Count - 1; slot >= 0 && requiredRemaining > 0; slot--)
            {
                var item = inventory[slot];
                if (remainingStacks[slot] <= 0 || !CraftingRecipe.ItemMatchesForCrafting(item, ingredient.Key))
                {
                    continue;
                }
                var amount = Math.Min(requiredRemaining, remainingStacks[slot]);
                remainingStacks[slot] -= amount;
                requiredRemaining -= amount;
                consumed.Add(new
                {
                    slot_index = slot,
                    qualified_item_id = item!.QualifiedItemId,
                    amount
                });
            }
            rows.Add(new
            {
                requirement_id_or_category = ingredient.Key,
                required_count = ingredient.Value,
                available_count_before_this_ingredient = availableBefore,
                satisfied = requiredRemaining == 0,
                reverse_slot_consumption_plan = consumed.ToArray()
            });
            if (requiredRemaining > 0)
            {
                allSatisfied = false;
            }
        }
        materialRows = rows.ToArray();
        return allSatisfied;
    }

    private static object[] ReadUnavailableMaterialRows(CraftingRecipe recipe, IList<Item?> inventory)
    {
        var stacks = inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        TryConsumeOneCraft(recipe, inventory, stacks, out var rows);
        return rows;
    }

    private static int MatchingStackTotal(IList<Item?> inventory, int[] remainingStacks, string requirement)
    {
        var total = 0;
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            if (remainingStacks[slot] > 0 && CraftingRecipe.ItemMatchesForCrafting(inventory[slot], requirement))
            {
                total += remainingStacks[slot];
            }
        }
        return total;
    }

    private static bool InventoryAcceptsAfterConsumption(IList<Item?> inventory, int[] remainingStacks, Item output)
    {
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            var item = inventory[slot];
            if (item is null || remainingStacks[slot] <= 0)
            {
                return true;
            }
            if (item.canStackWith(output) &&
                item.maximumStackSize() - remainingStacks[slot] >= output.Stack)
            {
                return true;
            }
        }
        return false;
    }

    private sealed record MachineRecipeOutput(
        Item Item,
        string ItemId,
        string QualifiedItemId,
        string DisplayName,
        string RuntimeType,
        bool BigCraftable,
        string MachineDataStatus,
        bool IsCask);

    private sealed record CraftCountProjection(int Count, bool Capped);

    private sealed record UnclassifiedRecipe(string RecipeName, string Reason);
}
