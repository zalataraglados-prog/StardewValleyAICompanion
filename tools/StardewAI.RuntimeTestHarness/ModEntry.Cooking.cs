using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TickCookingSafely()
    {
        if (activeCooking is null)
        {
            return;
        }
        try
        {
            TickCooking();
        }
        catch (Exception ex)
        {
            Monitor.Log("Cooking failed and was blocked: " + ex, LogLevel.Error);
            CompleteCookingBlocked(activeCooking, "cook_recipe_exception:" + ex.GetType().Name);
        }
    }

    private void StartCooking(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (Game1.player is null || Game1.activeClickableMenu is not null ||
            string.IsNullOrWhiteSpace(request.RecipeName) || request.CraftCount != 1 ||
            string.IsNullOrWhiteSpace(request.CookingReason) ||
            string.IsNullOrWhiteSpace(request.CookingSourceId) ||
            request.CookingSourceKind is not ("kitchen" or "cookout_kit") ||
            string.IsNullOrWhiteSpace(request.LocationId) ||
            !request.InteractionTileX.HasValue || !request.InteractionTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.OutputQualifiedItemId) ||
            !request.OutputCount.HasValue || !request.ExpectedOutputQuality.HasValue ||
            !request.RecipesCookedBefore.HasValue ||
            string.IsNullOrWhiteSpace(request.IngredientRowsJson) ||
            string.IsNullOrWhiteSpace(request.SeasoningRowsJson) ||
            string.IsNullOrWhiteSpace(request.MaterialContainerIdsJson))
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_typed_request_required"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.InteractionTileX.Value, request.InteractionTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            Math.Abs(target.X - stand.X) + Math.Abs(target.Y - stand.Y) != 1)
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_location_or_stand_drifted"));
            return;
        }

        var source = RebindCookingSource(location, request, target);
        if (source is null)
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_native_source_or_topology_drifted"));
            return;
        }
        if (source.Mutexes.Any(mutex => mutex.IsLocked() && !mutex.IsLockHeld()))
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_native_mutex_locked_by_other"));
            return;
        }
        if (!Game1.player.cookingRecipes.ContainsKey(request.RecipeName) ||
            !CraftingRecipe.cookingRecipes.ContainsKey(request.RecipeName))
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_identity_or_learning_drifted"));
            return;
        }

        CraftingRecipe recipe;
        Item output;
        try
        {
            recipe = new CraftingRecipe(request.RecipeName, isCookingRecipe: true);
            output = recipe.createItem();
        }
        catch (Exception ex)
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_creation_failed:" + ex.GetType().Name));
            return;
        }
        var projection = ProjectNativeCookingIngredients(recipe, source);
        var expectedQuality = projection.SeasoningConsumed ? 2 : output.Quality;
        var outputOrderData = output is StardewValley.Object obj ? obj.orderData.Value ?? string.Empty : string.Empty;
        var cookedBefore = Game1.player.recipesCooked.TryGetValue(output.ItemId, out var cooked) ? cooked : 0;
        if (recipe.itemToProduce.Count != 1 ||
            output.ItemId != request.OutputItemId ||
            output.QualifiedItemId != request.OutputQualifiedItemId ||
            recipe.numberProducedPerCraft != request.OutputCount ||
            expectedQuality != request.ExpectedOutputQuality ||
            outputOrderData != request.ExpectedOutputOrderData ||
            cookedBefore != request.RecipesCookedBefore ||
            !projection.Satisfied ||
            !JsonEquivalent(projection.IngredientRowsJson, request.IngredientRowsJson) ||
            !JsonEquivalent(projection.SeasoningRowsJson, request.SeasoningRowsJson))
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_output_material_or_count_projection_drifted"));
            return;
        }

        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand,
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(CookingBlocked(request, "cook_recipe_path_unavailable:" + pathReason));
            return;
        }
        activeCooking = new ActiveCooking(pending, location, source, recipe, projection, target, stand, path);
    }

    private void TickCooking()
    {
        var active = activeCooking;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteCookingBlocked(active, "cook_recipe_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteCookingBlocked(active, "cook_recipe_timeout");
            return;
        }
        switch (active.Stage)
        {
            case CookingStage.Move:
                TickCookingMove(active);
                break;
            case CookingStage.Open:
                OpenCookingMenu(active);
                break;
            case CookingStage.WaitForMenu:
                WaitForCookingMenu(active);
                break;
            case CookingStage.Craft:
                CraftFromCookingMenu(active);
                break;
            case CookingStage.WaitForUnlock:
                WaitForCookingUnlock(active);
                break;
        }
    }

    private void TickCookingMove(ActiveCooking active)
    {
        var playerTile = Game1.player.TilePoint;
        if (playerTile == active.Stand)
        {
            StopAllMovement();
            active.Stage = CookingStage.Open;
            return;
        }
        if (active.PathIndex >= active.Path.Count)
        {
            CompleteCookingBlocked(active, "cook_recipe_path_exhausted");
            return;
        }
        var next = active.Path[active.PathIndex];
        if (playerTile == next)
        {
            active.PathIndex++;
            return;
        }
        if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
        {
            CompleteCookingBlocked(active, "cook_recipe_dynamic_path_blocked");
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
            CompleteCookingBlocked(active, "cook_recipe_movement_stuck");
        }
    }

    private void OpenCookingMenu(ActiveCooking active)
    {
        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        bool handled;
        if (active.Source.Kind == "kitchen")
        {
            if (!TryApplySmapiRightButtonOverride(true, out var reason))
            {
                CompleteCookingBlocked(active, "cook_recipe_open_press_failed:" + reason);
                return;
            }
            handled = active.Location.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            TryApplySmapiRightButtonOverride(false, out _);
        }
        else
        {
            handled = active.Source.Cookout?.checkForAction(Game1.player) == true;
        }
        if (!handled)
        {
            CompleteCookingBlocked(active, "cook_recipe_native_open_not_handled");
            return;
        }
        active.Stage = CookingStage.WaitForMenu;
        active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitForCookingMenu(ActiveCooking active)
    {
        if (Game1.activeClickableMenu is CraftingPage page && page.cooking &&
            CookingMenuContainersMatch(page, active.Source))
        {
            active.Page = page;
            active.Stage = CookingStage.Craft;
            return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > 180)
        {
            CompleteCookingBlocked(active, "cook_recipe_native_menu_timeout");
        }
    }

    private static bool CookingMenuContainersMatch(CraftingPage page, RuntimeCookingSource source)
    {
        if (source.Kind == "cookout_kit")
        {
            return page._materialContainers is null;
        }
        return source.Mutexes.All(mutex => mutex.IsLockHeld()) &&
            page._materialContainers is not null &&
            page._materialContainers.Count == source.Containers.Count &&
            page._materialContainers.Zip(source.Containers, ReferenceEquals).All(value => value);
    }

    private void CraftFromCookingMenu(ActiveCooking active)
    {
        var request = active.Pending.Request;
        var page = active.Page;
        if (page is null || !ReferenceEquals(page, Game1.activeClickableMenu) ||
            !CookingMenuContainersMatch(page, active.Source))
        {
            CompleteCookingBlocked(active, "cook_recipe_menu_or_lock_lost");
            return;
        }
        active.BeforeSourceCounts = CaptureCookingSourceCounts(active.Projection.ConsumedBySourceAndId.Keys, active.Source);
        active.OutputBefore = CountCookingOutput(request.OutputQualifiedItemId, request.ExpectedOutputQuality!.Value, request.ExpectedOutputOrderData);
        active.RecipesCookedBefore = Game1.player.recipesCooked.TryGetValue(request.OutputItemId, out var cooked) ? cooked : 0;
        active.AchievementsBefore = string.Join(",", Game1.player.achievements.OrderBy(value => value));
        active.QuestsBefore = CraftQuestSignature();

        if (!TryClickCraftingRecipe(page, request.RecipeName) ||
            page.heldItem is null ||
            page.heldItem.QualifiedItemId != request.OutputQualifiedItemId ||
            page.heldItem.Stack != request.OutputCount ||
            page.heldItem.Quality != request.ExpectedOutputQuality ||
            CookingOrderData(page.heldItem) != request.ExpectedOutputOrderData)
        {
            CompleteCookingBlocked(active, "cook_recipe_native_recipe_click_failed");
            return;
        }
        var slot = FindCraftedOutputInventorySlot(page.heldItem);
        if (slot < 0 || slot >= page.inventory.inventory.Count)
        {
            CompleteCookingBlocked(active, "cook_recipe_output_slot_unavailable");
            return;
        }
        var target = page.inventory.inventory[slot].bounds.Center;
        page.receiveLeftClick(target.X, target.Y, playSound: false);
        if (page.heldItem is not null)
        {
            CompleteCookingBlocked(active, "cook_recipe_output_inventory_click_failed");
            return;
        }
        active.NativeRecipeClicked = true;
        page.exitThisMenuNoSound();
        active.Stage = CookingStage.WaitForUnlock;
        active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitForCookingUnlock(ActiveCooking active)
    {
        if (Game1.activeClickableMenu is null && active.Source.Mutexes.All(mutex => !mutex.IsLockHeld()))
        {
            CompleteCooking(active);
            return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > 120)
        {
            CompleteCookingBlocked(active, "cook_recipe_native_lock_release_timeout");
        }
    }

    private void CompleteCooking(ActiveCooking active)
    {
        var request = active.Pending.Request;
        var afterCounts = CaptureCookingSourceCounts(active.Projection.ConsumedBySourceAndId.Keys, active.Source);
        var sourceCountsMatch = active.Projection.ConsumedBySourceAndId.All(pair =>
            active.BeforeSourceCounts.TryGetValue(pair.Key, out var before) &&
            afterCounts.TryGetValue(pair.Key, out var after) && after == before - pair.Value);
        var outputAfter = CountCookingOutput(request.OutputQualifiedItemId,
            request.ExpectedOutputQuality!.Value, request.ExpectedOutputOrderData);
        var outputMatches = outputAfter == active.OutputBefore + request.OutputCount;
        var recipesAfter = Game1.player.recipesCooked.TryGetValue(request.OutputItemId, out var cooked) ? cooked : 0;
        var recipeCountMatches = recipesAfter == active.RecipesCookedBefore + 1;
        var menuClosed = Game1.activeClickableMenu is null;
        var verified = active.NativeRecipeClicked && sourceCountsMatch && outputMatches && recipeCountMatches && menuClosed;
        var changes = new List<SimulatedFactChange>();
        if (verified)
        {
            changes.AddRange(active.Projection.ConsumedBySourceAndId.Select(pair => new SimulatedFactChange
            {
                Path = "cooking.material_source[" + pair.Key + "]",
                Before = active.BeforeSourceCounts[pair.Key].ToString(),
                After = afterCounts[pair.Key].ToString()
            }));
            changes.Add(new SimulatedFactChange
            {
                Path = "player.inventory.cooked_output[" + request.OutputQualifiedItemId + ":quality=" + request.ExpectedOutputQuality + "]",
                Before = active.OutputBefore.ToString(),
                After = outputAfter.ToString()
            });
            changes.Add(new SimulatedFactChange
            {
                Path = "player.recipes_cooked[" + request.OutputItemId + "]",
                Before = active.RecipesCookedBefore.ToString(),
                After = recipesAfter.ToString()
            });
        }
        activeCooking = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "cook_recipe",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_cooking_source_interaction_completed",
                    active.Source.Kind == "kitchen" ? "native_kitchen_mutexes_acquired_and_released" : "native_cookout_menu_opened",
                    "native_CraftingPage_recipe_and_inventory_clicks_completed",
                    "exact_material_and_optional_qi_seasoning_consumption_verified",
                    "exact_output_quality_order_data_and_recipesCooked_delta_verified",
                    "native_quest_and_cooking_achievement_callback_path_executed"
                }
                : new[]
                {
                    sourceCountsMatch ? "material_sources_match" : "material_sources_mismatch",
                    outputMatches ? "output_match" : "output_mismatch",
                    recipeCountMatches ? "recipe_count_match" : "recipe_count_mismatch",
                    menuClosed ? "menu_closed" : "menu_open"
                },
            RequestedEffect = "native_cook_recipe=" + request.RecipeName + ";craft_count=1;cooking_reason=" + request.CookingReason,
            ObservedEffect = "output=" + request.OutputQualifiedItemId + ";recipes_cooked=" + recipesAfter +
                ";achievements_before=" + active.AchievementsBefore +
                ";achievements_after=" + string.Join(",", Game1.player.achievements.OrderBy(value => value)) +
                ";quests_before=" + active.QuestsBefore + ";quests_after=" + CraftQuestSignature(),
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = active.ElapsedTicks,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "cook_recipe_post_state_mismatch" },
            ChangedFacts = changes.ToArray()
        });
    }

    private void CompleteCookingBlocked(ActiveCooking active, params string[] reasons)
    {
        StopAllMovement();
        TryApplySmapiRightButtonOverride(false, out _);
        if (Game1.activeClickableMenu is CraftingPage page)
        {
            if (page.heldItem is not null)
            {
                page.emergencyShutDown();
            }
            else if (page.readyToClose())
            {
                page.exitThisMenuNoSound();
            }
        }
        activeCooking = null;
        active.Pending.Completion.SetResult(CookingBlocked(active.Pending.Request, reasons));
    }

    private static TrainingExecutionResult CookingBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            "cook_recipe",
            "native_cook_recipe=" + request.RecipeName + ";craft_count=" + request.CraftCount,
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";recipes_cooked=" + (Game1.player?.recipesCooked.TryGetValue(request.OutputItemId, out var cooked) == true ? cooked : -1),
            reasons);

    private static RuntimeCookingSource? RebindCookingSource(
        GameLocation location,
        TrainingExecutionRequest request,
        Point target)
    {
        string[] expectedIds;
        try
        {
            expectedIds = JsonSerializer.Deserialize<string[]>(request.MaterialContainerIdsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return null;
        }
        if (request.CookingSourceKind == "cookout_kit")
        {
            var expectedSourceId = "cookout:" + location.NameOrUniqueName + ":" + target.X + "," + target.Y;
            return expectedIds.Length == 0 && request.CookingSourceId == expectedSourceId &&
                location.objects.TryGetValue(target.ToVector2(), out var value) &&
                value is Torch torch && value.QualifiedItemId == "(BC)278"
                    ? new RuntimeCookingSource(expectedSourceId, "cookout_kit", Array.Empty<IInventory>(),
                        Array.Empty<string>(), Array.Empty<NetMutex>(), torch)
                    : null;
        }
        var action = location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings");
        var sourceId = "kitchen:" + location.NameOrUniqueName + ":" + target.X + "," + target.Y;
        if (!string.Equals(action, "kitchen", StringComparison.OrdinalIgnoreCase) || request.CookingSourceId != sourceId)
        {
            return null;
        }
        var containers = new List<IInventory>();
        var ids = new List<string>();
        var mutexes = new List<NetMutex>();
        var fridge = location.GetFridge();
        if (fridge is not null)
        {
            containers.Add(fridge.Items);
            ids.Add("kitchen-fridge:" + location.NameOrUniqueName);
            mutexes.Add(fridge.mutex);
        }
        foreach (var pair in location.objects.Pairs)
        {
            if (pair.Value is Chest chest && chest.bigCraftable.Value && chest.fridge.Value)
            {
                containers.Add(chest.Items);
                ids.Add("mini-fridge:" + location.NameOrUniqueName + ":" + (int)pair.Key.X + "," + (int)pair.Key.Y);
                mutexes.Add(chest.mutex);
            }
        }
        return expectedIds.SequenceEqual(ids, StringComparer.Ordinal)
            ? new RuntimeCookingSource(sourceId, "kitchen", containers, ids, mutexes, null)
            : null;
    }

    private static NativeCookingProjection ProjectNativeCookingIngredients(
        CraftingRecipe recipe,
        RuntimeCookingSource source)
    {
        var playerRemaining = Game1.player.Items.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray();
        var containerRemaining = source.Containers
            .Select(inventory => inventory.Select(item => Math.Max(0, item?.Stack ?? 0)).ToArray()).ToArray();
        var seasoningBefore = CookingMatchingTotal(Game1.player.Items, playerRemaining, "917") +
            Enumerable.Range(0, source.Containers.Count)
                .Sum(index => CookingMatchingTotal(source.Containers[index], containerRemaining[index], "917"));
        var rows = new List<object>();
        var consumedBySourceAndId = new Dictionary<string, int>(StringComparer.Ordinal);
        var satisfied = true;
        foreach (var ingredient in recipe.recipeList)
        {
            var required = ingredient.Value;
            var available = CookingMatchingTotal(Game1.player.Items, playerRemaining, ingredient.Key) +
                Enumerable.Range(0, source.Containers.Count)
                    .Sum(index => CookingMatchingTotal(source.Containers[index], containerRemaining[index], ingredient.Key));
            var consumed = new List<object>();
            ProjectCookingConsumption(Game1.player.Items, playerRemaining, ingredient.Key, ref required,
                "player:" + Game1.player.UniqueMultiplayerID, consumed, consumedBySourceAndId);
            for (var index = 0; index < source.Containers.Count && required > 0; index++)
            {
                ProjectCookingConsumption(source.Containers[index], containerRemaining[index], ingredient.Key, ref required,
                    source.ContainerIds[index], consumed, consumedBySourceAndId);
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
        var seasoningRows = new List<object>();
        if (seasoningBefore > 0)
        {
            var required = 1;
            var consumed = new List<object>();
            ProjectCookingConsumption(Game1.player.Items, playerRemaining, "917", ref required,
                "player:" + Game1.player.UniqueMultiplayerID, consumed, consumedBySourceAndId);
            for (var index = 0; index < source.Containers.Count && required > 0; index++)
            {
                ProjectCookingConsumption(source.Containers[index], containerRemaining[index], "917", ref required,
                    source.ContainerIds[index], consumed, consumedBySourceAndId);
            }
            seasoningRows.Add(new
            {
                requirement_id_or_category = "917",
                required_count = 1,
                available_count_before_seasoning = seasoningBefore,
                satisfied = required == 0,
                native_consumption_plan = consumed.ToArray()
            });
        }
        return new NativeCookingProjection(
            JsonSerializer.Serialize(rows), JsonSerializer.Serialize(seasoningRows),
            consumedBySourceAndId, satisfied, seasoningBefore > 0);
    }

    private static int CookingMatchingTotal(IList<Item?> inventory, int[] remaining, string requirement)
    {
        var total = 0;
        for (var slot = 0; slot < inventory.Count; slot++)
        {
            if (remaining[slot] > 0 && CraftingRecipe.ItemMatchesForCrafting(inventory[slot], requirement))
            {
                total += remaining[slot];
            }
        }
        return total;
    }

    private static void ProjectCookingConsumption(
        IList<Item?> inventory,
        int[] remaining,
        string requirement,
        ref int required,
        string sourceId,
        ICollection<object> rows,
        IDictionary<string, int> totals)
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
            rows.Add(new
            {
                source_id = sourceId,
                slot_index = slot,
                qualified_item_id = item.QualifiedItemId,
                amount,
                unit_sale_price = Math.Max(0, item.salePrice()),
                total_sale_value = (long)Math.Max(0, item.salePrice()) * amount
            });
            var key = sourceId + "|" + item.QualifiedItemId;
            totals[key] = totals.TryGetValue(key, out var old) ? old + amount : amount;
        }
    }

    private static Dictionary<string, int> CaptureCookingSourceCounts(
        IEnumerable<string> keys,
        RuntimeCookingSource source)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var separator = key.IndexOf('|');
            var sourceId = separator < 0 ? string.Empty : key[..separator];
            var qualifiedId = separator < 0 ? string.Empty : key[(separator + 1)..];
            IList<Item?>? inventory = sourceId == "player:" + Game1.player.UniqueMultiplayerID
                ? Game1.player.Items
                : source.ContainerIds.ToList().IndexOf(sourceId) is var index && index >= 0
                    ? source.Containers[index]
                    : null;
            result[key] = inventory?.Where(item => item is not null && item.QualifiedItemId == qualifiedId)
                .Sum(item => item!.Stack) ?? -1;
        }
        return result;
    }

    private static int CountCookingOutput(string qualifiedId, int quality, string orderData) =>
        Game1.player.Items.Where(item => item is not null && item.QualifiedItemId == qualifiedId &&
            item.Quality == quality && CookingOrderData(item) == orderData).Sum(item => item!.Stack);

    private static string CookingOrderData(Item item) =>
        item is StardewValley.Object obj ? obj.orderData.Value ?? string.Empty : string.Empty;

    private enum CookingStage
    {
        Move,
        Open,
        WaitForMenu,
        Craft,
        WaitForUnlock
    }

    private sealed class ActiveCooking
    {
        public ActiveCooking(PendingExecution pending, GameLocation location, RuntimeCookingSource source,
            CraftingRecipe recipe, NativeCookingProjection projection, Point target, Point stand, List<Point> path)
        {
            Pending = pending;
            Location = location;
            Source = source;
            Recipe = recipe;
            Projection = projection;
            Target = target;
            Stand = stand;
            Path = path;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public RuntimeCookingSource Source { get; }
        public CraftingRecipe Recipe { get; }
        public NativeCookingProjection Projection { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int StageStartedAt { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public CookingStage Stage { get; set; }
        public CraftingPage? Page { get; set; }
        public bool NativeRecipeClicked { get; set; }
        public Dictionary<string, int> BeforeSourceCounts { get; set; } = new(StringComparer.Ordinal);
        public int OutputBefore { get; set; }
        public int RecipesCookedBefore { get; set; }
        public string AchievementsBefore { get; set; } = string.Empty;
        public string QuestsBefore { get; set; } = string.Empty;
    }

    private sealed record RuntimeCookingSource(
        string SourceId,
        string Kind,
        IReadOnlyList<IInventory> Containers,
        IReadOnlyList<string> ContainerIds,
        IReadOnlyList<NetMutex> Mutexes,
        Torch? Cookout);

    private sealed record NativeCookingProjection(
        string IngredientRowsJson,
        string SeasoningRowsJson,
        Dictionary<string, int> ConsumedBySourceAndId,
        bool Satisfied,
        bool SeasoningConsumed);
}
