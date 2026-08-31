using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeTailoringNativeContract =
        "live_Tailoring_action_or_BC247_checkAction_then_native_TailoringMenu_inventory_slot_clicks_start_1500ms_update_collect_leftovers_and_verify_without_direct_inventory_tailoredItems_boot_or_clothing_mutation";

    private void StartTailoring(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (activeTailoring is not null)
            reasons.Add("tailoring_executor_busy");
        if (request.TailoringOperation is not ("recipe" or "boots_stat_transfer") ||
            string.IsNullOrWhiteSpace(request.TailoringCandidateId) ||
            string.IsNullOrWhiteSpace(request.TailoringPurpose) ||
            string.IsNullOrWhiteSpace(request.TailoringSourceId) ||
            request.TailoringSourceKind is not ("tailoring_action" or "placed_sewing_machine") ||
            string.IsNullOrWhiteSpace(request.LocationId) ||
            !request.InteractionTileX.HasValue || !request.InteractionTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.LeftSourceId) || string.IsNullOrWhiteSpace(request.RightSourceId) ||
            string.IsNullOrWhiteSpace(request.LeftStateJson) || string.IsNullOrWhiteSpace(request.RightStateJson) ||
            request.TailoringSpendLeftCount != (request.TailoringOperation == "recipe" ? 1 : 0) ||
            request.TailoringSpendRightCount != 1 ||
            request.TailoringOutputContractKind is not ("exact_item_state" or "native_random_result_domain") ||
            request.TailoringMarksTailoredItem != (request.TailoringOperation == "recipe") ||
            !string.Equals(request.TailoringNativeContract, RuntimeTailoringNativeContract, StringComparison.Ordinal))
            reasons.Add("tailoring_typed_request_invalid");
        if (Game1.activeClickableMenu is not null)
            reasons.Add("tailoring_menu_must_be_clear");

        var location = Game1.currentLocation;
        var target = new Point(request.InteractionTileX ?? -1, request.InteractionTileY ?? -1);
        var stand = new Point(request.StandTileX ?? -1, request.StandTileY ?? -1);
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            !TailoringSourceMatches(location, request, target))
            reasons.Add("tailoring_source_location_or_endpoint_drifted");
        if (Math.Abs(stand.X - target.X) + Math.Abs(stand.Y - target.Y) != 1)
            reasons.Add("tailoring_stand_tile_not_adjacent");

        var left = RebindTailoringItem(request.LeftSourceId);
        var right = RebindTailoringItem(request.RightSourceId);
        if (left is null || right is null || ReferenceEquals(left, right) ||
            TailoringRuntimeItemStateJson(left) != request.LeftStateJson ||
            TailoringRuntimeItemStateJson(right) != request.RightStateJson)
            reasons.Add("tailoring_input_projection_drifted");
        if (left is Clothing clothing && clothing.dyeable.Value &&
            (right?.HasContextTag("color_prismatic") == true || TailoringMenu.GetDyeColor(right).HasValue))
            reasons.Add("tailoring_dye_branch_owned_by_player_command");

        Dictionary<string, int>? tailoredCounts = null;
        try
        {
            tailoredCounts = JsonSerializer.Deserialize<Dictionary<string, int>>(request.TailoringTailoredCountsBeforeJson);
        }
        catch (JsonException)
        {
            reasons.Add("tailoring_history_contract_invalid");
        }
        if (tailoredCounts is null || tailoredCounts.Any(pair =>
                (Game1.player.tailoredItems.TryGetValue(pair.Key, out var current) ? current : 0) != pair.Value))
            reasons.Add("tailoring_history_projection_drifted");

        List<Point>? path = null;
        if (location is not null)
        {
            path = TryBuildTilePath(
                location,
                Game1.player.TilePoint,
                stand,
                Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512),
                out var pathReason,
                avoidSoftObstacles: true,
                allowRemovableObstacles: false);
            if (path is null)
                reasons.Add("tailoring_path_unavailable:" + pathReason);
        }
        if (reasons.Count > 0 || location is null || left is null || right is null || path is null || tailoredCounts is null)
        {
            pending.Completion.SetResult(TailoringBlocked(request, reasons.ToArray()));
            return;
        }
        activeTailoring = new ActiveTailoring(pending, location, target, stand, path, left, right, tailoredCounts);
    }

    private void TickTailoringSafely()
    {
        if (activeTailoring is null)
            return;
        try
        {
            TickTailoring(activeTailoring);
        }
        catch (Exception ex)
        {
            CompleteTailoringBlocked(activeTailoring, "tailoring_executor_exception:" + ex.GetType().Name);
        }
    }

    private void TickTailoring(ActiveTailoring active)
    {
        active.ElapsedTicks++;
        if (active.ElapsedTicks > 2400)
        {
            CompleteTailoringBlocked(active, "tailoring_runtime_timeout");
            return;
        }
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteTailoringBlocked(active, "tailoring_location_changed");
            return;
        }
        switch (active.Stage)
        {
            case TailoringStage.Move: TickTailoringMove(active); break;
            case TailoringStage.Open: OpenTailoringMenu(active); break;
            case TailoringStage.WaitMenu: WaitTailoringMenu(active); break;
            case TailoringStage.LoadLeft: LoadTailoringLeft(active); break;
            case TailoringStage.LoadRight: LoadTailoringRight(active); break;
            case TailoringStage.Start: StartTailoringOperation(active); break;
            case TailoringStage.WaitComplete: WaitTailoringComplete(active); break;
            case TailoringStage.StoreOutput: StoreTailoringOutput(active); break;
            case TailoringStage.ReturnLeft: ReturnTailoringIngredient(active, left: true); break;
            case TailoringStage.ReturnRight: ReturnTailoringIngredient(active, left: false); break;
            case TailoringStage.Close: CloseTailoringMenu(active); break;
            case TailoringStage.Verify: CompleteTailoring(active); break;
        }
    }

    private void TickTailoringMove(ActiveTailoring active)
    {
        var tile = Game1.player.TilePoint;
        if (tile == active.Stand)
        {
            StopAllMovement();
            active.Stage = TailoringStage.Open;
            return;
        }
        if (active.PathIndex >= active.Path.Count)
        {
            CompleteTailoringBlocked(active, "tailoring_path_exhausted");
            return;
        }
        var next = active.Path[active.PathIndex];
        if (tile == next)
        {
            active.PathIndex++;
            return;
        }
        if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
        {
            CompleteTailoringBlocked(active, "tailoring_dynamic_path_blocked");
            return;
        }
        var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
        active.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(tile, next));
        MovePlayerForTick();
        if (Game1.player.TilePoint == next)
            active.PathIndex++;
        active.StuckTicks = moved ? 0 : active.StuckTicks + 1;
        if (active.StuckTicks > 45)
            CompleteTailoringBlocked(active, "tailoring_movement_stuck");
    }

    private void OpenTailoringMenu(ActiveTailoring active)
    {
        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var request = active.Pending.Request;
        bool handled;
        if (request.TailoringSourceKind == "tailoring_action")
        {
            if (!TryApplySmapiRightButtonOverride(true, out var reason))
            {
                CompleteTailoringBlocked(active, "tailoring_open_press_failed:" + reason);
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
            handled = active.Location.objects.TryGetValue(active.Target.ToVector2(), out var machine) &&
                machine.QualifiedItemId == "(BC)247" && machine.checkForAction(Game1.player);
        }
        if (!handled)
        {
            CompleteTailoringBlocked(active, "tailoring_native_open_not_handled");
            return;
        }
        active.Stage = TailoringStage.WaitMenu;
        active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitTailoringMenu(ActiveTailoring active)
    {
        if (Game1.activeClickableMenu is TailoringMenu menu)
        {
            active.Menu = menu;
            active.Stage = TailoringStage.LoadLeft;
            return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > 120)
            CompleteTailoringBlocked(active, "tailoring_native_menu_timeout");
    }

    private void LoadTailoringLeft(ActiveTailoring active)
    {
        var menu = active.Menu!;
        if (!ClickTailoringInventorySource(menu, active.Pending.Request.LeftSourceId, active.Left) || menu.heldItem != active.Left)
        {
            CompleteTailoringBlocked(active, "tailoring_left_pickup_failed");
            return;
        }
        ClickTailoringComponent(menu, menu.leftIngredientSpot);
        if (menu.heldItem is not null || menu.leftIngredientSpot.item != active.Left)
        {
            CompleteTailoringBlocked(active, "tailoring_left_slot_load_failed");
            return;
        }
        active.Stage = TailoringStage.LoadRight;
    }

    private void LoadTailoringRight(ActiveTailoring active)
    {
        var menu = active.Menu!;
        if (!ClickTailoringInventorySource(menu, active.Pending.Request.RightSourceId, active.Right) || menu.heldItem != active.Right)
        {
            CompleteTailoringBlocked(active, "tailoring_right_pickup_failed");
            return;
        }
        ClickTailoringComponent(menu, menu.rightIngredientSpot);
        if (menu.heldItem is not null || menu.rightIngredientSpot.item != active.Right)
        {
            CompleteTailoringBlocked(active, "tailoring_right_slot_load_failed");
            return;
        }
        active.Stage = TailoringStage.Start;
    }

    private void StartTailoringOperation(ActiveTailoring active)
    {
        var menu = active.Menu;
        var request = active.Pending.Request;
        if (menu is null || !ReferenceEquals(menu, Game1.activeClickableMenu) ||
            !menu.IsValidCraft(menu.leftIngredientSpot.item, menu.rightIngredientSpot.item))
        {
            CompleteTailoringBlocked(active, "tailoring_native_pair_rejected");
            return;
        }
        var recipe = menu.GetRecipeForItems(menu.leftIngredientSpot.item, menu.rightIngredientSpot.item);
        if (request.TailoringOperation == "recipe" && !string.Equals(recipe?.Id, request.TailoringRecipeId, StringComparison.Ordinal) ||
            request.TailoringOperation == "boots_stat_transfer" &&
            (menu.leftIngredientSpot.item is not Boots || menu.rightIngredientSpot.item is not Boots || recipe is not null))
        {
            CompleteTailoringBlocked(active, "tailoring_recipe_identity_drifted");
            return;
        }
        ClickTailoringComponent(menu, menu.startTailoringButton);
        if (!menu.IsBusy())
        {
            CompleteTailoringBlocked(active, "tailoring_native_start_rejected");
            return;
        }
        active.NativeOperationStarted = true;
        active.Stage = TailoringStage.WaitComplete;
    }

    private void WaitTailoringComplete(ActiveTailoring active)
    {
        var menu = active.Menu;
        if (menu is null || !ReferenceEquals(menu, Game1.activeClickableMenu))
        {
            CompleteTailoringBlocked(active, "tailoring_menu_lost_during_operation");
            return;
        }
        if (menu.IsBusy())
            return;
        active.Result = menu.heldItem;
        if (active.Result is null || !TailoringOutputMatches(active.Pending.Request, active.Result))
        {
            CompleteTailoringBlocked(active, "tailoring_output_outside_compiled_contract");
            return;
        }
        active.Stage = TailoringStage.StoreOutput;
    }

    private void StoreTailoringOutput(ActiveTailoring active)
    {
        if (!StoreTailoringHeldItem(active.Menu!))
        {
            CompleteTailoringBlocked(active, "tailoring_output_inventory_store_failed");
            return;
        }
        active.Stage = TailoringStage.ReturnLeft;
    }

    private void ReturnTailoringIngredient(ActiveTailoring active, bool left)
    {
        var menu = active.Menu!;
        var component = left ? menu.leftIngredientSpot : menu.rightIngredientSpot;
        if (component.item is not null)
        {
            ClickTailoringComponent(menu, component);
            if (menu.heldItem is null || !StoreTailoringHeldItem(menu))
            {
                CompleteTailoringBlocked(active, left
                    ? "tailoring_left_leftover_collection_failed"
                    : "tailoring_right_leftover_collection_failed");
                return;
            }
        }
        active.Stage = left ? TailoringStage.ReturnRight : TailoringStage.Close;
    }

    private void CloseTailoringMenu(ActiveTailoring active)
    {
        var menu = active.Menu!;
        if (!menu.readyToClose() || menu.heldItem is not null ||
            menu.leftIngredientSpot.item is not null || menu.rightIngredientSpot.item is not null)
        {
            CompleteTailoringBlocked(active, "tailoring_menu_not_cleanly_closeable");
            return;
        }
        menu.exitThisMenuNoSound();
        active.Stage = TailoringStage.Verify;
    }

    private void CompleteTailoring(ActiveTailoring active)
    {
        var request = active.Pending.Request;
        var result = active.Result;
        var outputValid = result is not null && TailoringOutputMatches(request, result);
        var countsValid = result is not null && TailoringCountsMatch(active, result);
        var historyValid = result is not null && TailoringHistoryMatches(active, result);
        var verified = active.NativeOperationStarted && outputValid && countsValid && historyValid &&
            Game1.activeClickableMenu is not TailoringMenu;
        activeTailoring = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "tailor_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_tailoring_endpoint_and_TailoringMenu_lifecycle_completed",
                    "exact_live_inputs_consumed_and_all_leftovers_collected",
                    request.TailoringOutputContractKind == "native_random_result_domain"
                        ? "native_random_output_inside_complete_published_domain"
                        : "exact_output_item_state_verified",
                    "tailored_history_delta_verified",
                    "direct_inventory_tailoredItems_boot_clothing_and_rng_mutation_not_used"
                }
                : new[] { "tailoring_post_state_mismatch" },
            RequestedEffect = "native_tailoring_completed=true;tailoring_operation=" + request.TailoringOperation +
                ";tailoring_purpose=" + request.TailoringPurpose,
            ObservedEffect = "output=" + (result is null ? "none" : TailoringRuntimeItemStateJson(result)) +
                ";counts_valid=" + countsValid.ToString().ToLowerInvariant() +
                ";history_valid=" + historyValid.ToString().ToLowerInvariant(),
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "tailoring_post_state_mismatch" }
        });
    }

    private static bool TailoringSourceMatches(GameLocation location, TrainingExecutionRequest request, Point target)
    {
        if (request.TailoringSourceKind == "tailoring_action")
            return request.TailoringSourceId == "tailoring-action:" + location.NameOrUniqueName + ":" + target.X + "," + target.Y &&
                location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") == "Tailoring" &&
                Game1.player.eventsSeen.Contains("992559");
        return request.TailoringSourceId == "sewing-machine:" + location.NameOrUniqueName + ":" + target.X + "," + target.Y &&
            location.objects.TryGetValue(target.ToVector2(), out var machine) && machine.QualifiedItemId == "(BC)247";
    }

    private static Item? RebindTailoringItem(string sourceId) =>
        sourceId.StartsWith("inventory:", StringComparison.Ordinal) && int.TryParse(sourceId[10..], out var slot) &&
        slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot]
            : null;

    private static bool ClickTailoringInventorySource(TailoringMenu menu, string sourceId, Item expected)
    {
        if (!sourceId.StartsWith("inventory:", StringComparison.Ordinal) || !int.TryParse(sourceId[10..], out var slot) ||
            slot < 0 || slot >= menu.inventory.inventory.Count)
            return false;
        ClickTailoringComponent(menu, menu.inventory.inventory[slot]);
        return ReferenceEquals(menu.heldItem, expected);
    }

    private static void ClickTailoringComponent(TailoringMenu menu, ClickableComponent component)
    {
        var point = component.bounds.Center;
        menu.receiveLeftClick(point.X, point.Y, playSound: false);
    }

    private static bool StoreTailoringHeldItem(TailoringMenu menu)
    {
        if (menu.heldItem is null)
            return true;
        var slot = FindCraftedOutputInventorySlot(menu.heldItem);
        if (slot < 0 || slot >= menu.inventory.inventory.Count)
            return false;
        ClickTailoringComponent(menu, menu.inventory.inventory[slot]);
        return menu.heldItem is null;
    }

    private static Dictionary<string, int> CaptureTailoringCounts() =>
        Game1.player.Items.Where(item => item is not null).Cast<Item>()
            .GroupBy(item => item.QualifiedItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Stack), StringComparer.Ordinal);

    private static bool TailoringCountsMatch(ActiveTailoring active, Item result)
    {
        var expected = new Dictionary<string, int>(active.BeforeCounts, StringComparer.Ordinal);
        AdjustTailoringCount(expected, active.Left.QualifiedItemId, -(active.Pending.Request.TailoringSpendLeftCount ?? 0));
        AdjustTailoringCount(expected, active.Right.QualifiedItemId, -(active.Pending.Request.TailoringSpendRightCount ?? 0));
        if (active.Pending.Request.TailoringOperation == "recipe")
            AdjustTailoringCount(expected, result.QualifiedItemId, result.Stack);
        var actual = CaptureTailoringCounts();
        return expected.Keys.Concat(actual.Keys).Distinct(StringComparer.Ordinal)
            .All(key => expected.GetValueOrDefault(key) == actual.GetValueOrDefault(key));
    }

    private static void AdjustTailoringCount(Dictionary<string, int> values, string id, int delta)
    {
        values[id] = values.GetValueOrDefault(id) + delta;
        if (values[id] == 0)
            values.Remove(id);
    }

    private static bool TailoringHistoryMatches(ActiveTailoring active, Item result)
    {
        var key = TailoringRuntimeHistoryKey(result);
        var before = active.TailoredCountsBefore.GetValueOrDefault(key);
        var after = Game1.player.tailoredItems.TryGetValue(key, out var value) ? value : 0;
        return after == before + (active.Pending.Request.TailoringMarksTailoredItem == true ? 1 : 0);
    }

    private static bool TailoringOutputMatches(TrainingExecutionRequest request, Item result)
    {
        var state = TailoringRuntimeItemStateJson(result);
        if (request.TailoringOutputContractKind == "exact_item_state")
            return state == request.ExpectedOutputStateJson;
        try
        {
            using var document = JsonDocument.Parse(request.RandomOutcomeContractJson);
            return document.RootElement.TryGetProperty("allowed_output_states", out var rows) &&
                rows.ValueKind == JsonValueKind.Array && rows.EnumerateArray().Any(row => row.GetRawText() == state);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string TailoringRuntimeItemStateJson(Item item) => JsonSerializer.Serialize(new
    {
        qualified_item_id = item.QualifiedItemId,
        runtime_type = item.GetType().FullName,
        stack = item.Stack,
        quality = item.Quality,
        quest_item = item is StardewValley.Object obj && obj.questItem.Value,
        boots_defense = item is Boots boots ? boots.defenseBonus.Value : 0,
        boots_immunity = item is Boots immunity ? immunity.immunityBonus.Value : 0,
        boots_sprite_index = item is Boots sprite ? sprite.indexInTileSheet.Value : -1,
        clothing_type = item is Clothing clothing ? clothing.clothesType.Value.ToString() : string.Empty,
        clothing_dyeable = item is Clothing dyeable && dyeable.dyeable.Value,
        clothing_prismatic = item is Clothing prismatic && prismatic.isPrismatic.Value,
        clothing_color = item is Clothing colored
            ? new { colored.clothesColor.Value.R, colored.clothesColor.Value.G, colored.clothesColor.Value.B, colored.clothesColor.Value.A }
            : null
    });

    private static string TailoringRuntimeHistoryKey(Item item)
    {
#pragma warning disable CS0618 // Native Farmer.MarkItemAsTailored uses this legacy key.
        return Utility.getStandardDescriptionFromItem(item, 1);
#pragma warning restore CS0618
    }

    private void CompleteTailoringBlocked(ActiveTailoring active, params string[] reasons)
    {
        StopAllMovement();
        if (active.Menu is { } menu && ReferenceEquals(menu, Game1.activeClickableMenu) && !menu.IsBusy())
            menu.exitThisMenuNoSound();
        activeTailoring = null;
        active.Pending.Completion.SetResult(TailoringBlocked(active.Pending.Request, reasons));
    }

    private static TrainingExecutionResult TailoringBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            "tailor_item",
            "native_tailoring_completed=true;tailoring_operation=" + request.TailoringOperation,
            "tailoring_blocked",
            reasons);
}
