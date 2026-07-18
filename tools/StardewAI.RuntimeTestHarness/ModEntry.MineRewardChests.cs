using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartMineRewardChest(PendingExecution pending)
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
            string.IsNullOrWhiteSpace(request.QualifiedItemId) || !request.Quantity.HasValue ||
            !request.ExpectedSkillExperienceDelta.HasValue || !request.ExpectedOutputQuality.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_mine_reward_chest", "native_chest_dump_contents=true", "request=missing_typed_fields", "mine_reward_chest_typed_fields_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_mine_reward_chest", "native_chest_dump_contents=true", "player_or_location_not_ready", "mine_reward_chest_player_or_location_not_ready"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateMineRewardChestTarget(mine, target, stand, request, out var chest, out var item, out var outputExpectation);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_mine_reward_chest", MineRewardRequestedEffect(request), MineRewardObservedEffect(mine, target), reasons));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(mine, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_mine_reward_chest", MineRewardRequestedEffect(request), MineRewardObservedEffect(mine, target), "mine_reward_chest_path_unavailable:" + pathReason));
            return;
        }

        if (!TryInventoryItemMultiset(out var inventoryBefore))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_mine_reward_chest", MineRewardRequestedEffect(request), MineRewardObservedEffect(mine, target), "mine_reward_chest_inventory_state_unreadable"));
            return;
        }
        var outputCountBefore = outputExpectation is null || !inventoryBefore.TryGetValue(outputExpectation.Key, out var count) ? 0 : count;
        activeMineRewardChest = new ActiveMineRewardChest(
            pending, mine, chest!, item!, target, stand, path, maxMovementTiles,
            outputExpectation, outputCountBefore,
            Game1.player.experiencePoints[Farmer.luckSkill], Game1.player.maxStamina.Value,
            Game1.player.mailReceived.Contains("CF_Mines"));
    }

    private static string[] ValidateMineRewardChestTarget(
        MineShaft mine,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out Chest? chest,
        out Item? item,
        out ClearanceOutputItemExpectation? outputExpectation)
    {
        var reasons = new List<string>();
        chest = null;
        item = null;
        outputExpectation = null;
        if (!mine.overlayObjects.TryGetValue(target.ToVector2(), out var value) || value is not Chest candidate || candidate.GetType() != typeof(Chest))
        {
            return new[] { "mine_reward_chest_target_not_exact_vanilla_chest" };
        }
        chest = candidate;
        if (candidate.playerChest.Value || candidate.giftbox.Value || candidate.dropContents.Value || candidate.synchronized.Value ||
            candidate.SpecialChestType != Chest.SpecialChestTypes.None || candidate.Items.Count != 1 || candidate.Items[0] is null)
        {
            reasons.Add("mine_reward_chest_shape_drifted");
            return reasons.ToArray();
        }
        item = candidate.Items[0];
        if (item is SpecialItem special && special.which.Value == 4)
        {
            reasons.Add("mine_reward_chest_skull_key_uses_specialized_chain");
        }
        var supportedFamily = RuntimeMineKind(mine) == "ordinary_mines" && mine.mineLevel is 10 or 20 or 40 or 50 or 60 or 70 or 80 or 90 or 100 or 110 ||
            RuntimeMineKind(mine) == "skull_cavern";
        if (!supportedFamily)
        {
            reasons.Add("mine_reward_chest_family_drifted");
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(mine, stand) || !IsTileWalkable(mine, stand) || IsTileOccupiedByCharacter(mine, stand))
        {
            reasons.Add("mine_reward_chest_interaction_geometry_drifted");
        }
        if (!string.Equals(request.TargetRuntimeType, typeof(Chest).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.InteractionKind, "overlay_object", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "MineRewardChest", StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, item.QualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
            request.Quantity != item.Stack || request.ExpectedOutputQuality != item.Quality ||
            request.ExpectedSkillExperienceDelta != 0)
        {
            reasons.Add("mine_reward_chest_projection_drifted");
        }
        var isStardrop = item.QualifiedItemId == "(O)434";
        if (isStardrop)
        {
            if (request.ExpectedStardropMaxStaminaDelta != 34 || Game1.player.mailReceived.Contains("CF_Mines"))
            {
                reasons.Add("mine_reward_stardrop_progress_drifted");
            }
        }
        else
        {
            if (!Game1.player.couldInventoryAcceptThisItem(item) ||
                !TryParseClearanceOutputItems(request.ExpectedOutputItemsJson, out var outputs) || outputs.Length != 1)
            {
                reasons.Add("mine_reward_chest_inventory_projection_unavailable");
            }
            else
            {
                outputExpectation = outputs[0];
                var inventoryUnit = item.getOne();
                inventoryUnit.Stack = 1;
                inventoryUnit.HasBeenInInventory = true;
                if (outputExpectation.Key != ClearanceOutputItemKey.From(inventoryUnit) || outputExpectation.Quantity != item.Stack)
                {
                    reasons.Add("mine_reward_chest_unit_state_projection_drifted");
                }
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickMineRewardChest()
    {
        var active = activeMineRewardChest;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompleteMineRewardChest(active, false, "mine_reward_chest_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteMineRewardChest(active, false, "mine_reward_chest_timeout");
            return;
        }
        if (active.ActionIssued)
        {
            if (!MineRewardReceiptObserved(active))
            {
                return;
            }
            if (!active.CleanupIssued)
            {
                if (active.Chest.Items.Count != 0)
                {
                    CompleteMineRewardChest(active, false, "mine_reward_chest_cleanup_attempted_before_dump");
                    return;
                }
                active.CleanupHandled = active.Mine.checkAction(
                    new TileLocation(active.Target.X, active.Target.Y),
                    new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                active.CleanupIssued = true;
                if (!active.CleanupHandled)
                {
                    CompleteMineRewardChest(active, false, "mine_reward_chest_empty_cleanup_not_handled");
                }
                return;
            }
            if (!MineRewardPostconditionsMet(active))
            {
                return;
            }
            if (Game1.player.CanMove && !Game1.player.UsingTool && Game1.activeClickableMenu is null && !Game1.dialogueUp)
            {
                TryApplySmapiRightButtonOverride(false, out _);
                CompleteMineRewardChest(active, true);
                return;
            }
            PulseMineRewardDismiss(active);
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
            CompleteMineRewardChest(active, false, "mine_reward_chest_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteMineRewardChest(active, false, "mine_reward_chest_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (playerTile == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.Mine, next) || IsTileOccupiedByCharacter(active.Mine, next))
            {
                CompleteMineRewardChest(active, false, "mine_reward_chest_dynamic_path_blocked");
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
                CompleteMineRewardChest(active, false, "mine_reward_chest_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        active.OpenHandled = active.Mine.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        if (!active.OpenHandled)
        {
            CompleteMineRewardChest(active, false, "mine_reward_chest_native_open_not_handled");
        }
    }

    private static bool MineRewardReceiptObserved(ActiveMineRewardChest active)
    {
        var luckMatches = Game1.player.experiencePoints[Farmer.luckSkill] - active.LuckExperienceBefore == active.Pending.Request.ExpectedSkillExperienceDelta;
        var consumed = Game1.player.chestConsumedMineLevels.ContainsKey(active.Mine.mineLevel);
        if (active.Chest.Items.Count != 0 || !luckMatches || !consumed)
        {
            return false;
        }
        if (active.Item.QualifiedItemId == "(O)434")
        {
            return !active.StardropMailBefore && Game1.player.mailReceived.Contains("CF_Mines");
        }
        return TryInventoryItemMultiset(out var inventory) && active.OutputExpectation is not null &&
            inventory.TryGetValue(active.OutputExpectation.Key, out var count) && count - active.OutputCountBefore == active.OutputExpectation.Quantity;
    }

    private static bool MineRewardPostconditionsMet(ActiveMineRewardChest active)
    {
        var chestRemoved = !active.Mine.overlayObjects.TryGetValue(active.Target.ToVector2(), out var value) || !ReferenceEquals(value, active.Chest);
        if (!chestRemoved || !MineRewardReceiptObserved(active))
        {
            return false;
        }
        return active.Item.QualifiedItemId != "(O)434" ||
            Game1.player.maxStamina.Value - active.MaxStaminaBefore == active.Pending.Request.ExpectedStardropMaxStaminaDelta;
    }

    private void PulseMineRewardDismiss(ActiveMineRewardChest active)
    {
        if (active.DismissHeld)
        {
            TryApplySmapiRightButtonOverride(false, out _);
            active.DismissHeld = false;
            active.LastDismissTick = active.ElapsedTicks;
        }
        else if (active.ElapsedTicks - active.LastDismissTick >= 20 && TryApplySmapiRightButtonOverride(true, out _))
        {
            active.DismissHeld = true;
            active.DismissAttempts++;
        }
    }

    private void CompleteMineRewardChest(ActiveMineRewardChest active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        TryApplySmapiRightButtonOverride(false, out _);
        activeMineRewardChest = null;
        var request = active.Pending.Request;
        var verificationReasons = verified
            ? new[] { "native_reward_open_started_exact_chest", "native_dumpContents_emptied_chest", "empty_chest_cleanup_removed_chest", "exact_reward_verified_and_ignored_luck_experience_call_left_state_unchanged" }
            : reasons.Length == 0 ? new[] { "mine_reward_chest_post_state_mismatch" } : reasons;
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
            PrimitiveKind = "claim_mine_reward_chest",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = MineRewardRequestedEffect(request),
            ObservedEffect = MineRewardObservedEffect(active.Mine, active.Target) + ";open_handled=" + active.OpenHandled.ToString().ToLowerInvariant() + ";cleanup_handled=" + active.CleanupHandled.ToString().ToLowerInvariant() + ";dismiss_attempts=" + active.DismissAttempts,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.reward_chests[" + active.Target.X + "," + active.Target.Y + "]", Before = active.Item.QualifiedItemId, After = "removed" },
                new SimulatedFactChange { Path = "player.skills.luck.experience", Before = active.LuckExperienceBefore.ToString(), After = Game1.player.experiencePoints[Farmer.luckSkill].ToString() },
                new SimulatedFactChange { Path = "player.max_stamina", Before = active.MaxStaminaBefore.ToString(), After = Game1.player.maxStamina.Value.ToString() }
            }
        });
    }

    private static string MineRewardRequestedEffect(TrainingExecutionRequest request) =>
        "mining.reward_chests[" + request.TargetTileX + "," + request.TargetTileY + "].removed=true;reward=" + request.QualifiedItemId + ";luck_xp_delta=" + request.ExpectedSkillExperienceDelta;

    private static string MineRewardObservedEffect(MineShaft mine, Point target) =>
        "location=" + mine.NameOrUniqueName + ";mine_level=" + mine.mineLevel + ";target=" + target.X + "," + target.Y +
        ";chest_present=" + mine.overlayObjects.ContainsKey(target.ToVector2()).ToString().ToLowerInvariant() +
        ";luck_xp=" + Game1.player.experiencePoints[Farmer.luckSkill] + ";max_stamina=" + Game1.player.maxStamina.Value;

    private sealed class ActiveMineRewardChest
    {
        public ActiveMineRewardChest(PendingExecution pending, MineShaft mine, Chest chest, Item item, Point target, Point stand, List<Point> path,
            int maxMovementTiles, ClearanceOutputItemExpectation? outputExpectation, int outputCountBefore, int luckExperienceBefore, int maxStaminaBefore, bool stardropMailBefore)
        {
            Pending = pending;
            Mine = mine;
            Chest = chest;
            Item = item;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            OutputExpectation = outputExpectation;
            OutputCountBefore = outputCountBefore;
            LuckExperienceBefore = luckExperienceBefore;
            MaxStaminaBefore = maxStaminaBefore;
            StardropMailBefore = stardropMailBefore;
            LastObservedTile = Game1.player.TilePoint;
            LastPosition = Game1.player.Position;
            MaxTicks = Math.Max(600, path.Count * 90 + (item.QualifiedItemId == "(O)434" ? 900 : 360));
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Chest Chest { get; }
        public Item Item { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public ClearanceOutputItemExpectation? OutputExpectation { get; }
        public int OutputCountBefore { get; }
        public int LuckExperienceBefore { get; }
        public int MaxStaminaBefore { get; }
        public bool StardropMailBefore { get; }
        public int MaxTicks { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public Point LastObservedTile { get; set; }
        public Vector2 LastPosition { get; set; }
        public int StuckTicks { get; set; }
        public bool ActionIssued { get; set; }
        public bool OpenHandled { get; set; }
        public bool CleanupIssued { get; set; }
        public bool CleanupHandled { get; set; }
        public bool DismissHeld { get; set; }
        public int DismissAttempts { get; set; }
        public int LastDismissTick { get; set; }
    }
}
