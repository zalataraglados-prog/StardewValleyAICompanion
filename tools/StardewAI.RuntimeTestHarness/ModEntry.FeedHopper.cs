using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FeedHopperQualifiedItemId = "(BC)99";
    private const string FeedHopperHayQualifiedItemId = "(O)178";
    private const string FeedHopperNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)99->CheckForActionOnFeedHopper->root_location.piecesOfHay_minus_exact_withdrawal->player.inventory_(O)178_plus_exact_withdrawal";

    private void StartFeedHopperWithdrawal(PendingExecution pending)
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
            !request.FeedHopperSiloHayBefore.HasValue || !request.FeedHopperAnimalCount.HasValue ||
            !request.FeedHopperAnimalLimit.HasValue || !request.FeedHopperPlacedHayCount.HasValue ||
            !request.FeedHopperUnfedAnimalCount.HasValue ||
            !request.FeedHopperExpectedWithdrawalQuantity.HasValue ||
            !request.FeedHopperExpectedSiloHayAfter.HasValue ||
            !request.FeedHopperExpectedLocationActionReturn.HasValue)
        {
            pending.Completion.SetResult(FeedHopperBlocked(request, "feed_hopper_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FeedHopperBlocked(request, "feed_hopper_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateFeedHopperTarget(location, target, stand, request, out var house, out var hopper);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(FeedHopperBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FeedHopperBlocked(request, "feed_hopper_path_unavailable:" + pathReason));
            return;
        }

        nativeObjectInteractions.FeedHopper = new ActiveFeedHopperWithdrawal(
            pending, house!, hopper!, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateFeedHopperTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out AnimalHouse? house,
        out StardewObject? hopper)
    {
        var reasons = new List<string>();
        house = location as AnimalHouse;
        hopper = null;
        if (house is null ||
            !location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.ItemId, "99", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, FeedHopperQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(item.Name, "Feed Hopper", StringComparison.Ordinal))
        {
            reasons.Add("feed_hopper_exact_object_or_animal_house_missing_or_drifted");
        }
        else
        {
            hopper = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("feed_hopper_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
            reasons.Add("feed_hopper_destructive_object_trap_preamble_blocked");

        var safeSlotIndex = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (safeSlotIndex is < 0 or > 11 || safeSlotIndex >= Game1.player.Items.Count)
        {
            reasons.Add("feed_hopper_safe_toolbar_slot_drifted");
        }
        else
        {
            var safeItem = Game1.player.Items[safeSlotIndex];
            var safeKindMatches = request.FeedHopperSafeSlotKind switch
            {
                "empty" => safeItem is null,
                "tool" => safeItem is Tool,
                _ => false
            };
            if (!safeKindMatches)
                reasons.Add("feed_hopper_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
            reasons.Add("feed_hopper_restore_slot_drifted");

        if (house is not null && hopper is not null)
        {
            var root = location.GetRootLocation();
            var siloHay = Math.Max(0, root.piecesOfHay.Value);
            var animalCount = house.animalsThatLiveHere.Count;
            var animalLimit = Math.Max(0, house.animalLimit.Value);
            var placedHayCount = Math.Max(0, house.numberOfObjectsWithName("Hay"));
            var remainingCapacity = Math.Max(0, animalLimit - placedHayCount);
            var unfedAnimals = Math.Max(0, animalCount - placedHayCount);
            var expected = siloHay > 0
                ? Math.Max(0, Math.Min(Math.Max(1, Math.Min(animalCount, siloHay)), remainingCapacity))
                : 0;
            if (unfedAnimals <= 0 || expected <= 0 ||
                !Game1.player.couldInventoryAcceptThisItem(FeedHopperHayQualifiedItemId, expected, 0))
            {
                reasons.Add("feed_hopper_native_success_preconditions_drifted");
            }
            if (request.FeedHopperSiloHayBefore != siloHay ||
                request.FeedHopperAnimalCount != animalCount ||
                request.FeedHopperAnimalLimit != animalLimit ||
                request.FeedHopperPlacedHayCount != placedHayCount ||
                request.FeedHopperUnfedAnimalCount != unfedAnimals ||
                request.FeedHopperExpectedWithdrawalQuantity != expected ||
                request.FeedHopperExpectedSiloHayAfter != siloHay - expected ||
                request.FeedHopperExpectedLocationActionReturn != true ||
                !string.Equals(request.FeedHopperHayQualifiedItemId, FeedHopperHayQualifiedItemId, StringComparison.Ordinal) ||
                !string.Equals(request.FeedHopperRootLocationId, root.NameOrUniqueName, StringComparison.Ordinal) ||
                !string.Equals(request.ItemId, hopper.ItemId, StringComparison.Ordinal) ||
                !string.Equals(request.QualifiedItemId, hopper.QualifiedItemId, StringComparison.Ordinal) ||
                !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
                !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
                !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
                !string.Equals(request.ExpectedActionType, "FeedHopper", StringComparison.Ordinal) ||
                !string.Equals(request.NativeContract, FeedHopperNativeContract, StringComparison.Ordinal))
            {
                reasons.Add("feed_hopper_projection_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickFeedHopperWithdrawal()
    {
        var active = nativeObjectInteractions.FeedHopper;
        if (active is null)
            return;
        var movement = AdvanceNativeObjectInteractionMovement(active, "feed_hopper", out var movementFailure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            CompleteFeedHopperWithdrawal(active, false, movementFailure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;

        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var currentHopper) ||
            !ReferenceEquals(currentHopper, active.Hopper) ||
            !string.Equals(currentHopper.QualifiedItemId, FeedHopperQualifiedItemId, StringComparison.Ordinal) ||
            active.Location.GetRootLocation().piecesOfHay.Value != active.SiloHayBefore ||
            active.House.animalsThatLiveHere.Count != active.AnimalCount ||
            active.House.animalLimit.Value != active.AnimalLimit ||
            active.House.numberOfObjectsWithName("Hay") != active.PlacedHayCount)
        {
            CompleteFeedHopperWithdrawal(active, false, "feed_hopper_state_drifted_while_moving");
            return;
        }
        var safeItem = Game1.player.Items[active.SafeSlotIndex];
        if ((active.SafeSlotKind == "empty" && safeItem is not null) ||
            (active.SafeSlotKind == "tool" && safeItem is not Tool) ||
            IsDestructiveObjectTrap(active.Location, active.Stand))
        {
            CompleteFeedHopperWithdrawal(active, false, "feed_hopper_safe_context_drifted_while_moving");
            return;
        }

        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.ActiveObject is not null)
        {
            CompleteFeedHopperWithdrawal(active, false, "feed_hopper_active_object_selection_failed");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);

        var siloAfter = active.Location.GetRootLocation().piecesOfHay.Value;
        var inventoryAfter = CountInventoryItem(FeedHopperHayQualifiedItemId);
        var verified = active.NativeHandled == active.ExpectedLocationActionReturn &&
            siloAfter == active.ExpectedSiloHayAfter &&
            inventoryAfter == active.InventoryHayBefore + active.ExpectedWithdrawal &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var afterHopper) &&
            ReferenceEquals(afterHopper, active.Hopper) &&
            string.Equals(afterHopper.QualifiedItemId, FeedHopperQualifiedItemId, StringComparison.Ordinal) &&
            Game1.activeClickableMenu is null && !Game1.dialogueUp;
        CompleteFeedHopperWithdrawal(active, verified,
            verified ? Array.Empty<string>() : new[] { "feed_hopper_native_receipt_mismatch" });
    }

    private void CompleteFeedHopperWithdrawal(
        ActiveFeedHopperWithdrawal active,
        bool verified,
        params string[] reasons)
    {
        StopAllMovement();
        nativeObjectInteractions.FeedHopper = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var siloAfter = active.Location.GetRootLocation().piecesOfHay.Value;
        var inventoryAfter = CountInventoryItem(FeedHopperHayQualifiedItemId);
        var verificationReasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "safe_toolbar_slot_selected_without_active_object",
                "native_GameLocation_checkAction_withdrew_exact_feed_hopper_stack",
                "root_silo_hay_and_player_inventory_deltas_conserved",
                "canonical_feed_hopper_identity_unchanged",
                "selected_toolbar_slot_restored"
            }
            : reasons.Length == 0 ? new[] { "feed_hopper_post_state_mismatch" } : reasons;
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
            PrimitiveKind = "withdraw_feed_hopper_hay",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = FeedHopperRequestedEffect(active.Pending.Request),
            ObservedEffect = "silo_hay=" + siloAfter +
                ";inventory_hay=" + inventoryAfter +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";feed_hopper_present=" + active.Location.objects.ContainsKey(active.Target.ToVector2()).ToString().ToLowerInvariant() +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "root_location.pieces_of_hay",
                    Before = active.SiloHayBefore.ToString(),
                    After = siloAfter.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory[(O)178].count",
                    Before = active.InventoryHayBefore.ToString(),
                    After = inventoryAfter.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.current_tool_index",
                    Before = active.RestoreSlotIndex.ToString(),
                    After = Game1.player.CurrentToolIndex.ToString()
                }
            }
        });
    }

    private static TrainingExecutionResult FeedHopperBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "withdraw_feed_hopper_hay", FeedHopperRequestedEffect(request),
            "feed_hopper_current_state=" + FeedHopperCurrentObserved(request), reasons);

    private static string FeedHopperRequestedEffect(TrainingExecutionRequest request) =>
        "root_location.pieces_of_hay=" + request.FeedHopperExpectedSiloHayAfter +
        ";player.inventory[(O)178]+=" + request.FeedHopperExpectedWithdrawalQuantity +
        ";feed_hopper_identity_unchanged=true;selected_slot_restored=true";

    private static string FeedHopperCurrentObserved(TrainingExecutionRequest request)
    {
        if (Game1.currentLocation is not AnimalHouse house)
            return "not_animal_house";
        return "silo_hay=" + house.GetRootLocation().piecesOfHay.Value +
            ";animals=" + house.animalsThatLiveHere.Count +
            ";placed_hay=" + house.numberOfObjectsWithName("Hay") +
            ";inventory_hay=" + CountInventoryItem(FeedHopperHayQualifiedItemId);
    }

    private sealed class ActiveFeedHopperWithdrawal : INativeObjectInteractionMovement
    {
        public ActiveFeedHopperWithdrawal(
            PendingExecution pending,
            AnimalHouse location,
            StardewObject hopper,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles)
        {
            Pending = pending;
            House = location;
            Hopper = hopper;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            SafeSlotKind = pending.Request.FeedHopperSafeSlotKind;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            SiloHayBefore = pending.Request.FeedHopperSiloHayBefore!.Value;
            AnimalCount = pending.Request.FeedHopperAnimalCount!.Value;
            AnimalLimit = pending.Request.FeedHopperAnimalLimit!.Value;
            PlacedHayCount = pending.Request.FeedHopperPlacedHayCount!.Value;
            ExpectedWithdrawal = pending.Request.FeedHopperExpectedWithdrawalQuantity!.Value;
            ExpectedSiloHayAfter = pending.Request.FeedHopperExpectedSiloHayAfter!.Value;
            ExpectedLocationActionReturn = pending.Request.FeedHopperExpectedLocationActionReturn!.Value;
            InventoryHayBefore = CountInventoryItem(FeedHopperHayQualifiedItemId);
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public AnimalHouse House { get; }
        public GameLocation Location => House;
        public StardewObject Hopper { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public string SafeSlotKind { get; }
        public int RestoreSlotIndex { get; }
        public int SiloHayBefore { get; }
        public int AnimalCount { get; }
        public int AnimalLimit { get; }
        public int PlacedHayCount { get; }
        public int ExpectedWithdrawal { get; }
        public int ExpectedSiloHayAfter { get; }
        public bool ExpectedLocationActionReturn { get; }
        public int InventoryHayBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool NativeHandled { get; set; }
    }
}
