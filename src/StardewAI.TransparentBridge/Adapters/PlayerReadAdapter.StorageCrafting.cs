using StardewValley;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadStorageCraftingContext(Farmer? player)
    {
        if (player is null || CraftingRecipe.craftingRecipes is null)
        {
            return new
            {
                projection_status = "unavailable_world_or_crafting_catalog",
                storage_recipe_count = 0,
                unclassified_known_recipe_count = 0,
                unclassified_known_recipes = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        if (SnapshotProfileContext.Current is not ("machine" or "training_machine" or "full"))
        {
            return new
            {
                projection_status = "blocked_requires_machine_training_machine_or_full_profile",
                known_recipe_count = player.craftingRecipes.Count(),
                storage_recipe_count = 0,
                unclassified_known_recipe_count = 0,
                unclassified_known_recipes = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        var rows = new List<object>();
        var failures = new List<object>();
        foreach (var recipeName in player.craftingRecipes.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!CraftingRecipe.craftingRecipes.TryGetValue(recipeName, out var rawRecipe))
            {
                failures.Add(new { recipe_name = recipeName, reason = "known_recipe_missing_crafting_data_entry" });
                continue;
            }

            CraftingRecipe recipe;
            Item output;
            try
            {
                recipe = new CraftingRecipe(recipeName, isCookingRecipe: false);
                if (recipe.itemToProduce.Count != 1)
                {
                    continue;
                }
                output = recipe.createItem();
            }
            catch (Exception ex)
            {
                failures.Add(new
                {
                    recipe_name = recipeName,
                    reason = "crafting_recipe_or_output_creation_failed:" + ex.GetType().Name
                });
                continue;
            }

            if (output is not StardewValley.Object storage ||
                !TryClassifyNativeStoragePlacement(storage, out var storageBranch))
            {
                continue;
            }

            var remaining = player.Items.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
            var firstPlan = TryConsumeOneCraft(recipe, player.Items, remaining, out var materialRows)
                ? materialRows
                : ReadUnavailableMaterialRows(recipe, player.Items);
            var craftable = ProjectCraftableCount(recipe, player.Items);
            var workbenchSources = ReadWorkbenchCraftingSources(recipe, player, output);
            var acceptsOutputAfterConsumption = craftable.Count > 0 &&
                InventoryAcceptsAfterConsumption(player.Items, remaining, output);
            var probe = CreateStoragePlacementProbe(storage, storageBranch);
            var ordinaryMaterialStorage =
                probe.SpecialChestType is StardewValley.Objects.Chest.SpecialChestTypes.None or
                    StardewValley.Objects.Chest.SpecialChestTypes.BigChest &&
                storageBranch is not (
                    "native_object_placement_mini_fridge" or
                    "native_object_placement_mini_shipping_bin");

            rows.Add(new
            {
                recipe_name = recipe.name,
                raw_recipe_data = rawRecipe,
                times_crafted = recipe.timesCrafted,
                output_selection_status = "exact_single_native_storage_output",
                output_item_id = output.ItemId,
                output_qualified_item_id = output.QualifiedItemId,
                output_display_name = output.DisplayName,
                output_runtime_type = output.GetType().FullName,
                output_count_per_craft = recipe.numberProducedPerCraft,
                native_storage_branch = storageBranch,
                special_chest_type = probe.SpecialChestType.ToString(),
                actual_capacity = probe.GetActualCapacity(),
                ordinary_material_storage = ordinaryMaterialStorage,
                shared_global_storage =
                    probe.SpecialChestType == StardewValley.Objects.Chest.SpecialChestTypes.JunimoChest,
                shipping_storage =
                    probe.SpecialChestType == StardewValley.Objects.Chest.SpecialChestTypes.MiniShippingBin,
                fridge_storage = storageBranch == "native_object_placement_mini_fridge",
                known_recipe = true,
                ingredients_player_inventory_only = true,
                ingredient_rows = firstPlan,
                workbench_crafting_sources = workbenchSources,
                workbench_crafting_source_count = workbenchSources.Length,
                has_ingredients_for_one = craftable.Count > 0,
                craftable_count_from_player_inventory = craftable.Count,
                craftable_count_status = craftable.Capped
                    ? "bounded_at_999_requires_larger_projection"
                    : "exact_native_match_and_reverse_slot_consumption",
                output_inventory_acceptance_after_material_consumption = acceptsOutputAfterConsumption,
                craft_candidate_status = craftable.Count > 0
                    ? acceptsOutputAfterConsumption
                        ? "ready_for_native_personal_crafting_menu"
                        : "blocked_output_cannot_fit_after_material_consumption"
                    : "blocked_insufficient_player_inventory_materials",
                native_contract =
                    "CraftingPage.receiveLeftClick_then_clickCraftingRecipe_then_CraftingRecipe.consumeIngredients_reverse_inventory_order"
            });
        }

        return new
        {
            schema_version = "storage_crafting.v1",
            projection_status = failures.Count == 0
                ? "complete_known_storage_recipe_projection"
                : "partial_unclassified_known_recipe_catalog_entries",
            storage_recipe_count = rows.Count,
            unclassified_known_recipe_count = failures.Count,
            unclassified_known_recipes = failures.ToArray(),
            scan_scope = "all_learned_recipes_classified_by_native_storage_placement_branch",
            rows = rows.ToArray()
        };
    }
}
