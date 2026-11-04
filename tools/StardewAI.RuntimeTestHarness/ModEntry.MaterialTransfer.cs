using Microsoft.Xna.Framework;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TickMaterialTransferSafely()
    {
        var active = activeMaterialTransfer;
        if (active is null)
        {
            return;
        }

        try
        {
            TickMaterialTransfer();
        }
        catch (Exception ex)
        {
            Monitor.Log(
                "Material transfer execution failed and was blocked: " + ex,
                LogLevel.Error);
            CompleteMaterialTransferBlocked(
                active,
                "material_transfer_execution_exception:" + ex.GetType().Name);
        }
    }

    private void StartMaterialTransfer(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var intent = request.MaterialTransferIntent;
        var projection = request.MaterialTransferProjection;
        if (intent is null ||
            projection is null ||
            projection.Status != "projected" ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue ||
            !request.StandTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_typed_projection_required"));
            return;
        }

        if (Game1.activeClickableMenu is not null ||
            Game1.dialogueUp ||
            Game1.player.UsingTool ||
            !Game1.player.CanMove)
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!string.Equals(
                location.NameOrUniqueName,
                request.LocationId,
                StringComparison.Ordinal) ||
            !location.objects.TryGetValue(target.ToVector2(), out var value) ||
            value is not Chest chest ||
            chest.GetType() != typeof(Chest) ||
            !chest.playerChest.Value ||
            chest.giftbox.Value ||
            chest.SpecialChestType != Chest.SpecialChestTypes.None ||
            chest.fridge.Value ||
            !AreAdjacent(target, stand))
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_chest_target_drifted"));
            return;
        }

        var playerNodeId = "player:" + Game1.player.UniqueMultiplayerID;
        var chestNodeId = "chest:" +
            EscapeMaterialNodePart(location.NameOrUniqueName) +
            ":" + target.X + "," + target.Y;
        var sourceIsPlayer = intent.SourceNodeId == playerNodeId &&
            intent.DestinationNodeId == chestNodeId;
        var sourceIsChest = intent.SourceNodeId == chestNodeId &&
            intent.DestinationNodeId == playerNodeId;
        if (!sourceIsPlayer && !sourceIsChest)
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_node_identity_drifted"));
            return;
        }

        if (!MaterialChestActorUseAuthorized(chest))
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_chest_not_actor_authorized"));
            return;
        }

        if (chest.GetMutex().IsLocked() && !chest.GetMutex().IsLockHeld())
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_chest_locked_by_other_player"));
            return;
        }

        var source = sourceIsPlayer
            ? Game1.player.Items
            : chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var sourceItem = ItemAt(source, intent.SourceSlotIndex);
        if (!MatchesMaterialTransferItem(sourceItem, intent) ||
            sourceItem!.Stack != intent.ExpectedSourceStack ||
            intent.Quantity <= 0 ||
            intent.Quantity > sourceItem.Stack)
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_source_projection_drifted"));
            return;
        }

        var destination = sourceIsPlayer
            ? chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)
            : Game1.player.Items;
        var destinationQuantityBefore = MaterialQuantity(destination, intent);
        if (destinationQuantityBefore != projection.DestinationQuantityBefore)
        {
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_destination_projection_drifted"));
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
            pending.Completion.SetResult(MaterialTransferBlocked(
                pending,
                null,
                "material_transfer_path_unavailable:" + pathReason));
            return;
        }

        activeMaterialTransfer = new ActiveMaterialTransfer(
            pending,
            location,
            chest,
            target,
            stand,
            path,
            sourceIsPlayer,
            intent.ExpectedSourceStack,
            destinationQuantityBefore,
            sourceItem!,
            MaterialQuantity(source, intent));
    }

    private void TickMaterialTransfer()
    {
        var active = activeMaterialTransfer;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_timeout");
            return;
        }

        if (active.Stage == MaterialTransferStage.Move)
        {
            TickMaterialTransferMove(active);
            return;
        }

        if (active.Stage == MaterialTransferStage.Open)
        {
            StopAllMovement();
            if (Game1.player.TilePoint != active.Stand || !AreAdjacent(active.Stand, active.Target) ||
                Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp ||
                Game1.player.UsingTool || !Game1.player.CanMove ||
                !active.Location.objects.TryGetValue(active.Target.ToVector2(), out var target) ||
                !ReferenceEquals(target, active.Chest) || active.Chest.giftbox.Value ||
                !MaterialChestActorUseAuthorized(active.Chest) || NativeRightClickEdgePatch.Active ||
                active.Chest.GetMutex().IsLocked() && !active.Chest.GetMutex().IsLockHeld())
            {
                CompleteMaterialTransferBlocked(active, "material_transfer_live_open_context_drifted");
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            NativeRightClickEdgePatch.Arm();
            bool handled;
            bool rightClickObserved;
            try
            {
                handled = active.Chest.checkForAction(Game1.player);
                rightClickObserved = NativeRightClickEdgePatch.WasObserved;
            }
            finally
            {
                NativeRightClickEdgePatch.Clear();
            }
            if (!handled || !rightClickObserved)
            {
                CompleteMaterialTransferBlocked(active, "material_transfer_native_open_not_handled");
                return;
            }
            active.Stage = MaterialTransferStage.WaitForMenu;
            active.StageStartedAt = active.ElapsedTicks;
            return;
        }

        if (active.Stage == MaterialTransferStage.WaitForMenu)
        {
            if (Game1.activeClickableMenu is ItemGrabMenu menu &&
                ReferenceEquals(menu.sourceItem, active.Chest) &&
                active.Chest.GetMutex().IsLockHeld())
            {
                active.NativeMenuOpened = true;
                active.Stage = MaterialTransferStage.Transfer;
                return;
            }
            if (active.ElapsedTicks - active.StageStartedAt > MaterialTransferBudgetPolicy.MenuWaitTicks)
            {
                CompleteMaterialTransferBlocked(active, "material_transfer_native_menu_timeout");
            }
            return;
        }

        if (active.Stage == MaterialTransferStage.Transfer)
        {
            TickMaterialTransferClick(active);
            return;
        }

        if (active.Stage == MaterialTransferStage.CloseMenu)
        {
            if (Game1.activeClickableMenu is not ItemGrabMenu menu ||
                !ReferenceEquals(menu.sourceItem, active.Chest))
            {
                CompleteMaterialTransferBlocked(active, "material_transfer_menu_identity_drifted");
                return;
            }
            if (!menu.readyToClose())
            {
                CompleteMaterialTransferBlocked(active, "material_transfer_menu_not_ready_to_close");
                return;
            }
            Game1.exitActiveMenu();
            active.Stage = MaterialTransferStage.WaitForUnlock;
            active.StageStartedAt = active.ElapsedTicks;
            return;
        }

        if (Game1.activeClickableMenu is null &&
            !active.Chest.GetMutex().IsLockHeld())
        {
            CompleteMaterialTransfer(active);
            return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > MaterialTransferBudgetPolicy.UnlockWaitTicks)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_native_lock_release_timeout");
        }
    }

    private void TickMaterialTransferMove(ActiveMaterialTransfer active)
    {
        var playerTile = Game1.player.TilePoint;
        if (playerTile == active.Stand)
        {
            StopAllMovement();
            active.Stage = MaterialTransferStage.Open;
            active.StageStartedAt = active.ElapsedTicks;
            return;
        }
        if (active.PathIndex >= active.Path.Count)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_path_exhausted");
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
            CompleteMaterialTransferBlocked(active, "material_transfer_dynamic_path_blocked");
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
            CompleteMaterialTransferBlocked(active, "material_transfer_movement_stuck");
        }
    }

    private void TickMaterialTransferClick(ActiveMaterialTransfer active)
    {
        var request = active.Pending.Request;
        var intent = request.MaterialTransferIntent!;
        var projection = request.MaterialTransferProjection!;
        if (active.ClickCount >= intent.Quantity)
        {
            active.Stage = MaterialTransferStage.CloseMenu;
            active.StageStartedAt = active.ElapsedTicks;
            return;
        }
        if (Game1.activeClickableMenu is not ItemGrabMenu menu ||
            !ReferenceEquals(menu.sourceItem, active.Chest) ||
            !active.Chest.GetMutex().IsLockHeld() ||
            !MaterialChestActorUseAuthorized(active.Chest))
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_menu_or_lock_lost");
            return;
        }

        var source = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var sourceSlotIndex = MaterialSourceReferenceIndex(source, active.SourceItem);
        var before = ItemAt(source, sourceSlotIndex);
        var expectedBefore = active.SourceStackBefore - active.ClickCount;
        if (!MatchesMaterialTransferItem(before, intent) ||
            before!.Stack != expectedBefore)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_source_drifted_before_click");
            return;
        }

        var sourceMenu = active.SourceIsPlayer ? menu.inventory : menu.ItemsToGrabMenu;
        var position = InventorySlotScreenPosition(sourceMenu, sourceSlotIndex);
        if (!position.HasValue)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_slot_screen_position_unavailable");
            return;
        }

        // InventoryMenu.rightClick reads oldKBState; Shift would take half a stack.
        var oldKeyboard = Game1.oldKBState;
        try
        {
            Game1.oldKBState = default;
            menu.receiveRightClick(position.Value.X, position.Value.Y, playSound: true);
        }
        finally
        {
            Game1.oldKBState = oldKeyboard;
        }
        active.ClickCount++;
        var sourceAfter = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var destinationAfter = active.SourceIsPlayer
            ? active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)
            : Game1.player.Items;
        var sourceStackAfter = MaterialSourceReferenceStack(sourceAfter, active.SourceItem, intent);
        var destinationQuantityAfter = MaterialQuantity(destinationAfter, intent);
        if (sourceStackAfter != active.SourceStackBefore - active.ClickCount ||
            destinationQuantityAfter != active.DestinationQuantityBefore + active.ClickCount ||
            destinationQuantityAfter > projection.DestinationQuantityAfter)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_native_click_postcondition_failed");
        }
    }

    private static bool MaterialChestActorUseAuthorized(Chest chest)
    {
        return string.IsNullOrWhiteSpace(chest.GlobalInventoryId) &&
            chest.SpecialChestType != Chest.SpecialChestTypes.JunimoChest &&
            chest.owner.Value == Game1.player.UniqueMultiplayerID;
    }

    private void CompleteMaterialTransfer(ActiveMaterialTransfer active)
    {
        StopAllMovement();
        var intent = active.Pending.Request.MaterialTransferIntent!;
        var source = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var destination = active.SourceIsPlayer
            ? active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)
            : Game1.player.Items;
        var sourceAfter = MaterialSourceReferenceStack(source, active.SourceItem, intent);
        var destinationAfter = MaterialQuantity(destination, intent);
        activeMaterialTransfer = null;
        active.Pending.Completion.SetResult(MaterialTransferResult(
            active,
            "applied",
            sourceAfter,
            destinationAfter,
            Array.Empty<string>()));
    }

    private void CompleteMaterialTransferBlocked(
        ActiveMaterialTransfer active,
        params string[] reasons)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is ItemGrabMenu menu &&
            ReferenceEquals(menu.sourceItem, active.Chest) &&
            menu.readyToClose())
        {
            Game1.exitActiveMenu();
        }
        var intent = active.Pending.Request.MaterialTransferIntent!;
        var source = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var destination = active.SourceIsPlayer
            ? active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)
            : Game1.player.Items;
        var sourceAfter = MaterialSourceReferenceStack(source, active.SourceItem, intent);
        var destinationAfter = MaterialQuantity(destination, intent);
        activeMaterialTransfer = null;
        active.Pending.Completion.SetResult(MaterialTransferResult(
            active,
            "blocked",
            sourceAfter,
            destinationAfter,
            reasons));
    }

    private static TrainingExecutionResult MaterialTransferBlocked(
        PendingExecution pending,
        ActiveMaterialTransfer? active,
        params string[] reasons)
    {
        var result = BlockedWithPrimitive(
            pending.Request,
            "transfer_material",
            MaterialTransferRequestedEffect(pending.Request),
            "material_transfer=not_started",
            reasons);
        result.MaterialTransferIntent = pending.Request.MaterialTransferIntent;
        result.MaterialTransferProjection = pending.Request.MaterialTransferProjection;
        result.MaterialTransferClickCount = active?.ClickCount ?? 0;
        return result;
    }

    private static TrainingExecutionResult MaterialTransferResult(
        ActiveMaterialTransfer active,
        string status,
        int sourceAfter,
        int destinationAfter,
        string[] reasons)
    {
        var request = active.Pending.Request;
        var sourceItems = active.SourceIsPlayer ? Game1.player.Items :
            active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var sourceSlotAfter = MaterialSourceReferenceIndex(sourceItems, active.SourceItem);
        var result = new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            PrimitiveKind = "transfer_material",
            PrimitiveVerificationStatus = status == "applied" ? "verified" : "blocked",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = MaterialTransferRequestedEffect(request),
            ObservedEffect = "source_stack_after=" + sourceAfter +
                ";destination_quantity_after=" + destinationAfter +
                ";menu_opened=" + active.NativeMenuOpened.ToString().ToLowerInvariant() +
                ";lock_released=" + (!active.Chest.GetMutex().IsLockHeld()).ToString().ToLowerInvariant(),
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = active.ElapsedTicks,
            BlockReasons = reasons,
            MaterialTransferIntent = request.MaterialTransferIntent,
            MaterialTransferProjection = request.MaterialTransferProjection,
            MaterialTransferClickCount = active.ClickCount,
            MaterialTransferSourceStackBefore = active.SourceStackBefore,
            MaterialTransferSourceStackAfter = sourceAfter,
            MaterialTransferSourceSlotAfter = sourceSlotAfter >= 0 ? sourceSlotAfter : null,
            MaterialTransferDestinationQuantityBefore = active.DestinationQuantityBefore,
            MaterialTransferDestinationQuantityAfter = destinationAfter,
            MaterialTransferNativeMenuOpened = active.NativeMenuOpened,
            MaterialTransferNativeLockReleased = !active.Chest.GetMutex().IsLockHeld(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "farm.material_inventory_graph.inventory_nodes[" +
                        request.MaterialTransferIntent!.SourceNodeId +
                        "].quantity[" + request.MaterialTransferIntent.QualifiedItemId +
                        ";quality=" + request.MaterialTransferIntent.Quality + "]",
                    Before = active.SourceQuantityBefore.ToString(),
                    After = MaterialQuantity(sourceItems, request.MaterialTransferIntent).ToString()
                },
                new SimulatedFactChange
                {
                    Path = "farm.material_inventory_graph.inventory_nodes[" +
                        request.MaterialTransferIntent.DestinationNodeId +
                        "].quantity[" +
                        request.MaterialTransferIntent.QualifiedItemId +
                        ";quality=" +
                        request.MaterialTransferIntent.Quality +
                        "]",
                    Before = active.DestinationQuantityBefore.ToString(),
                    After = destinationAfter.ToString()
                }
            }
        };
        return result;
    }

    private static Point? InventorySlotScreenPosition(
        InventoryMenu inventory,
        int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventory.inventory.Count)
        {
            return null;
        }
        var bounds = inventory.inventory[slotIndex].bounds;
        return new Point(bounds.Center.X, bounds.Center.Y);
    }

    private static Item? ItemAt(IList<Item?> items, int index) =>
        index >= 0 && index < items.Count ? items[index] : null;

    private static bool MatchesMaterialTransferItem(
        Item? item,
        MaterialTransferIntent intent) =>
        item is not null &&
        string.Equals(
            item.QualifiedItemId,
            intent.QualifiedItemId,
            StringComparison.Ordinal) &&
        item.Quality == intent.Quality;

    private static int MaterialQuantity(
        IList<Item?> items,
        MaterialTransferIntent intent) =>
        items.Where(item => MatchesMaterialTransferItem(item, intent))
            .Sum(item => item!.Stack);

    private static string MaterialTransferRequestedEffect(
        TrainingExecutionRequest request) =>
        request.MaterialTransferIntent is not { } intent
            ? "material_transfer=missing"
            : "source_node=" + intent.SourceNodeId +
              ";destination_node=" + intent.DestinationNodeId +
              ";qualified_item_id=" + intent.QualifiedItemId +
              ";quality=" + intent.Quality +
              ";quantity=" + intent.Quantity;

    private static string EscapeMaterialNodePart(string value) =>
        value.Replace("%", "%25", StringComparison.Ordinal)
            .Replace(":", "%3A", StringComparison.Ordinal);

    private enum MaterialTransferStage
    {
        Move,
        Open,
        WaitForMenu,
        Transfer,
        CloseMenu,
        WaitForUnlock
    }

    private sealed class ActiveMaterialTransfer
    {
        public ActiveMaterialTransfer(
            PendingExecution pending,
            GameLocation location,
            Chest chest,
            Point target,
            Point stand,
            List<Point> path,
            bool sourceIsPlayer,
            int sourceStackBefore,
            int destinationQuantityBefore,
            Item sourceItem,
            int sourceQuantityBefore)
        {
            Pending = pending;
            Location = location;
            Chest = chest;
            Target = target;
            Stand = stand;
            Path = path;
            SourceIsPlayer = sourceIsPlayer;
            SourceStackBefore = sourceStackBefore;
            DestinationQuantityBefore = destinationQuantityBefore;
            SourceItem = sourceItem;
            SourceQuantityBefore = sourceQuantityBefore;
            LastPosition = Game1.player.Position;
            MaxTicks = Math.Max(
                600,
                path.Count * 90 +
                pending.Request.MaterialTransferIntent!.Quantity * 8 +
                360);
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Chest Chest { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public bool SourceIsPlayer { get; }
        public int SourceStackBefore { get; }
        public int DestinationQuantityBefore { get; }
        public Item SourceItem { get; }
        public int SourceQuantityBefore { get; }
        public int MaxTicks { get; }
        public string StartedAt { get; }
        public MaterialTransferStage Stage { get; set; }
        public int StageStartedAt { get; set; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int ClickCount { get; set; }
        public bool NativeMenuOpened { get; set; }
        public Vector2 LastPosition { get; set; }
    }
}
