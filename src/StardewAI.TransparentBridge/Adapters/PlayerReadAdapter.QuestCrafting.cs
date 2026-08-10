using StardewValley;
using StardewValley.Quests;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadQuestCraftingContext(Farmer? player)
    {
        if (player is null || CraftingRecipe.craftingRecipes is null)
        {
            return new
            {
                projection_status = "unavailable_world_or_crafting_catalog",
                active_target_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var quests = player.questLog
            .OfType<CraftingQuest>()
            .Where(quest => quest.accepted.Value && !quest.completed.Value)
            .OrderBy(quest => quest.id.Value, StringComparer.Ordinal)
            .ToArray();
        if (quests.Length == 0)
        {
            return new
            {
                projection_status = "not_applicable_no_active_crafting_quest",
                active_target_count = 0,
                rows = Array.Empty<object>()
            };
        }

        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                active_target_count = quests.Length,
                rows = quests.Select(quest => MissingQuestCraftingRow(
                    quest,
                    "blocked_requires_full_profile")).ToArray()
            };
        }

        var rows = new List<object>();
        foreach (var quest in quests)
        {
            var targetQualifiedId = quest.ItemId.Value ?? string.Empty;
            var matched = false;
            foreach (var recipeName in player.craftingRecipes.Keys
                         .OrderBy(name => name, StringComparer.Ordinal))
            {
                if (!CraftingRecipe.craftingRecipes.TryGetValue(
                        recipeName,
                        out var rawRecipe))
                {
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
                catch
                {
                    continue;
                }

                if (!string.Equals(
                        output.QualifiedItemId,
                        targetQualifiedId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                matched = true;
                var remaining = player.Items
                    .Select(item => Math.Max(0, item?.Stack ?? 0))
                    .ToArray();
                var hasOne = TryConsumeOneCraft(
                    recipe,
                    player.Items,
                    remaining,
                    out var ingredientRows);
                if (!hasOne)
                {
                    ingredientRows = ReadUnavailableMaterialRows(
                        recipe,
                        player.Items);
                }
                var acceptsOutput = hasOne &&
                    InventoryAcceptsAfterConsumption(
                        player.Items,
                        remaining,
                        output);
                var workbenchSources = ReadWorkbenchCraftingSources(
                    recipe,
                    player,
                    output);
                rows.Add(new
                {
                    quest_id = quest.id.Value,
                    quest_runtime_type = nameof(CraftingQuest),
                    target_qualified_item_id = targetQualifiedId,
                    recipe_name = recipe.name,
                    raw_recipe_data = rawRecipe,
                    times_crafted = recipe.timesCrafted,
                    output_selection_status = "exact_single_output",
                    output_item_id = output.ItemId,
                    output_qualified_item_id = output.QualifiedItemId,
                    output_display_name = output.DisplayName,
                    output_runtime_type = output.GetType().FullName ??
                        output.GetType().Name,
                    output_count_per_craft = recipe.numberProducedPerCraft,
                    known_recipe = true,
                    ingredient_rows = ingredientRows,
                    has_ingredients_for_one = hasOne,
                    craftable_count_from_player_inventory =
                        ProjectCraftableCount(recipe, player.Items).Count,
                    output_inventory_acceptance_after_material_consumption =
                        acceptsOutput,
                    craft_candidate_status = !hasOne
                        ? "blocked_insufficient_player_inventory_materials"
                        : acceptsOutput
                            ? "ready_for_native_personal_crafting_menu"
                            : "blocked_output_cannot_fit_after_material_consumption",
                    workbench_crafting_sources = workbenchSources,
                    workbench_crafting_source_count = workbenchSources.Length,
                    native_contract =
                        "CraftingPage.receiveLeftClick->CraftingRecipe.consumeIngredients->Quest.OnRecipeCrafted"
                });
            }

            if (!matched)
            {
                rows.Add(MissingQuestCraftingRow(
                    quest,
                    "blocked_matching_learned_recipe_unavailable"));
            }
        }

        return new
        {
            projection_status =
                "complete_active_crafting_quest_targeted_recipe_projection",
            active_target_count = quests.Length,
            row_count = rows.Count,
            scan_scope =
                "active_accepted_uncompleted_CraftingQuest_targets_only",
            rows = rows.ToArray()
        };
    }

    private static object MissingQuestCraftingRow(
        CraftingQuest quest,
        string status) =>
        new
        {
            quest_id = quest.id.Value,
            quest_runtime_type = nameof(CraftingQuest),
            target_qualified_item_id = quest.ItemId.Value ?? string.Empty,
            recipe_name = string.Empty,
            output_item_id = string.Empty,
            output_qualified_item_id = string.Empty,
            output_count_per_craft = 0,
            times_crafted = 0,
            known_recipe = false,
            ingredient_rows = Array.Empty<object>(),
            output_inventory_acceptance_after_material_consumption = false,
            craft_candidate_status = status,
            workbench_crafting_sources = Array.Empty<object>(),
            workbench_crafting_source_count = 0,
            native_contract =
                "CraftingPage.receiveLeftClick->CraftingRecipe.consumeIngredients->Quest.OnRecipeCrafted"
        };
}
