using StardewValley;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadCrabPotCraftingContext(Farmer? player)
    {
        if (player is null || CraftingRecipe.craftingRecipes is null)
        {
            return new
            {
                projection_status = "unavailable_world_or_crafting_catalog",
                learned_crab_pot_recipe_count = 0,
                rows = Array.Empty<object>()
            };
        }
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                learned_crab_pot_recipe_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var rows = new List<object>();
        var failures = new List<object>();
        foreach (var recipeName in player.craftingRecipes.Keys
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!CraftingRecipe.craftingRecipes.TryGetValue(
                    recipeName,
                    out var rawRecipe))
            {
                failures.Add(new
                {
                    recipe_name = recipeName,
                    reason = "known_recipe_missing_crafting_data_entry"
                });
                continue;
            }

            CraftingRecipe recipe;
            Item output;
            try
            {
                recipe = new CraftingRecipe(
                    recipeName,
                    isCookingRecipe: false);
                if (recipe.itemToProduce.Count != 1)
                    continue;
                output = recipe.createItem();
            }
            catch (Exception ex)
            {
                failures.Add(new
                {
                    recipe_name = recipeName,
                    reason = "crafting_recipe_or_output_creation_failed:" +
                        ex.GetType().Name
                });
                continue;
            }
            if (!string.Equals(
                    output.QualifiedItemId,
                    CrabPotQualifiedItemId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var remaining = player.Items
                .Select(item => Math.Max(0, item?.Stack ?? 0))
                .ToArray();
            var firstPlan = TryConsumeOneCraft(
                    recipe,
                    player.Items,
                    remaining,
                    out var materialRows)
                ? materialRows
                : ReadUnavailableMaterialRows(recipe, player.Items);
            var craftable = ProjectCraftableCount(recipe, player.Items);
            var workbenchSources = ReadWorkbenchCraftingSources(
                recipe,
                player,
                output);
            var acceptsOutput = craftable.Count > 0 &&
                InventoryAcceptsAfterConsumption(
                    player.Items,
                    remaining,
                    output);
            rows.Add(new
            {
                recipe_name = recipe.name,
                raw_recipe_data = rawRecipe,
                times_crafted = recipe.timesCrafted,
                output_selection_status =
                    "exact_single_crab_pot_output",
                output_item_id = output.ItemId,
                output_qualified_item_id = output.QualifiedItemId,
                output_display_name = output.DisplayName,
                output_runtime_type = output.GetType().FullName,
                output_count_per_craft = recipe.numberProducedPerCraft,
                known_recipe = true,
                ingredient_rows = firstPlan,
                workbench_crafting_sources = workbenchSources,
                workbench_crafting_source_count = workbenchSources.Length,
                has_ingredients_for_one = craftable.Count > 0,
                craftable_count_from_player_inventory = craftable.Count,
                craftable_count_status = craftable.Capped
                    ? "bounded_at_999_requires_larger_projection"
                    : "exact_native_match_and_reverse_slot_consumption",
                output_inventory_acceptance_after_material_consumption =
                    acceptsOutput,
                craft_candidate_status = craftable.Count > 0
                    ? acceptsOutput
                        ? "ready_for_native_personal_crafting_menu"
                        : "blocked_output_cannot_fit_after_material_consumption"
                    : "blocked_insufficient_player_inventory_materials",
                native_contract =
                    "CraftingPage.receiveLeftClick_then_clickCraftingRecipe_then_CraftingRecipe.consumeIngredients_reverse_inventory_order"
            });
        }

        return new
        {
            schema_version = "crab_pot_crafting.v1",
            projection_status = failures.Count == 0
                ? "complete_learned_crab_pot_recipe_projection"
                : "partial_unclassified_known_recipe_catalog_entries",
            learned_crab_pot_recipe_count = rows.Count,
            recipe_learned = rows.Count > 0,
            unclassified_known_recipe_count = failures.Count,
            unclassified_known_recipes = failures.ToArray(),
            rows = rows.ToArray()
        };
    }
}
