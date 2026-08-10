using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly Vector2[] NativeWorkbenchChestOffsets =
    {
        new(-1f, 1f), new(0f, 1f), new(1f, 1f),
        new(-1f, 0f), new(1f, 0f),
        new(-1f, -1f), new(0f, -1f), new(1f, -1f)
    };

    private void TickWorkbenchCraftSafely()
    {
        var active = activeWorkbenchCraft;
        if (active is null)
        {
            return;
        }
        try
        {
            TickWorkbenchCraft();
        }
        catch (Exception ex)
        {
            Monitor.Log("Workbench crafting failed and was blocked: " + ex, LogLevel.Error);
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_exception:" + ex.GetType().Name);
        }
    }

    private void StartWorkbenchCraft(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (Game1.player is null ||
            Game1.activeClickableMenu is not null ||
            string.IsNullOrWhiteSpace(request.RecipeName) ||
            string.IsNullOrWhiteSpace(request.OutputQualifiedItemId) ||
            string.IsNullOrWhiteSpace(request.WorkbenchAccessPointId) ||
            string.IsNullOrWhiteSpace(request.WorkbenchContainerNodeIdsJson) ||
            string.IsNullOrWhiteSpace(request.IngredientRowsJson) ||
            !request.OutputCount.HasValue ||
            !request.TimesCraftedBefore.HasValue ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue ||
            !request.StandTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_typed_request_required"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            !location.objects.TryGetValue(target.ToVector2(), out var value) ||
            value is not Workbench workbench ||
            !AreAdjacent(target, stand))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_target_drifted"));
            return;
        }

        var expectedAccessPoint = "access:workbench:" +
            EscapeMaterialNodePart(location.NameOrUniqueName) +
            ":" + target.X + "," + target.Y;
        if (!string.Equals(
                expectedAccessPoint,
                request.WorkbenchAccessPointId,
                StringComparison.Ordinal))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_access_point_drifted"));
            return;
        }

        var chests = NativeWorkbenchChestOffsets
            .Select(offset => target.ToVector2() + offset)
            .Where(tile => location.objects.TryGetValue(tile, out var adjacent) &&
                adjacent is Chest chest &&
                chest.SpecialChestType is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest)
            .Select(tile => new WorkbenchChest(
                (Chest)location.objects[tile],
                WorkbenchChestNodeId(location.NameOrUniqueName, tile)))
            .ToArray();
        string[] expectedNodeIds;
        try
        {
            expectedNodeIds =
                JsonSerializer.Deserialize<string[]>(request.WorkbenchContainerNodeIdsJson) ??
                Array.Empty<string>();
        }
        catch (JsonException)
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_container_node_ids_invalid"));
            return;
        }
        if (!expectedNodeIds.SequenceEqual(
                chests.Select(row => row.NodeId),
                StringComparer.Ordinal) ||
            workbench.mutex.IsLocked() && !workbench.mutex.IsLockHeld() ||
            chests.Any(row =>
                row.Chest.GetMutex().IsLocked() &&
                !row.Chest.GetMutex().IsLockHeld()))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_container_topology_or_lock_drifted"));
            return;
        }

        if (!Game1.player.craftingRecipes.TryGetValue(request.RecipeName, out var timesCrafted) ||
            timesCrafted != request.TimesCraftedBefore.Value ||
            !CraftingRecipe.craftingRecipes.ContainsKey(request.RecipeName))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_recipe_identity_or_count_drifted"));
            return;
        }
        var questBefore = ReadCraftingQuestTerminalState(request);
        if (request.OptionId == "executor.craft_quest_item" &&
            (!questBefore.Present || questBefore.Completed ||
             !questBefore.TargetMatches))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "craft_quest_item_live_identity_or_target_drifted"));
            return;
        }

        CraftingRecipe recipe;
        Item output;
        try
        {
            recipe = new CraftingRecipe(request.RecipeName, isCookingRecipe: false);
            output = recipe.createItem();
        }
        catch (Exception ex)
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_recipe_creation_failed:" + ex.GetType().Name));
            return;
        }
        if (recipe.itemToProduce.Count != 1 ||
            output.QualifiedItemId != request.OutputQualifiedItemId ||
            output.ItemId != request.OutputItemId ||
            output.Stack != request.OutputCount.Value)
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_output_projection_drifted"));
            return;
        }

        var inventories = chests.Select(row => (IInventory)row.Chest.Items).ToArray();
        var projection = ProjectNativeWorkbenchIngredients(
            recipe,
            inventories,
            chests.Select(row => row.NodeId).ToArray());
        if (!projection.Satisfied ||
            !JsonEquivalent(projection.RowsJson, request.IngredientRowsJson))
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_ingredient_projection_drifted"));
            return;
        }

        var path = TryBuildTilePath(
            location,
            Game1.player.TilePoint,
            stand,
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512),
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(WorkbenchCraftBlocked(
                request,
                "workbench_craft_path_unavailable:" + pathReason));
            return;
        }

        activeWorkbenchCraft = new ActiveWorkbenchCraft(
            pending,
            location,
            workbench,
            chests,
            inventories,
            recipe,
            projection,
            target,
            stand,
            path);
    }

    private void TickWorkbenchCraft()
    {
        var active = activeWorkbenchCraft;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_timeout");
            return;
        }

        switch (active.Stage)
        {
            case WorkbenchCraftStage.Move:
                TickWorkbenchCraftMove(active);
                break;
            case WorkbenchCraftStage.Open:
                OpenWorkbenchCraft(active);
                break;
            case WorkbenchCraftStage.WaitForMenu:
                WaitForWorkbenchCraftMenu(active);
                break;
            case WorkbenchCraftStage.Craft:
                CraftFromWorkbenchMenu(active);
                break;
            case WorkbenchCraftStage.WaitForUnlock:
                WaitForWorkbenchUnlock(active);
                break;
        }
    }

    private void TickWorkbenchCraftMove(ActiveWorkbenchCraft active)
    {
        var playerTile = Game1.player.TilePoint;
        if (playerTile == active.Stand)
        {
            StopAllMovement();
            active.Stage = WorkbenchCraftStage.Open;
            active.StageStartedAt = active.ElapsedTicks;
            return;
        }
        if (active.PathIndex >= active.Path.Count)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_path_exhausted");
            return;
        }
        var next = active.Path[active.PathIndex];
        if (playerTile == next)
        {
            active.PathIndex++;
            return;
        }
        if (!IsTileWalkable(active.Location, next) ||
            IsTileOccupiedByCharacter(active.Location, next))
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_dynamic_path_blocked");
            return;
        }
        var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
        active.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(playerTile, next));
        MovePlayerForTick();
        if (Game1.player.TilePoint == next)
        {
            active.PathIndex++;
        }
        active.StuckTicks = moved ? 0 : active.StuckTicks + 1;
        if (active.StuckTicks > 45)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_movement_stuck");
        }
    }

    private void OpenWorkbenchCraft(ActiveWorkbenchCraft active)
    {
        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        if (!TryApplySmapiRightButtonOverride(true, out var reason))
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_open_press_failed:" + reason);
            return;
        }
        var handled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(
                Game1.viewport.X,
                Game1.viewport.Y,
                Game1.viewport.Width,
                Game1.viewport.Height),
            Game1.player);
        TryApplySmapiRightButtonOverride(false, out _);
        if (!handled)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_native_open_not_handled");
            return;
        }
        active.Stage = WorkbenchCraftStage.WaitForMenu;
        active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitForWorkbenchCraftMenu(ActiveWorkbenchCraft active)
    {
        if (Game1.activeClickableMenu is CraftingPage page &&
            active.Workbench.mutex.IsLockHeld() &&
            active.Chests.All(row => row.Chest.GetMutex().IsLockHeld()) &&
            page._materialContainers is not null &&
            page._materialContainers.Count == active.Inventories.Length &&
            page._materialContainers
                .Zip(active.Inventories, ReferenceEquals)
                .All(matches => matches))
        {
            active.Page = page;
            active.Stage = WorkbenchCraftStage.Craft;
            active.StageStartedAt = active.ElapsedTicks;
            return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > 180)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_native_menu_timeout");
        }
    }

    private void CraftFromWorkbenchMenu(ActiveWorkbenchCraft active)
    {
        var request = active.Pending.Request;
        var page = active.Page;
        if (page is null ||
            !ReferenceEquals(Game1.activeClickableMenu, page) ||
            !active.Workbench.mutex.IsLockHeld() ||
            active.Chests.Any(row => !row.Chest.GetMutex().IsLockHeld()))
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_menu_or_lock_lost");
            return;
        }

        active.BeforeCounts = CaptureWorkbenchCraftCounts(
            active.Projection.ConsumedBySourceAndId.Keys,
            active.Chests);
        active.OutputBefore = InventoryQualifiedCount(request.OutputQualifiedItemId);
        var clicked = false;
        for (var pageIndex = 0; pageIndex < page.pagesOfCraftingRecipes.Count && !clicked; pageIndex++)
        {
            foreach (var pair in page.pagesOfCraftingRecipes[pageIndex])
            {
                if (pair.Value.name != request.RecipeName)
                {
                    continue;
                }
                page.currentCraftingPage = pageIndex;
                page.receiveLeftClick(
                    pair.Key.bounds.Center.X,
                    pair.Key.bounds.Center.Y,
                    playSound: false);
                clicked = true;
                break;
            }
        }
        if (!clicked ||
            page.heldItem is null ||
            page.heldItem.QualifiedItemId != request.OutputQualifiedItemId ||
            page.heldItem.Stack != request.OutputCount)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_native_recipe_click_failed");
            return;
        }
        var targetSlot = FindCraftedOutputInventorySlot(page.heldItem);
        if (targetSlot < 0 || targetSlot >= page.inventory.inventory.Count)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_output_slot_unavailable");
            return;
        }
        var target = page.inventory.inventory[targetSlot].bounds.Center;
        page.receiveLeftClick(target.X, target.Y, playSound: false);
        if (page.heldItem is not null)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_output_inventory_click_failed");
            return;
        }
        active.NativeRecipeClicked = true;
        page.exitThisMenuNoSound();
        active.Stage = WorkbenchCraftStage.WaitForUnlock;
        active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitForWorkbenchUnlock(ActiveWorkbenchCraft active)
    {
        if (Game1.activeClickableMenu is null &&
            !active.Workbench.mutex.IsLockHeld() &&
            active.Chests.All(row => !row.Chest.GetMutex().IsLockHeld()))
        {
            CompleteWorkbenchCraft(active);
            return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > 120)
        {
            CompleteWorkbenchCraftBlocked(active, "workbench_craft_native_lock_release_timeout");
        }
    }

    private void CompleteWorkbenchCraft(ActiveWorkbenchCraft active)
    {
        var request = active.Pending.Request;
        var afterCounts = CaptureWorkbenchCraftCounts(
            active.Projection.ConsumedBySourceAndId.Keys,
            active.Chests);
        var ingredientsMatch = active.Projection.ConsumedBySourceAndId.All(pair =>
            active.BeforeCounts.TryGetValue(pair.Key, out var before) &&
            afterCounts.TryGetValue(pair.Key, out var after) &&
            after == before - pair.Value);
        var outputAfter = InventoryQualifiedCount(request.OutputQualifiedItemId);
        var outputMatches = outputAfter == active.OutputBefore + request.OutputCount;
        var timesAfter = Game1.player.craftingRecipes.TryGetValue(
            request.RecipeName,
            out var times)
                ? times
                : -1;
        var recipeCountMatches =
            timesAfter == request.TimesCraftedBefore + request.OutputCount;
        var questAfter = ReadCraftingQuestTerminalState(request);
        var questTerminalMatches = request.OptionId !=
                "executor.craft_quest_item" ||
            !questAfter.Present || questAfter.Completed;
        var verified =
            active.NativeRecipeClicked &&
            ingredientsMatch &&
            outputMatches &&
            recipeCountMatches &&
            questTerminalMatches;
        var changedFacts = new List<SimulatedFactChange>();
        if (verified)
        {
            changedFacts.AddRange(active.Projection.ConsumedBySourceAndId.Select(pair =>
                new SimulatedFactChange
                {
                    Path = "farm.material_inventory_graph[" + pair.Key + "]",
                    Before = active.BeforeCounts[pair.Key].ToString(),
                    After = afterCounts[pair.Key].ToString()
                }));
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.inventory.qualified_count[" +
                    request.OutputQualifiedItemId + "]",
                Before = active.OutputBefore.ToString(),
                After = outputAfter.ToString()
            });
            if (request.OptionId == "executor.craft_quest_item")
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "quests." + request.QuestCandidateId + ".terminal",
                    Before = "present=true;completed=false",
                    After = "present=" + questAfter.Present.ToString().ToLowerInvariant() +
                        ";completed=" + (!questAfter.Present || questAfter.Completed).ToString().ToLowerInvariant()
                });
            }
        }
        activeWorkbenchCraft = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = CraftingPrimitiveKind(request),
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Workbench_checkForAction_completed",
                    "native_MultipleMutexRequest_acquired_and_released",
                    "native_CraftingPage_recipe_and_inventory_clicks_completed",
                    "exact_player_and_container_consumption_verified",
                    request.OptionId == "executor.craft_quest_item"
                        ? "exact_CraftingQuest_native_OnRecipeCrafted_terminal_verified"
                        : "native_quest_callback_path_executed"
                }
                : new[]
                {
                    ingredientsMatch ? "ingredient_sources_match" : "ingredient_sources_mismatch",
                    outputMatches ? "output_match" : "output_mismatch",
                    recipeCountMatches ? "recipe_count_match" : "recipe_count_mismatch",
                    questTerminalMatches
                        ? "quest_terminal_match"
                        : "quest_terminal_mismatch"
                },
            RequestedEffect = CraftMachineRequestedEffect(request),
            ObservedEffect = CraftMachineObservedEffect(request) +
                ";crafting_source=native_workbench_crafting_menu" +
                ";workbench_lock_released=" + (!active.Workbench.mutex.IsLockHeld()).ToString().ToLowerInvariant(),
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = active.ElapsedTicks,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "workbench_craft_post_state_mismatch" },
            ChangedFacts = changedFacts.ToArray(),
            QuestCandidateId = request.OptionId == "executor.craft_quest_item" ? request.QuestCandidateId : string.Empty,
            QuestFamily = request.OptionId == "executor.craft_quest_item" ? request.QuestFamily : string.Empty,
            QuestId = request.OptionId == "executor.craft_quest_item" ? request.QuestId : string.Empty,
            QuestKey = request.OptionId == "executor.craft_quest_item" ? request.QuestKey : string.Empty,
            QuestObjectiveIndex = request.OptionId == "executor.craft_quest_item" ? request.QuestObjectiveIndex : null,
            QuestProgressBefore = request.OptionId == "executor.craft_quest_item" ? request.QuestExpectedCurrentCount : null,
            QuestProgressAfter = request.OptionId == "executor.craft_quest_item" ? request.QuestExpectedTargetCount : null,
            QuestTargetCount = request.OptionId == "executor.craft_quest_item" ? request.QuestExpectedTargetCount : null,
            QuestPresentBefore = request.OptionId == "executor.craft_quest_item" ? true : null,
            QuestPresentAfter = request.OptionId == "executor.craft_quest_item" ? questAfter.Present : null,
            QuestCompletedBefore = request.OptionId == "executor.craft_quest_item" ? false : null,
            QuestCompletedAfter = request.OptionId == "executor.craft_quest_item" ? !questAfter.Present || questAfter.Completed : null
        });
    }

    private void CompleteWorkbenchCraftBlocked(
        ActiveWorkbenchCraft active,
        params string[] reasons)
    {
        StopAllMovement();
        TryApplySmapiRightButtonOverride(false, out _);
        if (Game1.activeClickableMenu is CraftingPage page)
        {
            if (page.heldItem is not null)
            {
                page.emergencyShutDown();
            }
            if (ReferenceEquals(Game1.activeClickableMenu, page) &&
                page.readyToClose())
            {
                page.exitThisMenuNoSound();
            }
        }
        activeWorkbenchCraft = null;
        active.Pending.Completion.SetResult(WorkbenchCraftBlocked(
            active.Pending.Request,
            reasons));
    }

    private static TrainingExecutionResult WorkbenchCraftBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            CraftingPrimitiveKind(request),
            CraftMachineRequestedEffect(request),
            CraftMachineObservedEffect(request) +
                ";crafting_source=native_workbench_crafting_menu",
            reasons);

    private static WorkbenchIngredientProjection ProjectNativeWorkbenchIngredients(
        CraftingRecipe recipe,
        IReadOnlyList<IInventory> containers,
        IReadOnlyList<string> containerNodeIds)
    {
        var playerRemaining = Game1.player.Items
            .Select(item => Math.Max(0, item?.Stack ?? 0))
            .ToArray();
        var containerRemaining = containers
            .Select(inventory => inventory
                .Select(item => Math.Max(0, item?.Stack ?? 0))
                .ToArray())
            .ToArray();
        var rows = new List<object>();
        var consumedBySourceAndId = new Dictionary<string, int>(StringComparer.Ordinal);
        var satisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var required = ingredient.Value;
            var available = WorkbenchMatchingTotal(
                Game1.player.Items,
                playerRemaining,
                ingredient.Key);
            for (var index = 0; index < containers.Count; index++)
            {
                available += WorkbenchMatchingTotal(
                    containers[index],
                    containerRemaining[index],
                    ingredient.Key);
            }
            var consumed = new List<object>();
            ConsumeProjectedWorkbenchIngredient(
                Game1.player.Items,
                playerRemaining,
                ingredient.Key,
                ref required,
                "player:" + Game1.player.UniqueMultiplayerID,
                consumed,
                consumedBySourceAndId);
            for (var index = 0; index < containers.Count && required > 0; index++)
            {
                ConsumeProjectedWorkbenchIngredient(
                    containers[index],
                    containerRemaining[index],
                    ingredient.Key,
                    ref required,
                    containerNodeIds[index],
                    consumed,
                    consumedBySourceAndId);
            }
            rows.Add(new
            {
                requirement_id_or_category = ingredient.Key,
                required_count = ingredient.Value,
                available_count_before_this_ingredient = available,
                satisfied = required == 0,
                native_consumption_plan = consumed.ToArray()
            });
            satisfied &= required == 0;
        }
        return new WorkbenchIngredientProjection(
            JsonSerializer.Serialize(rows),
            consumedBySourceAndId,
            satisfied);
    }

    private static int WorkbenchMatchingTotal(
        IList<Item?> inventory,
        int[] remaining,
        string requirement)
    {
        var total = 0;
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            if (remaining[slot] > 0 &&
                CraftingRecipe.ItemMatchesForCrafting(inventory[slot], requirement))
            {
                total += remaining[slot];
            }
        }
        return total;
    }

    private static void ConsumeProjectedWorkbenchIngredient(
        IList<Item?> inventory,
        int[] remaining,
        string requirement,
        ref int required,
        string sourceNodeId,
        ICollection<object> rows,
        IDictionary<string, int> consumedBySourceAndId)
    {
        for (var slot = inventory.Count - 1; slot >= 0 && required > 0; slot--)
        {
            var item = inventory[slot];
            if (item is null ||
                remaining[slot] <= 0 ||
                !CraftingRecipe.ItemMatchesForCrafting(item, requirement))
            {
                continue;
            }
            var amount = Math.Min(required, remaining[slot]);
            remaining[slot] -= amount;
            required -= amount;
            rows.Add(new
            {
                source_node_id = sourceNodeId,
                slot_index = slot,
                qualified_item_id = item.QualifiedItemId,
                amount
            });
            var key = sourceNodeId + "|" + item.QualifiedItemId;
            consumedBySourceAndId[key] =
                consumedBySourceAndId.TryGetValue(key, out var old)
                    ? old + amount
                    : amount;
        }
    }

    private static Dictionary<string, int> CaptureWorkbenchCraftCounts(
        IEnumerable<string> sourceAndIds,
        IReadOnlyList<WorkbenchChest> chests)
    {
        var chestByNode = chests.ToDictionary(row => row.NodeId, StringComparer.Ordinal);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in sourceAndIds)
        {
            var separator = key.IndexOf('|');
            var source = separator >= 0 ? key[..separator] : string.Empty;
            var qualifiedId = separator >= 0 ? key[(separator + 1)..] : string.Empty;
            IList<Item?> inventory = source.StartsWith("player:", StringComparison.Ordinal)
                ? Game1.player.Items
                : chestByNode.TryGetValue(source, out var chest)
                    ? chest.Chest.Items
                    : Array.Empty<Item?>();
            result[key] = inventory
                .Where(item => item is not null &&
                    item.QualifiedItemId == qualifiedId)
                .Sum(item => item!.Stack);
        }
        return result;
    }

    private static string WorkbenchChestNodeId(string locationId, Vector2 tile)
    {
        return "chest:" + EscapeMaterialNodePart(locationId) +
            ":" + (int)tile.X + "," + (int)tile.Y;
    }

    private enum WorkbenchCraftStage
    {
        Move,
        Open,
        WaitForMenu,
        Craft,
        WaitForUnlock
    }

    private sealed record WorkbenchChest(Chest Chest, string NodeId);

    private sealed record WorkbenchIngredientProjection(
        string RowsJson,
        Dictionary<string, int> ConsumedBySourceAndId,
        bool Satisfied);

    private sealed class ActiveWorkbenchCraft
    {
        public ActiveWorkbenchCraft(
            PendingExecution pending,
            GameLocation location,
            Workbench workbench,
            WorkbenchChest[] chests,
            IInventory[] inventories,
            CraftingRecipe recipe,
            WorkbenchIngredientProjection projection,
            Point target,
            Point stand,
            List<Point> path)
        {
            Pending = pending;
            Location = location;
            Workbench = workbench;
            Chests = chests;
            Inventories = inventories;
            Recipe = recipe;
            Projection = projection;
            Target = target;
            Stand = stand;
            Path = path;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Workbench Workbench { get; }
        public WorkbenchChest[] Chests { get; }
        public IInventory[] Inventories { get; }
        public CraftingRecipe Recipe { get; }
        public WorkbenchIngredientProjection Projection { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public WorkbenchCraftStage Stage { get; set; }
        public int StageStartedAt { get; set; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public CraftingPage? Page { get; set; }
        public bool NativeRecipeClicked { get; set; }
        public Dictionary<string, int> BeforeCounts { get; set; } =
            new(StringComparer.Ordinal);
        public int OutputBefore { get; set; }
    }
}
