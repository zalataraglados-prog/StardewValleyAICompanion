using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMachineLifecycleTarget(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.RecipeName) ||
            string.IsNullOrWhiteSpace(request.OutputQualifiedItemId) ||
            string.IsNullOrWhiteSpace(request.ProcessInputQualifiedItemId) ||
            request.ProcessInputQuantity is not > 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_lifecycle_target",
                "target_machine_fleet=empty;recipe_and_process_materials=ready",
                "request=missing_required_machine_lifecycle_fields",
                "machine_lifecycle_fixture_request_invalid");
        }
        if (!Game1.player.craftingRecipes.ContainsKey(request.RecipeName) ||
            !CraftingRecipe.craftingRecipes.ContainsKey(request.RecipeName))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_lifecycle_target",
                "target_machine_fleet=empty;recipe_and_process_materials=ready",
                "recipe=" + request.RecipeName,
                "machine_lifecycle_fixture_requires_learned_recipe");
        }

        CraftingRecipe recipe;
        StardewValley.Object machinePreview;
        try
        {
            recipe = new CraftingRecipe(request.RecipeName, isCookingRecipe: false);
            machinePreview = recipe.createItem() as StardewValley.Object
                ?? throw new InvalidOperationException("recipe_output_not_object");
        }
        catch (Exception ex)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_lifecycle_target",
                "target_machine_fleet=empty;recipe_and_process_materials=ready",
                "recipe=" + request.RecipeName,
                "machine_lifecycle_fixture_recipe_creation_failed:" +
                ex.GetType().Name);
        }
        if (!machinePreview.bigCraftable.Value ||
            machinePreview.GetMachineData() is null ||
            !string.Equals(
                machinePreview.QualifiedItemId,
                request.OutputQualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_lifecycle_target",
                "target_machine_fleet=empty;recipe_and_process_materials=ready",
                "output=" + machinePreview.QualifiedItemId,
                "machine_lifecycle_fixture_requires_native_machine_output");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var placedBefore = CountPlacedMachineInstances(
            request.OutputQualifiedItemId);
        var inventoryBefore = InventoryQualifiedCount(
            request.OutputQualifiedItemId);
        Utility.ForEachLocation(
            location =>
            {
                var tiles = location.objects.Pairs
                    .Where(pair => string.Equals(
                        pair.Value.QualifiedItemId,
                        request.OutputQualifiedItemId,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (var tile in tiles)
                {
                    location.objects.Remove(tile);
                }
                return true;
            },
            includeInteriors: true,
            includeGenerated: false);
        for (var slot = 0; slot < Game1.player.Items.Count; slot++)
        {
            if (string.Equals(
                    Game1.player.Items[slot]?.QualifiedItemId,
                    request.OutputQualifiedItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Game1.player.Items[slot] = null;
            }
        }

        var farm = Game1.getFarm();
        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        farm.objects.Remove(targetVector);
        farm.terrainFeatures.Remove(targetVector);

        var usesWorkbench = string.Equals(
            request.CraftingSource,
            "native_workbench_crafting_menu",
            StringComparison.Ordinal);
        WorkbenchMachineLifecycleFixture? workbenchFixture = null;
        if (usesWorkbench)
        {
            workbenchFixture =
                SetupWorkbenchMachineLifecycleFixture(
                    request,
                    farm,
                    target,
                    recipe,
                    out var workbenchReason);
            if (workbenchFixture is null)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_machine_lifecycle_target",
                    "target_machine_fleet=empty;workbench_recipe_materials=ready",
                    "workbench=" + workbenchReason,
                    "machine_lifecycle_workbench_fixture_invalid");
            }
        }

        var additionalRows = ParseMachineLifecycleAdditionalItems(
            request.ProcessAdditionalItemsJson,
            out var additionalParseReason);
        if (additionalRows is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_lifecycle_target",
                "target_machine_fleet=empty;recipe_and_process_materials=ready",
                "additional_items=" + additionalParseReason,
                "machine_lifecycle_fixture_additional_items_invalid");
        }

        var ingredientSlots = new List<string>();
        foreach (var ingredient in recipe.recipeList)
        {
            if (!int.TryParse(ingredient.Key, out var itemId) || itemId < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_machine_lifecycle_target",
                    "target_machine_fleet=empty;recipe_and_process_materials=ready",
                    "requirement=" + ingredient.Key,
                    "machine_lifecycle_fixture_requires_exact_recipe_ingredients");
            }
            var qualifiedId = ItemRegistry.ManuallyQualifyItemId(
                itemId.ToString(),
                "(O)");
            var processReserve = string.Equals(
                qualifiedId,
                request.ProcessInputQualifiedItemId,
                StringComparison.OrdinalIgnoreCase)
                    ? request.ProcessInputQuantity.Value
                    : 0;
            var additionalReserve = additionalRows
                .Where(row => string.Equals(
                    row.QualifiedItemId,
                    qualifiedId,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(row => row.Quantity);
            var totalRequired = checked(
                processReserve +
                additionalReserve);
            if (usesWorkbench)
            {
                workbenchFixture!.Chest.Items.Add(
                    ItemRegistry.Create(
                        qualifiedId,
                        ingredient.Value));
                ingredientSlots.Add(
                    workbenchFixture.ChestNodeId +
                    ":" + qualifiedId + ":" + ingredient.Value);
                if (totalRequired > 0 &&
                    EnsureInventoryItem(
                        qualifiedId,
                        totalRequired) < 0)
                {
                    return BlockedWithPrimitive(
                        request,
                        "debug_setup_machine_lifecycle_target",
                        "target_machine_fleet=empty;workbench_recipe_materials=ready",
                        "process_requirement=" + qualifiedId,
                        "machine_lifecycle_fixture_inventory_full");
                }
            }
            else
            {
                totalRequired = checked(
                    ingredient.Value + totalRequired);
                var slot = EnsureInventoryItem(
                    qualifiedId,
                    totalRequired);
                if (slot < 0)
                {
                    return BlockedWithPrimitive(
                        request,
                        "debug_setup_machine_lifecycle_target",
                        "target_machine_fleet=empty;recipe_and_process_materials=ready",
                        "requirement=" + qualifiedId,
                        "machine_lifecycle_fixture_inventory_full");
                }
                ingredientSlots.Add(
                    qualifiedId + "@" + slot + ":" + totalRequired);
            }
        }

        var processAdditionalReserve = additionalRows
            .Where(row => string.Equals(
                row.QualifiedItemId,
                request.ProcessInputQualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
            .Sum(row => row.Quantity);
        var processInputSlot = EnsureInventoryItem(
            request.ProcessInputQualifiedItemId,
            checked(
                request.ProcessInputQuantity.Value +
                processAdditionalReserve));
        var additionalSlots = new List<string>();
        foreach (var row in additionalRows)
        {
            var slot = EnsureInventoryItem(
                row.QualifiedItemId,
                row.Quantity);
            if (slot < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_machine_lifecycle_target",
                    "target_machine_fleet=empty;recipe_and_process_materials=ready",
                    "additional_item=" + row.QualifiedItemId,
                    "machine_lifecycle_fixture_inventory_full");
            }
            additionalSlots.Add(
                row.QualifiedItemId + "@" + slot + ":" + row.Quantity);
        }

        var moved = MoveFixtureFarmerToFarmAdjacent(
            target,
            out var stand,
            out var moveReason);
        var placedAfter = CountPlacedMachineInstances(
            request.OutputQualifiedItemId);
        var inventoryAfter = InventoryQualifiedCount(
            request.OutputQualifiedItemId);
        var verified =
            placedAfter == 0 &&
            inventoryAfter == 0 &&
            processInputSlot >= 0 &&
            (usesWorkbench
                ? workbenchFixture is not null &&
                  WorkbenchFixtureHasRecipeIngredients(
                      recipe,
                      workbenchFixture.Chest)
                : recipe.doesFarmerHaveIngredientsInInventory()) &&
            Game1.player.couldInventoryAcceptThisItem(machinePreview) &&
            moved &&
            ReferenceEquals(Game1.currentLocation, farm) &&
            Game1.player.TilePoint == stand;

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
            PrimitiveKind = "debug_setup_machine_lifecycle_target",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "all_loaded_target_machine_instances_removed",
                    "target_machine_inventory_count_zero",
                    "learned_native_recipe_preserved",
                    usesWorkbench
                        ? "exact_recipe_ingredients_available_in_native_workbench_chest"
                        : "exact_recipe_ingredients_available_in_player_inventory",
                    "process_input_and_additional_items_available",
                    "target_tile_cleared",
                    "player_moved_adjacent",
                    "placed_machine_count_before=" + placedBefore,
                    "inventory_machine_count_before=" + inventoryBefore,
                    "process_input_slot_index=" + processInputSlot,
                    "stand_tile=" + stand.X + "," + stand.Y,
                    "ingredient_slots=" + string.Join(",", ingredientSlots),
                    "additional_slots=" + string.Join(",", additionalSlots),
                    usesWorkbench && workbenchFixture is not null
                        ? "workbench_access_point_id=" +
                          workbenchFixture.AccessPointId
                        : "crafting_source=native_personal_crafting_menu"
                }
                : new[]
                {
                    "placed_machine_count_after=" + placedAfter,
                    "inventory_machine_count_after=" + inventoryAfter,
                    processInputSlot >= 0
                        ? "process_input_available"
                        : "process_input_unavailable",
                    (usesWorkbench
                        ? workbenchFixture is not null &&
                          WorkbenchFixtureHasRecipeIngredients(
                              recipe,
                              workbenchFixture.Chest)
                        : recipe.doesFarmerHaveIngredientsInInventory())
                        ? "recipe_ingredients_available"
                        : "recipe_ingredients_unavailable",
                    moved ? "player_moved_adjacent" : moveReason
                },
            RequestedEffect =
                "target_machine_fleet=empty;recipe_and_process_materials=ready",
            ObservedEffect =
                "recipe_name=" + request.RecipeName +
                ";machine_qualified_item_id=" +
                request.OutputQualifiedItemId +
                ";placed_machine_count_before=" + placedBefore +
                ";placed_machine_count_after=" + placedAfter +
                ";inventory_machine_count_before=" + inventoryBefore +
                ";inventory_machine_count_after=" + inventoryAfter +
                ";process_input_slot_index=" + processInputSlot +
                ";crafting_source=" + (
                    usesWorkbench
                        ? "native_workbench_crafting_menu"
                        : "native_personal_crafting_menu") +
                (workbenchFixture is null
                    ? string.Empty
                    : ";workbench_access_point_id=" +
                      workbenchFixture.AccessPointId +
                      ";workbench_container_node_id=" +
                      workbenchFixture.ChestNodeId) +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "machine_lifecycle_fixture_verification_failed"
                }
        };
    }

    private TrainingExecutionResult ExecuteCraftMachineItem(TrainingExecutionRequest request)
    {
        var primitiveKind = CraftingPrimitiveKind(request);
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
            return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_request_or_menu_state_invalid");
        }
        if (!Game1.player.craftingRecipes.TryGetValue(request.RecipeName, out var liveTimesCrafted) ||
            liveTimesCrafted != request.TimesCraftedBefore.Value ||
            !CraftingRecipe.craftingRecipes.ContainsKey(request.RecipeName))
        {
            return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_recipe_identity_or_count_drifted");
        }
        var questBefore = ReadCraftingQuestTerminalState(request);
        if (request.OptionId == "executor.craft_quest_item" &&
            (!questBefore.Present || questBefore.Completed ||
             !questBefore.TargetMatches))
        {
            return BlockedWithPrimitive(
                request,
                primitiveKind,
                requested,
                CraftMachineObservedEffect(request),
                "craft_quest_item_live_identity_or_target_drifted");
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
            return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_recipe_creation_failed:" + ex.GetType().Name);
        }
        if (recipe.itemToProduce.Count != 1 ||
            !string.Equals(preview.QualifiedItemId, request.OutputQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(preview.ItemId, request.OutputItemId, StringComparison.Ordinal) ||
            preview.Stack != request.OutputCount.Value)
        {
            return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_output_projection_drifted");
        }

        var projectedIngredients = ProjectNativePersonalCraftIngredients(recipe, Game1.player.Items);
        if (!projectedIngredients.Satisfied ||
            !JsonEquivalent(projectedIngredients.RowsJson, request.IngredientRowsJson))
        {
            return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_ingredient_projection_drifted");
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
            clickFound = TryClickCraftingRecipe(page, request.RecipeName);
            if (!clickFound || page.heldItem is null ||
                !string.Equals(page.heldItem.QualifiedItemId, request.OutputQualifiedItemId, StringComparison.Ordinal) ||
                page.heldItem.Stack != request.OutputCount.Value)
            {
                if (page.heldItem is null)
                {
                    page.exitThisMenuNoSound();
                }
                return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_native_recipe_click_failed");
            }

            var targetSlot = FindCraftedOutputInventorySlot(page.heldItem);
            if (targetSlot < 0 || targetSlot >= page.inventory.inventory.Count)
            {
                return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_output_inventory_slot_unavailable_after_consumption");
            }
            var target = page.inventory.inventory[targetSlot].bounds.Center;
            page.receiveLeftClick(target.X, target.Y, playSound: false);
            if (page.heldItem is not null)
            {
                return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_native_inventory_click_failed");
            }
            page.exitThisMenuNoSound();
        }
        catch (Exception ex)
        {
            if (page?.heldItem is null && ReferenceEquals(Game1.activeClickableMenu, page))
            {
                page?.exitThisMenuNoSound();
            }
            return BlockedWithPrimitive(request, primitiveKind, requested, CraftMachineObservedEffect(request), primitiveKind + "_native_menu_exception:" + ex.GetType().Name);
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
        var questAfter = ReadCraftingQuestTerminalState(request);
        var questTerminalVerified = request.OptionId !=
                "executor.craft_quest_item" ||
            !questAfter.Present || questAfter.Completed;
        var verified = clickFound && countMatch && recipeCountMatch &&
            menuClosed && questTerminalVerified;
        var achievementsAfter = string.Join(",", Game1.player.achievements.OrderBy(id => id));
        var questsAfter = CraftQuestSignature();
        var changedFacts = new List<SimulatedFactChange>();
        if (verified)
        {
            changedFacts.AddRange(affectedIds.Select(id => new SimulatedFactChange
            {
                Path = "player.inventory.qualified_count[" + id + "]",
                Before = beforeCounts[id].ToString(),
                After = afterCounts[id].ToString()
            }));
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.crafting_recipes[" + request.RecipeName + "]",
                Before = request.TimesCraftedBefore.Value.ToString(),
                After = timesAfter.ToString()
            });
            if (request.OptionId == "executor.craft_quest_item")
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "quests." + request.QuestCandidateId + ".terminal",
                    Before = "present=" + questBefore.Present.ToString().ToLowerInvariant() +
                        ";completed=" + questBefore.Completed.ToString().ToLowerInvariant(),
                    After = "present=" + questAfter.Present.ToString().ToLowerInvariant() +
                        ";completed=" + (!questAfter.Present || questAfter.Completed).ToString().ToLowerInvariant()
                });
            }
        }

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
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_CraftingPage_recipe_click_completed",
                    "native_CraftingPage_inventory_click_completed",
                    "exact_ingredient_and_output_multiset_verified",
                    "native_recipe_count_increment_verified",
                    request.OptionId == "executor.craft_quest_item"
                        ? "exact_CraftingQuest_native_OnRecipeCrafted_terminal_verified"
                        : "native_quest_callbacks_and_crafting_achievement_check_path_executed"
                }
                : new[]
                {
                    countMatch ? "inventory_multiset_match" : "inventory_multiset_mismatch",
                    recipeCountMatch ? "recipe_count_match" : "recipe_count_mismatch",
                    menuClosed ? "menu_closed" : "menu_not_closed",
                    questTerminalVerified
                        ? "quest_terminal_match"
                        : "quest_terminal_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = CraftMachineObservedEffect(request) +
                ";achievements_before=" + achievementsBefore + ";achievements_after=" + achievementsAfter +
                ";quest_signature_before=" + questsBefore + ";quest_signature_after=" + questsAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { primitiveKind + "_post_state_mismatch" },
            ChangedFacts = changedFacts.ToArray(),
            QuestCandidateId = request.OptionId == "executor.craft_quest_item" ? request.QuestCandidateId : string.Empty,
            QuestFamily = request.OptionId == "executor.craft_quest_item" ? request.QuestFamily : string.Empty,
            QuestId = request.OptionId == "executor.craft_quest_item" ? request.QuestId : string.Empty,
            QuestKey = request.OptionId == "executor.craft_quest_item" ? request.QuestKey : string.Empty,
            QuestObjectiveIndex = request.OptionId == "executor.craft_quest_item" ? request.QuestObjectiveIndex : null,
            QuestProgressBefore = request.OptionId == "executor.craft_quest_item" ? request.QuestExpectedCurrentCount : null,
            QuestProgressAfter = request.OptionId == "executor.craft_quest_item" ? request.QuestExpectedTargetCount : null,
            QuestTargetCount = request.OptionId == "executor.craft_quest_item" ? request.QuestExpectedTargetCount : null,
            QuestPresentBefore = request.OptionId == "executor.craft_quest_item" ? questBefore.Present : null,
            QuestPresentAfter = request.OptionId == "executor.craft_quest_item" ? questAfter.Present : null,
            QuestCompletedBefore = request.OptionId == "executor.craft_quest_item" ? questBefore.Completed : null,
            QuestCompletedAfter = request.OptionId == "executor.craft_quest_item" ? !questAfter.Present || questAfter.Completed : null
        };
    }

    private static string CraftingPrimitiveKind(
        TrainingExecutionRequest request) =>
        request.OptionId == "executor.craft_storage_item"
            ? "craft_storage_item"
            : request.OptionId == "executor.craft_quest_item"
                ? "craft_quest_item"
            : "craft_machine_item";

    private static CraftingQuestTerminalState ReadCraftingQuestTerminalState(
        TrainingExecutionRequest request)
    {
        if (request.OptionId != "executor.craft_quest_item")
        {
            return new CraftingQuestTerminalState(false, false, true);
        }
        var quest = Game1.player.questLog
            .OfType<StardewValley.Quests.CraftingQuest>()
            .SingleOrDefault(candidate => string.Equals(
                candidate.id.Value,
                request.QuestId,
                StringComparison.Ordinal));
        return quest is null
            ? new CraftingQuestTerminalState(false, false, true)
            : new CraftingQuestTerminalState(
                true,
                quest.completed.Value,
                string.Equals(
                    quest.ItemId.Value,
                    request.OutputQualifiedItemId,
                    StringComparison.Ordinal));
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
                var unitSalePrice = Math.Max(0, item.salePrice());
                consumed.Add(new
                {
                    slot_index = slot,
                    qualified_item_id = item.QualifiedItemId,
                    amount,
                    unit_sale_price = unitSalePrice,
                    total_sale_value = (long)unitSalePrice * amount
                });
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

    private static int CountPlacedMachineInstances(string qualifiedId)
    {
        var count = 0;
        Utility.ForEachLocation(
            location =>
            {
                count += location.objects.Pairs.Count(pair =>
                    string.Equals(
                        pair.Value.QualifiedItemId,
                        qualifiedId,
                        StringComparison.OrdinalIgnoreCase));
                return true;
            },
            includeInteriors: true,
            includeGenerated: false);
        return count;
    }

    private static MachineLifecycleAdditionalItem[]?
        ParseMachineLifecycleAdditionalItems(
            string json,
            out string reason)
    {
        reason = "available";
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<MachineLifecycleAdditionalItem>();
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                reason = "root_not_array";
                return null;
            }
            var rows = new List<MachineLifecycleAdditionalItem>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty(
                        "qualified_item_id",
                        out var qualifiedIdElement) ||
                    qualifiedIdElement.ValueKind != JsonValueKind.String ||
                    !element.TryGetProperty(
                        "quantity",
                        out var quantityElement) ||
                    !quantityElement.TryGetInt32(out var quantity) ||
                    quantity <= 0)
                {
                    reason = "invalid_item_row";
                    return null;
                }
                var qualifiedId =
                    qualifiedIdElement.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(qualifiedId))
                {
                    reason = "missing_qualified_item_id";
                    return null;
                }
                rows.Add(new MachineLifecycleAdditionalItem(
                    qualifiedId,
                    quantity));
            }
            return rows.ToArray();
        }
        catch (JsonException)
        {
            reason = "invalid_json";
            return null;
        }
    }

    private static string CraftQuestSignature() =>
        string.Join(",", Game1.player.questLog.OrderBy(quest => quest.id.Value).Select(quest => quest.id.Value + ":" + quest.questType.Value + ":" + quest.completed.Value));

    private static string CraftMachineRequestedEffect(TrainingExecutionRequest request) =>
        "player.inventory.materials_consumed_by_native_recipe=true;player.inventory.output_increases=" + request.OutputQualifiedItemId + ":" + request.OutputCount + ";player.crafting_recipes[" + request.RecipeName + "].count_increases=" + request.OutputCount;

    private static string CraftMachineObservedEffect(TrainingExecutionRequest request) =>
        "recipe=" + request.RecipeName + ";times_crafted=" + (Game1.player?.craftingRecipes.TryGetValue(request.RecipeName, out var count) == true ? count : -1) + ";output_count=" + InventoryQualifiedCount(request.OutputQualifiedItemId) + ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");

    private sealed record ProjectedCraftIngredients(string RowsJson, Dictionary<string, int> ConsumedByQualifiedId, bool Satisfied);

    private sealed record CraftingQuestTerminalState(
        bool Present,
        bool Completed,
        bool TargetMatches);

    private sealed record MachineLifecycleAdditionalItem(
        string QualifiedItemId,
        int Quantity);
}
