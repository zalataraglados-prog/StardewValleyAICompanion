using System.Globalization;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActivePanOreSpot
    {
        public ActivePanOreSpot(PendingExecution pending, GameLocation location, Pan pan, Point target, Point stand,
            List<Point> path, ClearanceOutputItemExpectation[] expectedItems,
            ExpectedAnimalStatIncrement[] receiptStatIncrements,
            Dictionary<ClearanceOutputItemKey, int> inventoryBefore, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Pan = pan;
            Target = target;
            Stand = stand;
            Path = path;
            ExpectedItems = expectedItems;
            ReceiptStatIncrements = receiptStatIncrements;
            InventoryBefore = inventoryBefore;
            MaxMovementTiles = maxMovementTiles;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Pan Pan { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public ClearanceOutputItemExpectation[] ExpectedItems { get; }
        public ExpectedAnimalStatIncrement[] ReceiptStatIncrements { get; }
        public Dictionary<ClearanceOutputItemKey, int> InventoryBefore { get; }
        public int MaxMovementTiles { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public Point LastObservedTile { get; set; }
        public bool BeginIssued { get; set; }
    }

    private TrainingExecutionResult ExecuteSetupPanOreSpot(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_pan_ore_spot", "current_location.ore_pan_point.active=true", "target=missing", "target_tile_required");
        }
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var upgradeLevel = Math.Clamp(request.PanUpgradeLevel ?? 1, 1, 4);
        var slot = EnsureFixtureTool(new Pan(upgradeLevel));
        if (slot < 0 || Game1.player.Items[slot] is not Pan pan)
        {
            return BlockedWithPrimitive(request, "debug_setup_pan_ore_spot", "current_location.ore_pan_point.active=true", "pan=unavailable", "fixture_inventory_cannot_accept_pan");
        }
        pan.UpgradeLevel = upgradeLevel;
        pan.enchantments.Clear();
        farm.orePanPoint.Value = target;
        var moved = MoveFixtureFarmerToFarmAdjacent(target, out var stand, out var moveReason);
        var verified = moved && farm.orePanPoint.Value == target && AreAdjacent(stand, target);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_pan_ore_spot",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_runtime_pan_point_active", "tool_slot=" + slot, "stand_tile=" + stand.X + "," + stand.Y } : new[] { moveReason },
            RequestedEffect = "current_location.ore_pan_point.active=true",
            ObservedEffect = PanningObservedEffect(farm),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_pan_point_not_ready:" + moveReason }
        };
    }

    private void StartPanOreSpot(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.ToolSlotIndex.HasValue || request.RequiredToolKind != "Pan" || request.TargetRuntimeType != typeof(Pan).FullName ||
            !request.PanUpgradeLevel.HasValue || string.IsNullOrWhiteSpace(request.PanEnchantmentsJson) ||
            !request.ClickPixelX.HasValue || !request.ClickPixelY.HasValue ||
            !request.ExpectedTimesPannedBefore.HasValue || !request.ExpectedTimesPannedAfter.HasValue ||
            !request.ExpectedMiningExperienceBefore.HasValue || !request.ExpectedMiningExperienceDelta.HasValue || !request.ExpectedMiningExperienceAfter.HasValue ||
            !request.ExpectedForagingExperienceBefore.HasValue || !request.ExpectedForagingExperienceDelta.HasValue || !request.ExpectedForagingExperienceAfter.HasValue ||
            string.IsNullOrWhiteSpace(request.PostUseOrePanPointStatus) || !request.PostUseRespawnAttempts.HasValue ||
            !TryParseClearanceOutputItems(request.ExpectedOutputItemsJson, out var expectedItems) || expectedItems.Length == 0 ||
            !TryParseAnimalStatIncrements(request.ExpectedStatIncrementsJson, out var receiptStatIncrements))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", "request=missing_typed_projection", "pan_ore_spot_typed_projection_required"));
            return;
        }
        if (activePanOreSpot is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", "player=busy_or_menu_open", "pan_ore_spot_player_busy"));
            return;
        }
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) || location.orePanPoint.Value != target ||
            !AreAdjacent(stand, target) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", PanningObservedEffect(location), "pan_ore_spot_target_or_stand_drifted"));
            return;
        }
        if (request.ToolSlotIndex.Value < 0 || request.ToolSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.ToolSlotIndex.Value] is not Pan pan || pan.GetType() != typeof(Pan) || pan.UpgradeLevel != request.PanUpgradeLevel.Value)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", PanningObservedEffect(location), "pan_ore_spot_tool_projection_drifted"));
            return;
        }
        var enchantmentsJson = JsonSerializer.Serialize(pan.enchantments.Select(value => value.GetType().FullName ?? value.GetType().Name).OrderBy(value => value, StringComparer.Ordinal), JsonOptions);
        if (enchantmentsJson != request.PanEnchantmentsJson || request.ClickPixelX != target.X * Game1.tileSize + Game1.tileSize / 2 || request.ClickPixelY != target.Y * Game1.tileSize + Game1.tileSize / 2 ||
            (long)Game1.player.stats.Get("TimesPanned") != request.ExpectedTimesPannedBefore ||
            Game1.player.experiencePoints[Farmer.miningSkill] != request.ExpectedMiningExperienceBefore ||
            Game1.player.experiencePoints[Farmer.foragingSkill] != request.ExpectedForagingExperienceBefore)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", PanningObservedEffect(location), "pan_ore_spot_side_effect_input_drifted"));
            return;
        }
        if (receiptStatIncrements.Any(stat => Game1.player.stats.Get(stat.StatName) != stat.Before || stat.After != stat.Before + stat.Amount))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", PanningObservedEffect(location), "pan_ore_spot_receipt_stat_projection_drifted"));
            return;
        }
        if (!TryPreviewPanOutputs(location, pan, out var currentExpected, out var inventoryAcceptsAll) || !inventoryAcceptsAll || !currentExpected.SequenceEqual(expectedItems))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "pan_ore_spot",
                "ore_pan_point_consumed=true",
                PanningObservedEffect(location) +
                    ";inventory_accepts_all=" + inventoryAcceptsAll.ToString().ToLowerInvariant() +
                    ";requested_outputs=" + PanningExpectationSignature(expectedItems) +
                    ";runtime_outputs=" + PanningExpectationSignature(currentExpected),
                "pan_ore_spot_reward_projection_drifted"));
            return;
        }
        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null || !TryInventoryItemMultiset(out var inventoryBefore))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pan_ore_spot", "ore_pan_point_consumed=true", PanningObservedEffect(location), "pan_ore_spot_path_or_inventory_unavailable:" + pathReason));
            return;
        }
        activePanOreSpot = new ActivePanOreSpot(pending, location, pan, target, stand, path, expectedItems, receiptStatIncrements, inventoryBefore, maxMovement);
    }

    private void TickPanOreSpot()
    {
        var active = activePanOreSpot;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location) || active.ElapsedTicks > 3600)
        {
            CompletePanOreSpotBlocked(active, "pan_ore_spot_world_location_or_timeout");
            return;
        }
        if (!active.BeginIssued && active.Location.orePanPoint.Value != active.Target)
        {
            CompletePanOreSpotBlocked(active, "pan_ore_spot_target_drifted");
            return;
        }
        if (!active.BeginIssued && Game1.player.TilePoint != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompletePanOreSpotBlocked(active, "pan_ore_spot_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            var playerTile = Game1.player.TilePoint;
            if (playerTile != active.LastObservedTile)
            {
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
                active.LastObservedTile = playerTile;
            }
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompletePanOreSpotBlocked(active, "pan_ore_spot_movement_budget_exceeded");
            }
            else if (playerTile == next)
            {
                active.PathIndex++;
            }
            return;
        }
        StopAllMovement();
        if (!active.BeginIssued)
        {
            SelectTool(active.Pan);
            Game1.player.lastClick = new Vector2(active.Pending.Request.ClickPixelX!.Value, active.Pending.Request.ClickPixelY!.Value);
            Game1.player.BeginUsingTool();
            active.BeginIssued = true;
            return;
        }
        if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
        {
            return;
        }
        CompletePanOreSpot(active);
    }

    private void CompletePanOreSpot(ActivePanOreSpot active)
    {
        activePanOreSpot = null;
        StopAllMovement();
        var inventoryReadable = TryInventoryItemMultiset(out var inventoryAfter);
        var request = active.Pending.Request;
        var timesAfter = (int)Game1.player.stats.Get("TimesPanned");
        var miningAfter = Game1.player.experiencePoints[Farmer.miningSkill];
        var foragingAfter = Game1.player.experiencePoints[Farmer.foragingSkill];
        var outputMatches = inventoryReadable && PanningOutputDeltaMatches(active.InventoryBefore, inventoryAfter, active.ExpectedItems);
        var pointStatusMatches = request.PostUseOrePanPointStatus == "runtime_rng_observed" || active.Location.orePanPoint.Value == Point.Zero;
        var noOverflowMenu = Game1.activeClickableMenu is null && Game1.nextClickableMenu.Count == 0;
        var receiptStatsMatch = active.ReceiptStatIncrements.All(stat => Game1.player.stats.Get(stat.StatName) == stat.After);
        var verified = outputMatches && pointStatusMatches && noOverflowMenu && receiptStatsMatch &&
            timesAfter == request.ExpectedTimesPannedAfter &&
            miningAfter == request.ExpectedMiningExperienceAfter && miningAfter - request.ExpectedMiningExperienceBefore == request.ExpectedMiningExperienceDelta &&
            foragingAfter == request.ExpectedForagingExperienceAfter && foragingAfter - request.ExpectedForagingExperienceBefore == request.ExpectedForagingExperienceDelta;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = active.Location.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "pan_ore_spot",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_pan_lifecycle_consumed_active_point", "exact_inventory_multiset_times_panned_mining_xp_and_foraging_xp_verified", "post_use_ore_point_rng_observed_without_preview_consumption" }
                : new[] { "pan_ore_spot_postcondition_mismatch" },
            RequestedEffect = "ore_pan_point_consumed=true;exact_output_multiset=true;times_panned_delta=1;mining_xp_delta=" + request.ExpectedMiningExperienceDelta + ";foraging_xp_delta=" + request.ExpectedForagingExperienceDelta,
            ObservedEffect = PanningObservedEffect(active.Location) + ";output_multiset_matches=" + outputMatches.ToString().ToLowerInvariant() +
                ";expected_output_delta=" + PanningExpectationSignature(active.ExpectedItems) +
                ";observed_inventory_delta=" + PanningInventoryDeltaSignature(active.InventoryBefore, inventoryAfter) +
                ";receipt_stats_match=" + receiptStatsMatch.ToString().ToLowerInvariant() +
                ";times_panned_after=" + timesAfter + ";mining_xp_after=" + miningAfter + ";foraging_xp_after=" + foragingAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "pan_ore_spot_postcondition_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.ore_pan_point", Before = active.Target.X + "," + active.Target.Y, After = active.Location.orePanPoint.Value.X + "," + active.Location.orePanPoint.Value.Y },
                new SimulatedFactChange { Path = "player.stats.TimesPanned", Before = request.ExpectedTimesPannedBefore.ToString()!, After = timesAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.mining.experience", Before = request.ExpectedMiningExperienceBefore.ToString()!, After = miningAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.foraging.experience", Before = request.ExpectedForagingExperienceBefore.ToString()!, After = foragingAfter.ToString(CultureInfo.InvariantCulture) }
            }
        });
    }

    private void CompletePanOreSpotBlocked(ActivePanOreSpot active, string reason)
    {
        activePanOreSpot = null;
        StopAllMovement();
        Game1.player.completelyStopAnimatingOrDoingAction();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "pan_ore_spot", "ore_pan_point_consumed=true", PanningObservedEffect(active.Location), reason));
    }

    private static bool TryPreviewPanOutputs(GameLocation location, Pan pan, out ClearanceOutputItemExpectation[] projected, out bool inventoryAcceptsAll)
    {
        projected = Array.Empty<ClearanceOutputItemExpectation>();
        inventoryAcceptsAll = false;
        try
        {
            var clone = new NetFarmerRoot(Game1.player).Clone().Value;
            if (clone is null)
            {
                return false;
            }
            clone.currentLocation = location;
            clone.Position = Game1.player.Position;
            clone.miningLevel.Value = 0;
            clone.foragingLevel.Value = 0;
            clone.experiencePoints[Farmer.miningSkill] = 15000;
            clone.experiencePoints[Farmer.foragingSkill] = 15000;
            var pointBefore = location.orePanPoint.Value;
            List<Item> outputs;
            var liveRandom = Game1.random;
            try
            {
                Game1.random = new Random(0);
                outputs = pan.getPanItems(location, clone);
            }
            finally
            {
                Game1.random = liveRandom;
            }
            foreach (var item in outputs)
            {
                item.HasBeenInInventory = true;
            }
            if (location.orePanPoint.Value != pointBefore)
            {
                return false;
            }
            projected = outputs
                .Select(item => new ClearanceOutputItemExpectation(
                    ClearanceOutputItemKey.From(item).RuntimeType,
                    item.QualifiedItemId,
                    item.Quality,
                    ClearanceOutputItemKey.From(item).UnitStateSha256,
                    item.Stack))
                .GroupBy(item => item.Key)
                .Select(group => new ClearanceOutputItemExpectation(group.Key.RuntimeType, group.Key.QualifiedItemId, group.Key.Quality, group.Key.UnitStateSha256, group.Sum(item => item.Quantity)))
                .OrderBy(item => item.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(item => item.UnitStateSha256, StringComparer.Ordinal)
                .ToArray();
            inventoryAcceptsAll = true;
            foreach (var source in outputs)
            {
                var item = source.getOne();
                item.Stack = source.Stack;
                if (Utility.addItemToThisInventoryList(item, clone.Items, clone.MaxItems) is not null)
                {
                    inventoryAcceptsAll = false;
                    break;
                }
            }
            return true;
        }
        catch
        {
            projected = Array.Empty<ClearanceOutputItemExpectation>();
            inventoryAcceptsAll = false;
            return false;
        }
    }

    private static string PanningObservedEffect(GameLocation location) =>
        "location=" + location.NameOrUniqueName + ";ore_pan_point=" + location.orePanPoint.Value.X + "," + location.orePanPoint.Value.Y +
        ";times_panned=" + Game1.player.stats.Get("TimesPanned") +
        ";mining_xp=" + Game1.player.experiencePoints[Farmer.miningSkill] +
        ";foraging_xp=" + Game1.player.experiencePoints[Farmer.foragingSkill];

    private static string PanningExpectationSignature(IEnumerable<ClearanceOutputItemExpectation> items) =>
        string.Join(",", items.Select(item => item.QualifiedItemId + "x" + item.Quantity + "@" + item.UnitStateSha256[..8]));

    private static string PanningInventoryDeltaSignature(
        IReadOnlyDictionary<ClearanceOutputItemKey, int> before,
        IReadOnlyDictionary<ClearanceOutputItemKey, int> after) =>
        string.Join(",", before.Keys.Concat(after.Keys).Distinct()
            .Select(key => new
            {
                Key = key,
                Delta = (after.TryGetValue(key, out var afterValue) ? afterValue : 0) -
                    (before.TryGetValue(key, out var beforeValue) ? beforeValue : 0)
            })
            .Where(row => row.Delta != 0 && !IsInventoryToolKey(row.Key))
            .OrderBy(row => row.Key.QualifiedItemId, StringComparer.Ordinal)
            .Select(row => row.Key.QualifiedItemId + "x" + row.Delta + "@" + row.Key.UnitStateSha256[..8]));

    private static bool PanningOutputDeltaMatches(
        IReadOnlyDictionary<ClearanceOutputItemKey, int> before,
        IReadOnlyDictionary<ClearanceOutputItemKey, int> after,
        IReadOnlyList<ClearanceOutputItemExpectation> expected)
    {
        var expectedQuantities = expected.ToDictionary(item => item.Key, item => item.Quantity);
        foreach (var key in before.Keys.Concat(after.Keys).Concat(expectedQuantities.Keys).Distinct())
        {
            if (IsInventoryToolKey(key))
            {
                continue;
            }
            var beforeQuantity = before.TryGetValue(key, out var beforeValue) ? beforeValue : 0;
            var afterQuantity = after.TryGetValue(key, out var afterValue) ? afterValue : 0;
            var expectedQuantity = expectedQuantities.TryGetValue(key, out var expectedValue) ? expectedValue : 0;
            if (afterQuantity - beforeQuantity != expectedQuantity)
            {
                return false;
            }
        }
        return expected.All(item => !IsInventoryToolKey(item.Key));
    }

    private static bool IsInventoryToolKey(ClearanceOutputItemKey key) =>
        key.QualifiedItemId.StartsWith("(T)", StringComparison.Ordinal) ||
        key.QualifiedItemId.StartsWith("(W)", StringComparison.Ordinal) ||
        key.QualifiedItemId.StartsWith("(SL)", StringComparison.Ordinal);
}
