using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeGrangeNativeContract =
        "Event.checkAction(festival_fall16_buildings_349_350_351)->FarmerTeam.grangeMutex->StorageContainer(9x3,Event.onGrangeChange,Utility.highlightSmallObjects)->one_native_remove_or_place_click_pair->okButton->mutex_release";

    private sealed class ActiveGrangeDisplay : INativeObjectInteractionMovement
    {
        public ActiveGrangeDisplay(PendingExecution pending, GameLocation location, StardewValley.Event festival,
            Point interaction, Point stand, List<Point> path, Item item, int sinkStackBefore, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Festival = festival;
            Interaction = interaction;
            Stand = stand;
            Path = path;
            Item = item;
            SinkStackBefore = sinkStackBefore;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewValley.Event Festival { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public Item Item { get; }
        public int SinkStackBefore { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 2400;
        public string StartedAt { get; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int MenuWaitTicks { get; set; }
        public bool OpenIssued { get; set; }
        public bool FirstClickIssued { get; set; }
        public bool SecondClickIssued { get; set; }
        public bool CloseIssued { get; set; }
    }

    private void StartGrangeDisplay(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (!GrangeRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                "grange=requested", GrangeObservedEffect(), "grange_display_typed_request_required"));
            return;
        }
        if (activeGrangeDisplay is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                "grange=requested", GrangeObservedEffect(), "grange_display_player_busy"));
            return;
        }
        var location = Game1.currentLocation;
        var festival = location?.currentEvent;
        if (location is null || festival is null || !festival.isFestival || festival.id != "festival_fall16" ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            festival.grangeJudged != request.GrangeJudged)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                "festival=fall16", GrangeObservedEffect(), "grange_display_festival_context_mismatch"));
            return;
        }
        var interaction = new Point(request.GrangeInteractionTileX!.Value, request.GrangeInteractionTileY!.Value);
        var stand = new Point(request.GrangeStandTileX!.Value, request.GrangeStandTileY!.Value);
        var tileIndex = location.getTileIndexAt(interaction.X, interaction.Y, "Buildings", "untitled tile sheet");
        if (tileIndex is not (349 or 350 or 351) || !AreAdjacent(stand, interaction) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand) ||
            Game1.player.team.grangeMutex.IsLocked() || Game1.player.team.grangeDisplay.Count != 9)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                "interaction=grange_stand", GrangeObservedEffect(), "grange_display_endpoint_or_mutex_drifted"));
            return;
        }
        var displaySlot = request.GrangeDisplaySlotIndex!.Value;
        Item? item;
        var sinkStackBefore = 0;
        if (request.GrangeOperation == "place")
        {
            var inventorySlot = request.GrangeInventorySlotIndex!.Value;
            item = inventorySlot >= 0 && inventorySlot < Game1.player.Items.Count ? Game1.player.Items[inventorySlot] : null;
            if (Game1.player.team.grangeDisplay[displaySlot] is not null || item is not StardewValley.Object obj ||
                obj.bigCraftable.Value || StardewValley.Event.IsItemMayorShorts(item) ||
                item.QualifiedItemId != request.QualifiedItemId || item.ItemId != request.ItemId ||
                item.GetType().FullName != request.GrangeItemRuntimeType || item.Quality != request.GrangeItemQuality ||
                item.Stack != request.GrangeInventoryStackBefore || request.GrangeInventoryStackAfter != item.Stack - 1 ||
                item.sellToStorePrice(-1L) != request.GrangeActualSellPrice ||
                RuntimeGrangeItemPoints(obj) != request.GrangeItemPoints)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                    "operation=place", GrangeObservedEffect(), "grange_display_place_source_drifted"));
                return;
            }
        }
        else
        {
            item = Game1.player.team.grangeDisplay[displaySlot];
            var sink = request.GrangeSinkInventorySlotIndex!.Value;
            var sinkItem = sink >= 0 && sink < Game1.player.Items.Count ? Game1.player.Items[sink] : null;
            sinkStackBefore = sinkItem?.Stack ?? 0;
            if (item is null || item.QualifiedItemId != request.QualifiedItemId || item.ItemId != request.ItemId ||
                item.GetType().FullName != request.GrangeItemRuntimeType || item.Quality != request.GrangeItemQuality ||
                sink < 0 || sink >= Game1.player.Items.Count ||
                sinkItem is not null && (!sinkItem.canStackWith(item) || sinkItem.Stack >= sinkItem.maximumStackSize()))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                    "operation=remove", GrangeObservedEffect(), "grange_display_remove_sink_drifted"));
                return;
            }
        }
        if (RuntimeScoreGrange() != request.GrangeScoreBefore ||
            Game1.player.team.grangeDisplay.Count(row => row is not null) != request.GrangeOccupiedSlotsBefore)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                "grange=before_projection", GrangeObservedEffect(), "grange_display_score_or_count_drifted"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand,
            maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_grange_display",
                "route=stand", GrangeObservedEffect(), "grange_display_path_unavailable:" + pathReason));
            return;
        }
        activeGrangeDisplay = new ActiveGrangeDisplay(
            pending, location, festival, interaction, stand, path, item, sinkStackBefore, maxMovementTiles);
    }

    private static bool GrangeRequestIsTyped(TrainingExecutionRequest request)
    {
        return request.GrangeInteractionTileX.HasValue && request.GrangeInteractionTileY.HasValue &&
            request.GrangeStandTileX.HasValue && request.GrangeStandTileY.HasValue &&
            request.GrangeJudged.HasValue && request.GrangeDisplaySlotIndex is >= 0 and <= 8 &&
            request.GrangeScoreBefore.HasValue && request.GrangeScoreAfter.HasValue &&
            request.GrangeOccupiedSlotsBefore.HasValue && request.GrangeOccupiedSlotsAfter.HasValue &&
            request.GrangeFirstPlaceScore == 90 && !string.IsNullOrWhiteSpace(request.GrangeProjectionFingerprint) &&
            !string.IsNullOrWhiteSpace(request.QualifiedItemId) && !string.IsNullOrWhiteSpace(request.ItemId) &&
            !string.IsNullOrWhiteSpace(request.GrangeItemRuntimeType) && request.NativeContract == RuntimeGrangeNativeContract &&
            (request.GrangeOperation == "place" && request.GrangeInventorySlotIndex is >= 0 &&
                request.GrangeInventoryStackBefore is >= 1 && request.GrangeInventoryStackAfter == request.GrangeInventoryStackBefore - 1 &&
                request.GrangeOccupiedSlotsAfter == request.GrangeOccupiedSlotsBefore + 1 && request.GrangeJudged == false ||
             request.GrangeOperation == "remove" && request.GrangeSinkInventorySlotIndex is >= 0 &&
                request.GrangeOccupiedSlotsAfter == request.GrangeOccupiedSlotsBefore - 1 &&
                (request.GrangeObjective != "retrieve_after_judging" || request.GrangeJudged == true));
    }

    private void TickGrangeDisplay()
    {
        var active = activeGrangeDisplay;
        if (active is null)
            return;
        if (!ReferenceEquals(Game1.currentLocation?.currentEvent, active.Festival))
        {
            BlockGrangeDisplay(active, "grange_display_festival_event_changed");
            return;
        }
        var movement = AdvanceNativeObjectInteractionMovement(active, "grange_display", out var movementFailure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            BlockGrangeDisplay(active, movementFailure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;
        if (!active.OpenIssued)
        {
            Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
            if (!active.Festival.checkAction(new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y), Game1.viewport, Game1.player))
            {
                BlockGrangeDisplay(active, "grange_display_native_check_action_rejected");
                return;
            }
            active.OpenIssued = true;
            return;
        }
        if (active.CloseIssued)
        {
            if (Game1.activeClickableMenu is null && !Game1.player.team.grangeMutex.IsLockHeld())
            {
                CompleteGrangeDisplay(active);
                return;
            }
            if (++active.MenuWaitTicks > 240)
                BlockGrangeDisplay(active, "grange_display_menu_close_or_mutex_release_timeout");
            return;
        }
        if (Game1.activeClickableMenu is not StorageContainer menu)
        {
            if (Game1.activeClickableMenu is not null || ++active.MenuWaitTicks > 180)
                BlockGrangeDisplay(active, "grange_display_native_storage_menu_open_failed");
            return;
        }
        if (!Game1.player.team.grangeMutex.IsLockHeld())
        {
            BlockGrangeDisplay(active, "grange_display_mutex_not_held_with_menu");
            return;
        }
        var request = active.Pending.Request;
        if (!active.FirstClickIssued)
        {
            Point? position = request.GrangeOperation == "place"
                ? InventorySlotScreenPosition(menu.inventory, request.GrangeInventorySlotIndex!.Value)
                : InventorySlotScreenPosition(menu.ItemsToGrabMenu, request.GrangeDisplaySlotIndex!.Value);
            if (!position.HasValue)
            {
                BlockGrangeDisplay(active, "grange_display_first_click_component_missing");
                return;
            }
            if (request.GrangeOperation == "place")
                menu.receiveRightClick(position.Value.X, position.Value.Y);
            else
                menu.receiveLeftClick(position.Value.X, position.Value.Y);
            active.FirstClickIssued = true;
            if (menu.heldItem?.QualifiedItemId != request.QualifiedItemId || menu.heldItem.Stack != 1)
                BlockGrangeDisplay(active, "grange_display_native_pickup_failed");
            return;
        }
        if (!active.SecondClickIssued)
        {
            Point? position = request.GrangeOperation == "place"
                ? InventorySlotScreenPosition(menu.ItemsToGrabMenu, request.GrangeDisplaySlotIndex!.Value)
                : InventorySlotScreenPosition(menu.inventory, request.GrangeSinkInventorySlotIndex!.Value);
            if (!position.HasValue)
            {
                BlockGrangeDisplay(active, "grange_display_second_click_component_missing");
                return;
            }
            menu.receiveLeftClick(position.Value.X, position.Value.Y);
            active.SecondClickIssued = true;
            var slotItem = Game1.player.team.grangeDisplay[request.GrangeDisplaySlotIndex!.Value];
            var displayMatches = request.GrangeOperation == "place"
                ? slotItem?.QualifiedItemId == request.QualifiedItemId && slotItem.Stack == 1
                : slotItem is null;
            if (menu.heldItem is not null || !displayMatches)
                BlockGrangeDisplay(active, "grange_display_native_second_click_failed");
            return;
        }
        if (!menu.readyToClose() || menu.okButton is null)
        {
            if (++active.MenuWaitTicks > 180)
                BlockGrangeDisplay(active, "grange_display_menu_not_ready_to_close");
            return;
        }
        menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
        active.CloseIssued = true;
        active.MenuWaitTicks = 0;
    }

    private void CompleteGrangeDisplay(ActiveGrangeDisplay active)
    {
        var request = active.Pending.Request;
        var displayItem = Game1.player.team.grangeDisplay[request.GrangeDisplaySlotIndex!.Value];
        var inventoryVerified = request.GrangeOperation == "place"
            ? GrangeInventoryStack(request.GrangeInventorySlotIndex!.Value, request.QualifiedItemId) == request.GrangeInventoryStackAfter
            : GrangeInventoryStack(request.GrangeSinkInventorySlotIndex!.Value, request.QualifiedItemId) == active.SinkStackBefore + 1;
        var verified = inventoryVerified && RuntimeScoreGrange() == request.GrangeScoreAfter &&
            Game1.player.team.grangeDisplay.Count(item => item is not null) == request.GrangeOccupiedSlotsAfter &&
            active.Festival.grangeJudged == request.GrangeJudged &&
            (request.GrangeOperation == "place" ? displayItem?.QualifiedItemId == request.QualifiedItemId : displayItem is null) &&
            Game1.activeClickableMenu is null && !Game1.player.team.grangeMutex.IsLockHeld();
        activeGrangeDisplay = null;
        StopAllMovement();
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "manage_grange_display",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_festival_checkAction_opened_storage", "shared_grange_mutex_acquired_and_released", "exactly_one_native_display_mutation_verified", "inventory_conservation_verified", "score_and_judging_state_verified" }
                : new[] { "grange_display_post_state_mismatch" },
            RequestedEffect = "operation=" + request.GrangeOperation + ";slot=" + request.GrangeDisplaySlotIndex + ";score=" + request.GrangeScoreAfter,
            ObservedEffect = GrangeObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "grange_display_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.team.grangeDisplay[" + request.GrangeDisplaySlotIndex + "]", Before = request.GrangeOperation == "place" ? "null" : request.QualifiedItemId, After = request.GrangeOperation == "place" ? request.QualifiedItemId : "null" },
                    new SimulatedFactChange { Path = "player.grange_display.current_projected_score", Before = request.GrangeScoreBefore.ToString()!, After = request.GrangeScoreAfter.ToString()! }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockGrangeDisplay(ActiveGrangeDisplay active, string reason)
    {
        activeGrangeDisplay = null;
        StopAllMovement();
        if (Game1.activeClickableMenu is StorageContainer)
            Game1.exitActiveMenu();
        if (Game1.player.team.grangeMutex.IsLockHeld())
            Game1.player.team.grangeMutex.ReleaseLock();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "manage_grange_display",
            "operation=" + active.Pending.Request.GrangeOperation, GrangeObservedEffect(), reason));
    }

    private static int GrangeInventoryStack(int slot, string qualifiedItemId)
    {
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        return item?.QualifiedItemId == qualifiedItemId ? item.Stack : 0;
    }

    private static int RuntimeScoreGrange()
    {
        var score = 14;
        var empty = 0;
        var groups = new HashSet<int>();
        var shorts = false;
        for (var slot = 0; slot < 9; slot++)
        {
            var item = slot < Game1.player.team.grangeDisplay.Count ? Game1.player.team.grangeDisplay[slot] : null;
            if (item is StardewValley.Object obj)
            {
                shorts |= StardewValley.Event.IsItemMayorShorts(obj);
                score += RuntimeGrangeItemPoints(obj);
                var group = RuntimeGrangeGroup(obj.Category);
                if (group != 0) groups.Add(group);
            }
            else if (item is null) empty++;
        }
        score += Math.Min(30, groups.Count * 5) + 9 - 2 * empty;
        return shorts ? -666 : score;
    }

    private static int RuntimeGrangeItemPoints(StardewValley.Object item)
    {
        var points = item.Quality + 1;
        var price = item.sellToStorePrice(-1L);
        if (price >= 20) points++;
        if (price >= 90) points++;
        if (price >= 200) points++;
        if (price >= 300 && item.Quality < 2) points++;
        if (price >= 400 && item.Quality < 1) points++;
        return points;
    }

    private static int RuntimeGrangeGroup(int category) => category switch
    {
        -75 => -75, -79 => -79, -18 or -14 or -6 or -5 => -5, -12 or -2 => -12,
        -4 => -4, -81 or -80 or -27 => -81, -7 => -7, -26 => -26, _ => 0
    };

    private static string GrangeObservedEffect() =>
        "festival=" + (Game1.currentLocation?.currentEvent?.id ?? "none") +
        ";judged=" + (Game1.currentLocation?.currentEvent?.grangeJudged.ToString().ToLowerInvariant() ?? "unavailable") +
        ";score=" + RuntimeScoreGrange() +
        ";occupied=" + Game1.player.team.grangeDisplay.Count(item => item is not null) +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";mutex_held=" + Game1.player.team.grangeMutex.IsLockHeld().ToString().ToLowerInvariant();
}
