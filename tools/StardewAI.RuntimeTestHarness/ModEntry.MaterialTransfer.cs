using Microsoft.Xna.Framework;
using StardewAI.Contracts.Execution;
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
            destinationQuantityBefore);
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
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            if (!TryApplySmapiRightButtonOverride(true, out var pressReason))
            {
                CompleteMaterialTransferBlocked(active, "material_transfer_open_press_failed:" + pressReason);
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
            if (active.ElapsedTicks - active.StageStartedAt > 180)
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
        if (active.ElapsedTicks - active.StageStartedAt > 120)
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
            !active.Chest.GetMutex().IsLockHeld())
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_menu_or_lock_lost");
            return;
        }

        var source = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var before = ItemAt(source, intent.SourceSlotIndex);
        var expectedBefore = active.SourceStackBefore - active.ClickCount;
        if (!MatchesMaterialTransferItem(before, intent) ||
            before!.Stack != expectedBefore)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_source_drifted_before_click");
            return;
        }

        var sourceMenu = active.SourceIsPlayer ? menu.inventory : menu.ItemsToGrabMenu;
        var position = InventorySlotScreenPosition(sourceMenu, intent.SourceSlotIndex);
        if (!position.HasValue)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_slot_screen_position_unavailable");
            return;
        }

        menu.receiveRightClick(position.Value.X, position.Value.Y, playSound: true);
        active.ClickCount++;
        var sourceAfter = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var destinationAfter = active.SourceIsPlayer
            ? active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)
            : Game1.player.Items;
        var sourceStackAfter = MaterialSourceStackAt(
            sourceAfter,
            intent.SourceSlotIndex,
            intent);
        var destinationQuantityAfter = MaterialQuantity(destinationAfter, intent);
        if (sourceStackAfter != active.SourceStackBefore - active.ClickCount ||
            destinationQuantityAfter != active.DestinationQuantityBefore + active.ClickCount ||
            destinationQuantityAfter > projection.DestinationQuantityAfter)
        {
            CompleteMaterialTransferBlocked(active, "material_transfer_native_click_postcondition_failed");
        }
    }

    private void CompleteMaterialTransfer(ActiveMaterialTransfer active)
    {
        StopAllMovement();
        TryApplySmapiRightButtonOverride(false, out _);
        var intent = active.Pending.Request.MaterialTransferIntent!;
        var source = active.SourceIsPlayer
            ? Game1.player.Items
            : active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID);
        var destination = active.SourceIsPlayer
            ? active.Chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)
            : Game1.player.Items;
        var sourceAfter = MaterialSourceStackAt(
            source,
            intent.SourceSlotIndex,
            intent);
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
        TryApplySmapiRightButtonOverride(false, out _);
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
        var sourceAfter = MaterialSourceStackAt(
            source,
            intent.SourceSlotIndex,
            intent);
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
                        "].slots[" +
                        request.MaterialTransferIntent.SourceSlotIndex +
                        "].stack",
                    Before = active.SourceStackBefore.ToString(),
                    After = sourceAfter.ToString()
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

    private static int MaterialSourceStackAt(
        IList<Item?> items,
        int index,
        MaterialTransferIntent intent)
    {
        var item = ItemAt(items, index);
        return MatchesMaterialTransferItem(item, intent) ? item!.Stack : 0;
    }

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
            int destinationQuantityBefore)
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
