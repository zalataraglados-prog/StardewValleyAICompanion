using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using System.Text.Json;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string PotOfGoldQualifiedItemId = "(O)PotOfGold";
    private const string PotOfGoldCoinQualifiedItemId = "(O)GoldCoin";
    private const string PotOfGoldHatQualifiedItemId = "(H)LeprechuanHat";
    private const string PotOfGoldNativeContract = "Forest.DayUpdate_spring_17_tile_52_98->Object.checkForAction_PotOfGold->removeObject_and_createMultipleItemDebris";

    private void StartPotOfGoldClaim(PendingExecution pending)
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_pot_of_gold", PotOfGoldRequestedEffect(request), PotOfGoldObservedEffect(Game1.currentLocation), "pot_of_gold_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_pot_of_gold", PotOfGoldRequestedEffect(request), PotOfGoldObservedEffect(Game1.currentLocation), "pot_of_gold_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidatePotOfGoldTarget(location, target, stand, request, out var pot);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_pot_of_gold", PotOfGoldRequestedEffect(request), PotOfGoldObservedEffect(location), reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "claim_pot_of_gold", PotOfGoldRequestedEffect(request), PotOfGoldObservedEffect(location), "pot_of_gold_path_unavailable:" + pathReason));
            return;
        }

        activePotOfGoldClaim = new ActivePotOfGoldClaim(
            pending,
            location,
            pot!,
            target,
            stand,
            path,
            request.Quantity.Value,
            maxMovementTiles,
            CountPotOfGoldReward(location, PotOfGoldCoinQualifiedItemId),
            CountPotOfGoldReward(location, PotOfGoldHatQualifiedItemId));
    }

    private static string[] ValidatePotOfGoldTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? pot)
    {
        var reasons = new List<string>();
        pot = null;
        if (!string.Equals(location.NameOrUniqueName, "Forest", StringComparison.Ordinal) ||
            !string.Equals(request.LocationId, "Forest", StringComparison.Ordinal))
        {
            reasons.Add("pot_of_gold_not_current_forest");
        }
        if (!Game1.IsSpring || Game1.dayOfMonth != 17)
        {
            reasons.Add("pot_of_gold_not_native_spring_17");
        }
        if (target != new Point(52, 98) ||
            !location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !string.Equals(item.QualifiedItemId, PotOfGoldQualifiedItemId, StringComparison.Ordinal) ||
            item.Stack != 1)
        {
            reasons.Add("pot_of_gold_exact_object_missing_or_drifted");
        }
        else
        {
            pot = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("pot_of_gold_interaction_geometry_drifted");
        }
        var expectedCoinQuantity = Math.Min(100, 7 + Game1.year);
        if (!string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, PotOfGoldQualifiedItemId, StringComparison.Ordinal) ||
            request.Quantity != expectedCoinQuantity ||
            !string.Equals(request.RewardBranch, "spring_17_forest_pot_of_gold", StringComparison.Ordinal) ||
            !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "PotOfGold", StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, PotOfGoldNativeContract, StringComparison.Ordinal) ||
            !PotOfGoldOutputContractMatches(request.ExpectedOutputItemsJson, expectedCoinQuantity))
        {
            reasons.Add("pot_of_gold_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickPotOfGoldClaim()
    {
        var active = activePotOfGoldClaim;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompletePotOfGoldClaim(active, false, "pot_of_gold_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompletePotOfGoldClaim(active, false, "pot_of_gold_timeout");
            return;
        }
        if (active.ActionIssued)
        {
            var potRemoved = !active.Location.objects.ContainsKey(active.Target.ToVector2());
            var coinDelta = CountPotOfGoldReward(active.Location, PotOfGoldCoinQualifiedItemId) - active.CoinTotalBefore;
            var hatDelta = CountPotOfGoldReward(active.Location, PotOfGoldHatQualifiedItemId) - active.HatTotalBefore;
            if (potRemoved && coinDelta == active.ExpectedCoinQuantity && hatDelta == 1)
            {
                CompletePotOfGoldClaim(active, true);
            }
            else if (active.ElapsedTicks - active.ActionIssuedAtTick > 120)
            {
                CompletePotOfGoldClaim(active, false, "pot_of_gold_native_reward_receipt_mismatch");
            }
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
            CompletePotOfGoldClaim(active, false, "pot_of_gold_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompletePotOfGoldClaim(active, false, "pot_of_gold_path_exhausted");
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
                CompletePotOfGoldClaim(active, false, "pot_of_gold_dynamic_path_blocked");
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
                CompletePotOfGoldClaim(active, false, "pot_of_gold_movement_stuck");
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
            CompletePotOfGoldClaim(active, false, "pot_of_gold_native_action_not_handled");
        }
    }

    private void CompletePotOfGoldClaim(ActivePotOfGoldClaim active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activePotOfGoldClaim = null;
        var request = active.Pending.Request;
        var coinAfter = CountPotOfGoldReward(active.Location, PotOfGoldCoinQualifiedItemId);
        var hatAfter = CountPotOfGoldReward(active.Location, PotOfGoldHatQualifiedItemId);
        var verificationReasons = verified
            ? new[] { "shared_bfs_reached_exact_adjacent_stand", "native_GameLocation_checkAction_handled_PotOfGold", "exact_year_scaled_coin_and_hat_rewards_conserved_across_inventory_and_debris", "remaining_debris_deferred_to_shared_pickup_executor" }
            : reasons.Length == 0 ? new[] { "pot_of_gold_post_state_mismatch" } : reasons;
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
            PrimitiveKind = "claim_pot_of_gold",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = PotOfGoldRequestedEffect(request),
            ObservedEffect = PotOfGoldObservedEffect(active.Location) + ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() + ";coin_total_delta=" + (coinAfter - active.CoinTotalBefore) + ";hat_total_delta=" + (hatAfter - active.HatTotalBefore),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.pot_of_gold_reward.exact_object_present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "reward_total[(O)GoldCoin]", Before = active.CoinTotalBefore.ToString(), After = coinAfter.ToString() },
                new SimulatedFactChange { Path = "reward_total[(H)LeprechuanHat]", Before = active.HatTotalBefore.ToString(), After = hatAfter.ToString() }
            }
        });
    }

    private static int CountPotOfGoldReward(GameLocation location, string qualifiedItemId)
    {
        var inventory = CountInventoryItem(qualifiedItemId);
        var debris = location.debris
            .Where(row => string.Equals(
                row.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(row.itemId.Value) ?? row.itemId.Value,
                qualifiedItemId,
                StringComparison.Ordinal))
            .Sum(row => Math.Max(1, row.item?.Stack ?? 1));
        return inventory + debris;
    }

    private static bool PotOfGoldOutputContractMatches(string json, int expectedCoinQuantity)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var rows = document.RootElement.EnumerateArray().ToArray();
            return rows.Length == 2 &&
                rows.Any(row => row.GetProperty("qualified_item_id").GetString() == PotOfGoldCoinQualifiedItemId && row.GetProperty("quantity").GetInt32() == expectedCoinQuantity && row.GetProperty("delivery").GetString() == "individual_item_debris") &&
                rows.Any(row => row.GetProperty("qualified_item_id").GetString() == PotOfGoldHatQualifiedItemId && row.GetProperty("quantity").GetInt32() == 1 && row.GetProperty("delivery").GetString() == "item_debris");
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string PotOfGoldRequestedEffect(TrainingExecutionRequest request) =>
        "current_location.pot_of_gold_reward.exact_object_present=false;reward_total_delta=(O)GoldCoin*" + request.Quantity + "+(H)LeprechuanHat*1";

    private static string PotOfGoldObservedEffect(GameLocation? location)
    {
        if (location is null)
        {
            return "location=unavailable";
        }
        var target = new Vector2(52f, 98f);
        var present = location.objects.TryGetValue(target, out var item) && item.QualifiedItemId == PotOfGoldQualifiedItemId;
        return "location=" + location.NameOrUniqueName + ";date=" + Game1.currentSeason + ":" + Game1.dayOfMonth + ";pot_present=" + present.ToString().ToLowerInvariant() + ";coin_total=" + CountPotOfGoldReward(location, PotOfGoldCoinQualifiedItemId) + ";hat_total=" + CountPotOfGoldReward(location, PotOfGoldHatQualifiedItemId);
    }

    private sealed class ActivePotOfGoldClaim
    {
        public ActivePotOfGoldClaim(PendingExecution pending, GameLocation location, StardewObject pot, Point target, Point stand, List<Point> path, int expectedCoinQuantity, int maxMovementTiles, int coinTotalBefore, int hatTotalBefore)
        {
            Pending = pending;
            Location = location;
            Pot = pot;
            Target = target;
            Stand = stand;
            Path = path;
            ExpectedCoinQuantity = expectedCoinQuantity;
            MaxMovementTiles = maxMovementTiles;
            CoinTotalBefore = coinTotalBefore;
            HatTotalBefore = hatTotalBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject Pot { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int ExpectedCoinQuantity { get; }
        public int MaxMovementTiles { get; }
        public int CoinTotalBefore { get; }
        public int HatTotalBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int ActionIssuedAtTick { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool ActionIssued { get; set; }
        public bool NativeHandled { get; set; }
    }
}
