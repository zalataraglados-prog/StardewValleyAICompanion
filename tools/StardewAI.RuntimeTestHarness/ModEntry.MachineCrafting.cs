using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteCraftMachineItem(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var requested = CraftMachineRequestedEffect(request);
        if (Game1.player is null || Game1.activeClickableMenu is not null ||
            string.IsNullOrWhiteSpace(request.RecipeName) ||
            string.IsNullOrWhiteSpace(request.OutputQualifiedItemId) ||
            !request.OutputCount.HasValue || !request.TimesCraftedBefore.HasValue ||
            string.IsNullOrWhiteSpace(request.IngredientRowsJson) ||
            !string.Equals(request.CraftingSource, "native_personal_crafting_menu", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_request_or_menu_state_invalid");
        }
        if (!Game1.player.craftingRecipes.TryGetValue(request.RecipeName, out var liveTimesCrafted) ||
            liveTimesCrafted != request.TimesCraftedBefore.Value ||
            !CraftingRecipe.craftingRecipes.ContainsKey(request.RecipeName))
        {
            return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_recipe_identity_or_count_drifted");
        }

        CraftingRecipe recipe;
        Item preview;
        try
        {
            recipe = new CraftingRecipe(request.RecipeName, isCookingRecipe: false);
            preview = recipe.createItem();
        }
        catch (Exception ex)
        {
            return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_recipe_creation_failed:" + ex.GetType().Name);
        }
        if (recipe.itemToProduce.Count != 1 ||
            !string.Equals(preview.QualifiedItemId, request.OutputQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(preview.ItemId, request.OutputItemId, StringComparison.Ordinal) ||
            preview.Stack != request.OutputCount.Value)
        {
            return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_output_projection_drifted");
        }

        var projectedIngredients = ProjectNativePersonalCraftIngredients(recipe, Game1.player.Items);
        if (!projectedIngredients.Satisfied ||
            !JsonEquivalent(projectedIngredients.RowsJson, request.IngredientRowsJson))
        {
            return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_ingredient_projection_drifted");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var affectedIds = projectedIngredients.ConsumedByQualifiedId.Keys
            .Append(request.OutputQualifiedItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var beforeCounts = affectedIds.ToDictionary(id => id, InventoryQualifiedCount, StringComparer.Ordinal);
        var achievementsBefore = string.Join(",", Game1.player.achievements.OrderBy(id => id));
        var questsBefore = CraftQuestSignature();
        CraftingPage? page = null;
        var clickFound = false;
        try
        {
            var width = 800 + IClickableMenu.borderWidth * 2;
            var height = 600 + IClickableMenu.borderWidth * 2;
            page = new CraftingPage(
                Math.Max(0, (Game1.uiViewport.Width - width) / 2),
                Math.Max(0, (Game1.uiViewport.Height - height) / 2),
                width,
                height,
                cooking: false,
                standaloneMenu: false,
                materialContainers: null);
            Game1.activeClickableMenu = page;
            for (var pageIndex = 0; pageIndex < page.pagesOfCraftingRecipes.Count && !clickFound; pageIndex++)
            {
                foreach (var pair in page.pagesOfCraftingRecipes[pageIndex])
                {
                    if (!string.Equals(pair.Value.name, request.RecipeName, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    page.currentCraftingPage = pageIndex;
                    page.receiveLeftClick(pair.Key.bounds.Center.X, pair.Key.bounds.Center.Y, playSound: false);
                    clickFound = true;
                    break;
                }
            }
            if (!clickFound || page.heldItem is null ||
                !string.Equals(page.heldItem.QualifiedItemId, request.OutputQualifiedItemId, StringComparison.Ordinal) ||
                page.heldItem.Stack != request.OutputCount.Value)
            {
                if (page.heldItem is null)
                {
                    page.exitThisMenuNoSound();
                }
                return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_native_recipe_click_failed");
            }

            var targetSlot = FindCraftedOutputInventorySlot(page.heldItem);
            if (targetSlot < 0 || targetSlot >= page.inventory.inventory.Count)
            {
                return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_output_inventory_slot_unavailable_after_consumption");
            }
            var target = page.inventory.inventory[targetSlot].bounds.Center;
            page.receiveLeftClick(target.X, target.Y, playSound: false);
            if (page.heldItem is not null)
            {
                return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_native_inventory_click_failed");
            }
            page.exitThisMenuNoSound();
        }
        catch (Exception ex)
        {
            if (page?.heldItem is null && ReferenceEquals(Game1.activeClickableMenu, page))
            {
                page?.exitThisMenuNoSound();
            }
            return BlockedWithPrimitive(request, "craft_machine_item", requested, CraftMachineObservedEffect(request), "craft_machine_item_native_menu_exception:" + ex.GetType().Name);
        }

        var expectedCounts = beforeCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var consumed in projectedIngredients.ConsumedByQualifiedId)
        {
            expectedCounts[consumed.Key] -= consumed.Value;
        }
        expectedCounts[request.OutputQualifiedItemId] += request.OutputCount.Value;
        var afterCounts = affectedIds.ToDictionary(id => id, InventoryQualifiedCount, StringComparer.Ordinal);
        var countMatch = affectedIds.All(id => expectedCounts[id] == afterCounts[id]);
        var timesAfter = Game1.player.craftingRecipes.TryGetValue(request.RecipeName, out var observedTimes) ? observedTimes : -1;
        var recipeCountMatch = timesAfter == request.TimesCraftedBefore.Value + request.OutputCount.Value;
        var menuClosed = Game1.activeClickableMenu is null;
        var verified = clickFound && countMatch && recipeCountMatch && menuClosed;
        var achievementsAfter = string.Join(",", Game1.player.achievements.OrderBy(id => id));
        var questsAfter = CraftQuestSignature();

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "craft_machine_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_CraftingPage_recipe_click_completed",
                    "native_CraftingPage_inventory_click_completed",
                    "exact_ingredient_and_output_multiset_verified",
                    "native_recipe_count_increment_verified",
                    "native_quest_callbacks_and_crafting_achievement_check_path_executed"
                }
                : new[]
                {
                    countMatch ? "inventory_multiset_match" : "inventory_multiset_mismatch",
                    recipeCountMatch ? "recipe_count_match" : "recipe_count_mismatch",
                    menuClosed ? "menu_closed" : "menu_not_closed"
                },
            RequestedEffect = requested,
            ObservedEffect = CraftMachineObservedEffect(request) +
                ";achievements_before=" + achievementsBefore + ";achievements_after=" + achievementsAfter +
                ";quest_signature_before=" + questsBefore + ";quest_signature_after=" + questsAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "craft_machine_item_post_state_mismatch" },
            ChangedFacts = verified
                ? affectedIds.Select(id => new SimulatedFactChange
                {
                    Path = "player.inventory.qualified_count[" + id + "]",
                    Before = beforeCounts[id].ToString(),
                    After = afterCounts[id].ToString()
                }).Append(new SimulatedFactChange
                {
                    Path = "player.crafting_recipes[" + request.RecipeName + "]",
                    Before = request.TimesCraftedBefore.Value.ToString(),
                    After = timesAfter.ToString()
                }).ToArray()
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static ProjectedCraftIngredients ProjectNativePersonalCraftIngredients(CraftingRecipe recipe, IList<Item?> inventory)
    {
        var remaining = inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        var rows = new List<object>();
        var consumedById = new Dictionary<string, int>(StringComparer.Ordinal);
        var satisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var required = ingredient.Value;
            var available = 0;
            for (var slot = 0; slot < inventory.Count; slot++)
            {
                if (remaining[slot] > 0 && CraftingRecipe.ItemMatchesForCrafting(inventory[slot], ingredient.Key))
                {
                    available += remaining[slot];
                }
            }
            var consumed = new List<object>();
            for (var slot = inventory.Count - 1; slot >= 0 && required > 0; slot--)
            {
                var item = inventory[slot];
                if (item is null || remaining[slot] <= 0 || !CraftingRecipe.ItemMatchesForCrafting(item, ingredient.Key))
                {
                    continue;
                }
                var amount = Math.Min(required, remaining[slot]);
                remaining[slot] -= amount;
                required -= amount;
                consumed.Add(new { slot_index = slot, qualified_item_id = item.QualifiedItemId, amount });
                consumedById[item.QualifiedItemId] = consumedById.TryGetValue(item.QualifiedItemId, out var old) ? old + amount : amount;
            }
            rows.Add(new
            {
                requirement_id_or_category = ingredient.Key,
                required_count = ingredient.Value,
                available_count_before_this_ingredient = available,
                satisfied = required == 0,
                reverse_slot_consumption_plan = consumed.ToArray()
            });
            satisfied &= required == 0;
        }
        return new ProjectedCraftIngredients(JsonSerializer.Serialize(rows), consumedById, satisfied);
    }

    private static bool JsonEquivalent(string left, string right)
    {
        try
        {
            using var leftDocument = JsonDocument.Parse(left);
            using var rightDocument = JsonDocument.Parse(right);
            return string.Equals(leftDocument.RootElement.GetRawText(), rightDocument.RootElement.GetRawText(), StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int FindCraftedOutputInventorySlot(Item output)
    {
        for (var slot = 0; slot < Game1.player.Items.Count; slot++)
        {
            var item = Game1.player.Items[slot];
            if (item is null || item.canStackWith(output) && item.maximumStackSize() - item.Stack >= output.Stack)
            {
                return slot;
            }
        }
        return -1;
    }

    private static int InventoryQualifiedCount(string qualifiedId) =>
        Game1.player.Items.Where(item => item is not null && string.Equals(item.QualifiedItemId, qualifiedId, StringComparison.Ordinal)).Sum(item => item!.Stack);

    private static string CraftQuestSignature() =>
        string.Join(",", Game1.player.questLog.OrderBy(quest => quest.id.Value).Select(quest => quest.id.Value + ":" + quest.questType.Value + ":" + quest.completed.Value));

    private static string CraftMachineRequestedEffect(TrainingExecutionRequest request) =>
        "player.inventory.materials_consumed_by_native_recipe=true;player.inventory.output_increases=" + request.OutputQualifiedItemId + ":" + request.OutputCount + ";player.crafting_recipes[" + request.RecipeName + "].count_increases=" + request.OutputCount;

    private static string CraftMachineObservedEffect(TrainingExecutionRequest request) =>
        "recipe=" + request.RecipeName + ";times_crafted=" + (Game1.player?.craftingRecipes.TryGetValue(request.RecipeName, out var count) == true ? count : -1) + ";output_count=" + InventoryQualifiedCount(request.OutputQualifiedItemId) + ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");

    private sealed record ProjectedCraftIngredients(string RowsJson, Dictionary<string, int> ConsumedByQualifiedId, bool Satisfied);
}
