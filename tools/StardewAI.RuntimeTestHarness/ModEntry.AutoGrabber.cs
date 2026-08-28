using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.SaveSerialization;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string AutoGrabberQualifiedItemId = "(BC)165";
    private const string AutoGrabberNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)165->CheckForActionOnAutoGrabber->ItemGrabMenu->receiveLeftClick->grabItemFromAutoGrabber->player.inventory";

    private void StartAutoGrabberCollection(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue ||
            !request.AutoGrabberContentStackCountBefore.HasValue ||
            !request.AutoGrabberTransferableStackCount.HasValue ||
            !request.AutoGrabberExpectedStackCountAfter.HasValue ||
            !request.AutoGrabberContentQuantityBefore.HasValue ||
            !request.AutoGrabberExpectedTransferQuantity.HasValue ||
            !request.AutoGrabberExpectedQuantityAfter.HasValue ||
            !request.AutoGrabberExpectedLocationActionReturn.HasValue)
        {
            pending.Completion.SetResult(AutoGrabberBlocked(request, "auto_grabber_typed_fields_required"));
            return;
        }
        if (!TryParseAutoGrabberRows(request.AutoGrabberContentsBeforeJson, out var before) ||
            !TryParseAutoGrabberRows(request.AutoGrabberTransferableContentsJson, out var transferable) ||
            !TryParseAutoGrabberRows(request.AutoGrabberRemainingContentsJson, out var remaining) ||
            transferable.Length == 0)
        {
            pending.Completion.SetResult(AutoGrabberBlocked(request, "auto_grabber_content_contract_invalid"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(AutoGrabberBlocked(request, "auto_grabber_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateAutoGrabberTarget(
            location, target, stand, request, before, transferable, remaining,
            out var autoGrabber, out var chest);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(AutoGrabberBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(AutoGrabberBlocked(request, "auto_grabber_path_unavailable:" + pathReason));
            return;
        }

        activeAutoGrabberCollection = new ActiveAutoGrabberCollection(
            pending, location, autoGrabber!, chest!, target, stand, path, maxMovementTiles,
            before, transferable, remaining);
    }

    private static string[] ValidateAutoGrabberTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        AutoGrabberContentRow[] before,
        AutoGrabberContentRow[] transferable,
        AutoGrabberContentRow[] remaining,
        out StardewObject? autoGrabber,
        out Chest? chest)
    {
        var reasons = new List<string>();
        autoGrabber = null;
        chest = null;
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) || !item.bigCraftable.Value ||
            !string.Equals(item.ItemId, "165", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, AutoGrabberQualifiedItemId, StringComparison.Ordinal) ||
            item.heldObject.Value is not Chest heldChest)
        {
            reasons.Add("auto_grabber_exact_object_or_held_chest_missing_or_drifted");
        }
        else
        {
            autoGrabber = item;
            chest = heldChest;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("auto_grabber_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
            reasons.Add("auto_grabber_destructive_object_trap_preamble_blocked");

        var safeSlotIndex = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (safeSlotIndex is < 0 or > 11 || safeSlotIndex >= Game1.player.Items.Count)
        {
            reasons.Add("auto_grabber_safe_toolbar_slot_drifted");
        }
        else
        {
            var safeItem = Game1.player.Items[safeSlotIndex];
            var safeKindMatches = request.AutoGrabberSafeSlotKind switch
            {
                "empty" => safeItem is null,
                "tool" => safeItem is Tool,
                _ => false
            };
            if (!safeKindMatches)
                reasons.Add("auto_grabber_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
            reasons.Add("auto_grabber_restore_slot_drifted");

        if (chest is not null)
        {
            var current = ReadAutoGrabberRows(chest);
            if (!AutoGrabberRowsEqual(current, before, includeSourceSlot: true) ||
                before.Length != request.AutoGrabberContentStackCountBefore ||
                transferable.Length != request.AutoGrabberTransferableStackCount ||
                remaining.Length != request.AutoGrabberExpectedStackCountAfter ||
                before.Sum(row => row.Quantity) != request.AutoGrabberContentQuantityBefore ||
                transferable.Sum(row => row.Quantity) != request.AutoGrabberExpectedTransferQuantity ||
                remaining.Sum(row => row.Quantity) != request.AutoGrabberExpectedQuantityAfter ||
                before.Length != transferable.Length + remaining.Length ||
                before.Sum(row => row.Quantity) != transferable.Sum(row => row.Quantity) + remaining.Sum(row => row.Quantity) ||
                !AutoGrabberRowsEqual(
                    before,
                    transferable.Concat(remaining).ToArray(),
                    includeSourceSlot: true))
            {
                reasons.Add("auto_grabber_content_projection_drifted");
            }
            foreach (var row in transferable)
            {
                var source = FindAutoGrabberSourceItem(chest, row);
                if (source is null || !Game1.player.couldInventoryAcceptThisItem(source))
                    reasons.Add("auto_grabber_transferable_stack_no_longer_fits");
            }
        }
        if (request.AutoGrabberExpectedLocationActionReturn != true ||
            !string.Equals(request.AutoGrabberHeldContainerRuntimeType, typeof(Chest).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.ItemId, "165", StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, AutoGrabberQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.InteractionKind, "location_object_menu_transaction", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "AutoGrabber", StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, AutoGrabberNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("auto_grabber_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickAutoGrabberCollection()
    {
        var active = activeAutoGrabberCollection;
        if (active is null)
            return;
        if (active.Stage != AutoGrabberStage.Move)
            active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteAutoGrabberCollection(active, false, "auto_grabber_timeout");
            return;
        }

        if (active.Stage == AutoGrabberStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "auto_grabber", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteAutoGrabberCollection(active, false, movementFailure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving)
                return;
            if (!AutoGrabberSourceStateMatches(active))
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_state_drifted_while_moving");
                return;
            }
            Game1.player.CurrentToolIndex = active.SafeSlotIndex;
            if (Game1.player.ActiveObject is not null)
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_active_object_selection_failed");
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            active.NativeHandled = active.Location.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            active.Stage = AutoGrabberStage.Transfer;
            return;
        }

        if (active.Stage == AutoGrabberStage.Transfer)
        {
            if (!TryGetOwnedAutoGrabberMenu(active, out var menu))
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_native_menu_missing_or_drifted");
                return;
            }
            if (active.CompletedStacks >= active.Transferable.Length)
            {
                if (!menu.readyToClose())
                {
                    CompleteAutoGrabberCollection(active, false, "auto_grabber_menu_not_ready_to_close");
                    return;
                }
                Game1.exitActiveMenu();
                active.Stage = AutoGrabberStage.Verify;
                return;
            }

            var expected = active.Transferable[active.CompletedStacks];
            var slot = FindAutoGrabberSourceSlot(active.Chest, expected);
            if (slot < 0 || slot >= menu.ItemsToGrabMenu.actualInventory.Count)
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_projected_source_stack_missing");
                return;
            }
            var item = active.Chest.Items[slot];
            if (item is null || !Game1.player.couldInventoryAcceptThisItem(item))
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_inventory_capacity_drifted");
                return;
            }
            var beforeInventoryQuantity = CountAutoGrabberInventoryQuantity(expected);
            var beforeChestStacks = active.Chest.Items.Count(value => value is not null);
            var position = InventorySlotScreenPosition(menu.ItemsToGrabMenu, slot);
            if (!position.HasValue)
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_slot_screen_position_unavailable");
                return;
            }
            menu.receiveLeftClick(position.Value.X, position.Value.Y, playSound: true);
            var afterInventoryQuantity = CountAutoGrabberInventoryQuantity(expected);
            var afterChestStacks = active.Chest.Items.Count(value => value is not null);
            if (afterInventoryQuantity != beforeInventoryQuantity + expected.Quantity ||
                afterChestStacks != beforeChestStacks - 1)
            {
                CompleteAutoGrabberCollection(active, false, "auto_grabber_native_click_postcondition_failed");
                return;
            }
            active.CompletedStacks++;
            active.TransferredQuantity += expected.Quantity;
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            CompleteAutoGrabberCollection(active, false, "auto_grabber_owned_menu_did_not_close");
            return;
        }
        var verified = active.NativeHandled == active.ExpectedLocationActionReturn &&
            active.CompletedStacks == active.Transferable.Length &&
            active.TransferredQuantity == active.ExpectedTransferQuantity &&
            AutoGrabberRowsEqual(ReadAutoGrabberRows(active.Chest), active.Remaining, includeSourceSlot: false) &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) &&
            ReferenceEquals(current, active.AutoGrabber) &&
            ReferenceEquals(current.heldObject.Value, active.Chest);
        CompleteAutoGrabberCollection(active, verified,
            verified ? Array.Empty<string>() : new[] { "auto_grabber_native_receipt_mismatch" });
    }

    private static bool AutoGrabberSourceStateMatches(ActiveAutoGrabberCollection active) =>
        active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) &&
        ReferenceEquals(current, active.AutoGrabber) &&
        ReferenceEquals(current.heldObject.Value, active.Chest) &&
        AutoGrabberRowsEqual(ReadAutoGrabberRows(active.Chest), active.Before, includeSourceSlot: true) &&
        !IsDestructiveObjectTrap(active.Location, active.Stand);

    private static bool TryGetOwnedAutoGrabberMenu(
        ActiveAutoGrabberCollection active,
        out ItemGrabMenu menu)
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu owned)
        {
            menu = null!;
            return false;
        }
        menu = owned;
        return
            ReferenceEquals(menu.context, active.AutoGrabber) &&
            ReferenceEquals(menu.ItemsToGrabMenu.actualInventory, active.Chest.Items);
    }

    private void CompleteAutoGrabberCollection(
        ActiveAutoGrabberCollection active,
        bool verified,
        params string[] reasons)
    {
        StopAllMovement();
        if (TryGetOwnedAutoGrabberMenu(active, out var menu) && menu.readyToClose())
            Game1.exitActiveMenu();
        activeAutoGrabberCollection = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var after = ReadAutoGrabberRows(active.Chest);
        var afterQuantity = after.Sum(row => row.Quantity);
        var verificationReasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "native_GameLocation_checkAction_opened_exact_auto_grabber_menu",
                "native_ItemGrabMenu_left_click_transferred_each_projected_stack",
                "nonfitting_auto_grabber_stacks_remained_unchanged",
                "auto_grabber_and_held_chest_identity_unchanged",
                "selected_toolbar_slot_restored"
            }
            : reasons.Length == 0 ? new[] { "auto_grabber_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "policy_training",
            PrimitiveKind = "collect_auto_grabber_contents",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = AutoGrabberRequestedEffect(active.Pending.Request),
            ObservedEffect = "auto_grabber_stacks=" + after.Length +
                ";auto_grabber_quantity=" + afterQuantity +
                ";transferred_stacks=" + active.CompletedStacks +
                ";transferred_quantity=" + active.TransferredQuantity +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.objects[auto_grabber].held_chest.stack_count",
                    Before = active.Before.Length.ToString(),
                    After = after.Length.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "current_location.objects[auto_grabber].held_chest.quantity",
                    Before = active.Before.Sum(row => row.Quantity).ToString(),
                    After = afterQuantity.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory.transferred_auto_grabber_quantity",
                    Before = "0",
                    After = active.TransferredQuantity.ToString()
                }
            }
        });
    }

    private static TrainingExecutionResult AutoGrabberBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            "collect_auto_grabber_contents",
            AutoGrabberRequestedEffect(request),
            "auto_grabber_current_state=not_started_or_rejected",
            reasons);

    private static string AutoGrabberRequestedEffect(TrainingExecutionRequest request) =>
        "auto_grabber.contents_stacks-=" + request.AutoGrabberTransferableStackCount +
        ";player.inventory_quantity+=" + request.AutoGrabberExpectedTransferQuantity +
        ";remaining_contents_unchanged=true;selected_slot_restored=true";

    private static bool TryParseAutoGrabberRows(string json, out AutoGrabberContentRow[] rows)
    {
        try
        {
            rows = JsonSerializer.Deserialize<AutoGrabberContentRow[]>(json) ?? Array.Empty<AutoGrabberContentRow>();
            return rows.All(row => row.SourceSlotIndex >= 0 && row.Quantity > 0 &&
                !string.IsNullOrWhiteSpace(row.RuntimeType) &&
                !string.IsNullOrWhiteSpace(row.QualifiedItemId) &&
                row.SourceUnitStateSha256.Length == 64 && row.InventoryUnitStateSha256.Length == 64);
        }
        catch (JsonException)
        {
            rows = Array.Empty<AutoGrabberContentRow>();
            return false;
        }
    }

    private static AutoGrabberContentRow[] ReadAutoGrabberRows(Chest chest) =>
        chest.Items
            .Select((item, slot) => item is null ? null : AutoGrabberContentRow.From(slot, item))
            .Where(row => row is not null)
            .Select(row => row!)
            .ToArray();

    private static bool AutoGrabberRowsEqual(
        AutoGrabberContentRow[] actual,
        AutoGrabberContentRow[] expected,
        bool includeSourceSlot)
    {
        if (actual.Length != expected.Length)
            return false;
        var remaining = actual.ToList();
        foreach (var row in expected)
        {
            var index = remaining.FindIndex(value => AutoGrabberRowsMatch(value, row, includeSourceSlot));
            if (index < 0)
                return false;
            remaining.RemoveAt(index);
        }
        return remaining.Count == 0;
    }

    private static bool AutoGrabberRowsMatch(
        AutoGrabberContentRow actual,
        AutoGrabberContentRow expected,
        bool includeSourceSlot) =>
        (!includeSourceSlot || actual.SourceSlotIndex == expected.SourceSlotIndex) &&
        actual.RuntimeType == expected.RuntimeType &&
        actual.QualifiedItemId == expected.QualifiedItemId &&
        actual.Quality == expected.Quality &&
        actual.SourceUnitStateSha256 == expected.SourceUnitStateSha256 &&
        actual.Quantity == expected.Quantity;

    private static Item? FindAutoGrabberSourceItem(Chest chest, AutoGrabberContentRow row)
    {
        var slot = FindAutoGrabberSourceSlot(chest, row);
        return slot >= 0 ? chest.Items[slot] : null;
    }

    private static int FindAutoGrabberSourceSlot(Chest chest, AutoGrabberContentRow row)
    {
        for (var slot = 0; slot < chest.Items.Count; slot++)
        {
            var item = chest.Items[slot];
            if (item is not null && AutoGrabberRowsMatch(AutoGrabberContentRow.From(slot, item), row, includeSourceSlot: false))
                return slot;
        }
        return -1;
    }

    private static int CountAutoGrabberInventoryQuantity(AutoGrabberContentRow expected) =>
        Game1.player.Items
            .Where(item => item is not null &&
                item.GetType().FullName == expected.RuntimeType &&
                item.QualifiedItemId == expected.QualifiedItemId &&
                item.Quality == expected.Quality &&
                AutoGrabberItemStateHash(item, inventoryReceipt: true) == expected.InventoryUnitStateSha256)
            .Sum(item => item!.Stack);

    private static string AutoGrabberItemStateHash(Item item, bool inventoryReceipt)
    {
        var unit = item.getOne();
        unit.Stack = 1;
        if (inventoryReceipt)
            unit.HasBeenInInventory = true;
        if (unit is StardewObject objectUnit)
            objectUnit.Flipped = false;
        using var stream = new MemoryStream();
        SaveSerializer.GetSerializer(unit.GetType()).Serialize(stream, unit);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private sealed record AutoGrabberContentRow(
        int SourceSlotIndex,
        string RuntimeType,
        string QualifiedItemId,
        int Quality,
        string SourceUnitStateSha256,
        string InventoryUnitStateSha256,
        int Quantity)
    {
        public static AutoGrabberContentRow From(int slot, Item item) =>
            new(
                slot,
                item.GetType().FullName ?? item.GetType().Name,
                item.QualifiedItemId,
                item.Quality,
                AutoGrabberItemStateHash(item, inventoryReceipt: false),
                AutoGrabberItemStateHash(item, inventoryReceipt: true),
                item.Stack);
    }

    private sealed class ActiveAutoGrabberCollection : INativeObjectInteractionMovement
    {
        public ActiveAutoGrabberCollection(
            PendingExecution pending,
            GameLocation location,
            StardewObject autoGrabber,
            Chest chest,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            AutoGrabberContentRow[] before,
            AutoGrabberContentRow[] transferable,
            AutoGrabberContentRow[] remaining)
        {
            Pending = pending;
            Location = location;
            AutoGrabber = autoGrabber;
            Chest = chest;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            Before = before;
            Transferable = transferable;
            Remaining = remaining;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            SafeSlotKind = pending.Request.AutoGrabberSafeSlotKind;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            ExpectedTransferQuantity = pending.Request.AutoGrabberExpectedTransferQuantity!.Value;
            ExpectedLocationActionReturn = pending.Request.AutoGrabberExpectedLocationActionReturn!.Value;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject AutoGrabber { get; }
        public Chest Chest { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public AutoGrabberContentRow[] Before { get; }
        public AutoGrabberContentRow[] Transferable { get; }
        public AutoGrabberContentRow[] Remaining { get; }
        public int SafeSlotIndex { get; }
        public string SafeSlotKind { get; }
        public int RestoreSlotIndex { get; }
        public int ExpectedTransferQuantity { get; }
        public bool ExpectedLocationActionReturn { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool NativeHandled { get; set; }
        public int CompletedStacks { get; set; }
        public int TransferredQuantity { get; set; }
        public AutoGrabberStage Stage { get; set; }
    }

    private enum AutoGrabberStage
    {
        Move,
        Transfer,
        Verify
    }
}
