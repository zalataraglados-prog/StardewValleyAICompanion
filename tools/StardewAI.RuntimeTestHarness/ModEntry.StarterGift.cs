using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string StarterGiftNativeContract = "FarmHouse_owned_by_current_player->Chest.checkForAction_giftbox_starter->dumpContents_addQuest_and_addItemByMenuIfNecessaryElseHoldUp->remove_empty_giftbox";

    private void StartStarterGiftClaim(PendingExecution pending)
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
            !request.Quantity.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_starter_gift", StarterGiftRequestedEffect(request), StarterGiftObservedEffect(Game1.currentLocation, request), "starter_gift_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_starter_gift", StarterGiftRequestedEffect(request), StarterGiftObservedEffect(Game1.currentLocation, request), "starter_gift_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateStarterGiftTarget(location, target, stand, request, out var chest, out var expectedOutputs);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_starter_gift", StarterGiftRequestedEffect(request), StarterGiftObservedEffect(location, request), reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 64, 1, 128);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_starter_gift", StarterGiftRequestedEffect(request), StarterGiftObservedEffect(location, request), "starter_gift_path_unavailable:" + pathReason));
            return;
        }
        if (!TryInventoryItemMultiset(out var inventoryBefore))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_starter_gift", StarterGiftRequestedEffect(request), StarterGiftObservedEffect(location, request), "starter_gift_inventory_state_unreadable"));
            return;
        }

        activeStarterGiftClaim = new ActiveStarterGiftClaim(
            pending,
            (FarmHouse)location,
            chest!,
            target,
            stand,
            path,
            maxMovementTiles,
            expectedOutputs,
            inventoryBefore,
            Game1.player.hasQuest(request.QuestId));
    }

    private static string[] ValidateStarterGiftTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out Chest? chest,
        out ClearanceOutputItemExpectation[] expectedOutputs)
    {
        var reasons = new List<string>();
        chest = null;
        expectedOutputs = Array.Empty<ClearanceOutputItemExpectation>();
        if (location is not FarmHouse farmHouse || !farmHouse.IsOwnedByCurrentPlayer ||
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal))
        {
            reasons.Add("starter_gift_not_owned_current_farmhouse");
            return reasons.ToArray();
        }
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item is not Chest candidate || candidate.GetType() != typeof(Chest) ||
            candidate.playerChest.Value || !candidate.giftbox.Value ||
            !candidate.giftboxIsStarterGift.Value || candidate.Items.Count == 0)
        {
            reasons.Add("starter_gift_exact_chest_missing_or_drifted");
            return reasons.ToArray();
        }
        chest = candidate;
        if (location.objects.Values.Count(value => value is Chest valueChest &&
                valueChest.GetType() == typeof(Chest) && !valueChest.playerChest.Value &&
                valueChest.giftbox.Value && valueChest.giftboxIsStarterGift.Value) != 1)
        {
            reasons.Add("starter_gift_chest_identity_ambiguous");
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("starter_gift_interaction_geometry_drifted");
        }
        var expectedQuestId = Game1.whichFarm == 7 ? "132" : "6";
        if (Game1.player.hasQuest(expectedQuestId))
        {
            reasons.Add("starter_gift_expected_quest_already_present");
        }
        if (!TryParseClearanceOutputItems(request.ExpectedOutputItemsJson, out expectedOutputs) ||
            expectedOutputs.Length == 0 || !StarterGiftContentsMatch(candidate, expectedOutputs))
        {
            reasons.Add("starter_gift_output_projection_drifted");
        }
        if (candidate.Items.Any(output => output is null || !StarterGiftInventoryAccepts(output)))
        {
            reasons.Add("starter_gift_inventory_cannot_accept_all_outputs");
        }
        if (!string.Equals(request.TargetRuntimeType, typeof(Chest).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, candidate.QualifiedItemId, StringComparison.Ordinal) ||
            request.Quantity != candidate.Items.Count ||
            !string.Equals(request.QuestId, expectedQuestId, StringComparison.Ordinal) ||
            !string.Equals(request.RewardBranch, "starter_gift_farm_type_" + Game1.whichFarm, StringComparison.Ordinal) ||
            !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "StarterGiftChest", StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, StarterGiftNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("starter_gift_projection_contract_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool StarterGiftInventoryAccepts(Item item)
    {
        Game1.player.GetItemReceiveBehavior(item, out var needsInventorySpace, out _);
        return !needsInventorySpace || Game1.player.couldInventoryAcceptThisItem(item);
    }

    private static bool StarterGiftContentsMatch(Chest chest, IReadOnlyCollection<ClearanceOutputItemExpectation> expected)
    {
        var actual = chest.Items
            .Where(item => item is not null)
            .GroupBy(item => ClearanceOutputItemKey.FromInventoryReceipt(item!))
            .ToDictionary(group => group.Key, group => group.Sum(item => item!.Stack));
        return actual.Count == expected.Count && expected.All(row =>
            actual.TryGetValue(row.Key, out var quantity) && quantity == row.Quantity);
    }

    private void TickStarterGiftClaim()
    {
        var active = activeStarterGiftClaim;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteStarterGiftClaim(active, false, "starter_gift_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteStarterGiftClaim(active, false, "starter_gift_timeout");
            return;
        }
        if (active.ActionIssued)
        {
            if (StarterGiftReceiptMatches(active))
            {
                if (Game1.player.CanMove && !Game1.player.UsingTool &&
                    Game1.activeClickableMenu is null && !Game1.dialogueUp)
                {
                    CompleteStarterGiftClaim(active, true);
                    return;
                }
                PulseStarterGiftDismiss(active);
                return;
            }
            if (active.ElapsedTicks - active.ActionIssuedAtTick > 900)
            {
                CompleteStarterGiftClaim(active, false, "starter_gift_native_receipt_mismatch");
                return;
            }
            PulseStarterGiftDismiss(active);
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
        }
        if (active.MovementTiles > active.MaxMovementTiles)
        {
            CompleteStarterGiftClaim(active, false, "starter_gift_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteStarterGiftClaim(active, false, "starter_gift_path_exhausted");
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
                CompleteStarterGiftClaim(active, false, "starter_gift_dynamic_path_blocked");
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
                CompleteStarterGiftClaim(active, false, "starter_gift_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        active.ActionIssuedAtTick = active.ElapsedTicks;
        if (!active.NativeHandled)
        {
            CompleteStarterGiftClaim(active, false, "starter_gift_native_action_not_handled");
        }
    }

    private static bool StarterGiftReceiptMatches(ActiveStarterGiftClaim active)
    {
        if (active.Location.objects.TryGetValue(active.Target.ToVector2(), out var value) &&
            ReferenceEquals(value, active.Chest))
        {
            return false;
        }
        if (!Game1.player.hasQuest(active.Pending.Request.QuestId) ||
            !TryInventoryItemMultiset(out var inventoryAfter))
        {
            return false;
        }
        return StarterGiftOutputDeltaMatches(
            active.InventoryBefore,
            inventoryAfter,
            active.ExpectedOutputs);
    }

    private static bool StarterGiftOutputDeltaMatches(
        IReadOnlyDictionary<ClearanceOutputItemKey, int> before,
        IReadOnlyDictionary<ClearanceOutputItemKey, int> after,
        IReadOnlyCollection<ClearanceOutputItemExpectation> expected)
    {
        var expectedQuantities = expected.ToDictionary(row => row.Key, row => row.Quantity);
        foreach (var key in before.Keys.Concat(after.Keys).Concat(expectedQuantities.Keys).Distinct())
        {
            var beforeQuantity = before.TryGetValue(key, out var beforeValue) ? beforeValue : 0;
            var afterQuantity = after.TryGetValue(key, out var afterValue) ? afterValue : 0;
            var expectedQuantity = expectedQuantities.TryGetValue(key, out var expectedValue) ? expectedValue : 0;
            if (afterQuantity - beforeQuantity != expectedQuantity)
            {
                return false;
            }
        }
        return true;
    }

    private void PulseStarterGiftDismiss(ActiveStarterGiftClaim active)
    {
        if (active.DismissHeld)
        {
            TryApplySmapiRightButtonOverride(false, out _);
            active.DismissHeld = false;
            active.LastDismissTick = active.ElapsedTicks;
        }
        else if ((Game1.activeClickableMenu is not null || Game1.dialogueUp || !Game1.player.CanMove) &&
            active.ElapsedTicks - active.LastDismissTick >= 20 &&
            TryApplySmapiRightButtonOverride(true, out _))
        {
            active.DismissHeld = true;
        }
    }

    private void CompleteStarterGiftClaim(ActiveStarterGiftClaim active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        TryApplySmapiRightButtonOverride(false, out _);
        activeStarterGiftClaim = null;
        var request = active.Pending.Request;
        var verificationReasons = verified
            ? new[] { "shared_bfs_reached_exact_adjacent_stand", "native_GameLocation_checkAction_invoked_exact_starter_gift", "native_dumpContents_added_exact_inventory_outputs_and_layout_quest", "empty_starter_gift_removed" }
            : reasons.Length == 0 ? new[] { "starter_gift_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "claim_starter_gift",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = StarterGiftRequestedEffect(request),
            ObservedEffect = StarterGiftObservedEffect(active.Location, request) + ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.starter_gift_claim.exact_starter_gift_present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "player.quests[" + request.QuestId + "]", Before = active.QuestPresentBefore.ToString().ToLowerInvariant(), After = Game1.player.hasQuest(request.QuestId).ToString().ToLowerInvariant() }
            }
        });
    }

    private static string StarterGiftRequestedEffect(TrainingExecutionRequest request) =>
        "current_location.starter_gift_claim.exact_starter_gift_present=false;quest_id=" + request.QuestId + ";expected_outputs=" + request.ExpectedOutputItemsJson;

    private static string StarterGiftObservedEffect(GameLocation? location, TrainingExecutionRequest request)
    {
        if (location is null)
        {
            return "location=unavailable";
        }
        var present = request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
            location.objects.TryGetValue(new Vector2(request.TargetTileX.Value, request.TargetTileY.Value), out var item) &&
            item is Chest chest && chest.giftbox.Value && chest.giftboxIsStarterGift.Value;
        return "location=" + location.NameOrUniqueName +
            ";starter_gift_present=" + present.ToString().ToLowerInvariant() +
            ";quest_id=" + request.QuestId +
            ";quest_present=" + Game1.player.hasQuest(request.QuestId).ToString().ToLowerInvariant();
    }

    private sealed class ActiveStarterGiftClaim
    {
        public ActiveStarterGiftClaim(
            PendingExecution pending,
            FarmHouse location,
            Chest chest,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            ClearanceOutputItemExpectation[] expectedOutputs,
            Dictionary<ClearanceOutputItemKey, int> inventoryBefore,
            bool questPresentBefore)
        {
            Pending = pending;
            Location = location;
            Chest = chest;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            ExpectedOutputs = expectedOutputs;
            InventoryBefore = inventoryBefore;
            QuestPresentBefore = questPresentBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public FarmHouse Location { get; }
        public Chest Chest { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public ClearanceOutputItemExpectation[] ExpectedOutputs { get; }
        public Dictionary<ClearanceOutputItemKey, int> InventoryBefore { get; }
        public bool QuestPresentBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int ActionIssuedAtTick { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool ActionIssued { get; set; }
        public bool NativeHandled { get; set; }
        public bool DismissHeld { get; set; }
        public int LastDismissTick { get; set; }
    }
}
