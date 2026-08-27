using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string SlimeBallQualifiedItemId = "(BC)56";
    private const string SlimeQualifiedItemId = "(O)766";
    private const string PetrifiedSlimeQualifiedItemId = "(O)557";
    private const string SlimeBallNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)56->CheckForActionOnSlimeBall->remove_object->seeded_(O)766_debris_10_20->seeded_geometric_(O)557_debris";

    private void StartSlimeBallCollection(PendingExecution pending)
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
            !request.RequiredFragility.HasValue || !request.SlimeBallSeedDaysPlayed.HasValue ||
            !request.SlimeBallSeedUniqueGameId.HasValue ||
            !request.SlimeBallExpectedSlimeQuantity.HasValue ||
            !request.SlimeBallExpectedPetrifiedSlimeQuantity.HasValue ||
            !request.SlimeBallExpectedLocationActionReturn.HasValue)
        {
            pending.Completion.SetResult(SlimeBallBlocked(request, "slime_ball_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(SlimeBallBlocked(request, "slime_ball_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateSlimeBallTarget(location, target, stand, request, out var slimeBall);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(SlimeBallBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(SlimeBallBlocked(request, "slime_ball_path_unavailable:" + pathReason));
            return;
        }

        activeSlimeBallCollection = new ActiveSlimeBallCollection(
            pending,
            location,
            slimeBall!,
            target,
            stand,
            path,
            maxMovementTiles,
            CountConservedItem(location, SlimeQualifiedItemId),
            CountConservedItem(location, PetrifiedSlimeQualifiedItemId));
    }

    private static string[] ValidateSlimeBallTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? slimeBall)
    {
        var reasons = new List<string>();
        slimeBall = null;
        if (location.GetType() != typeof(SlimeHutch) ||
            !location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            item.Fragility != 2 ||
            !string.Equals(item.Name, "Slime Ball", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, SlimeBallQualifiedItemId, StringComparison.Ordinal))
        {
            reasons.Add("slime_ball_exact_natural_object_missing_or_drifted");
        }
        else
        {
            slimeBall = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("slime_ball_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
        {
            reasons.Add("slime_ball_destructive_object_trap_preamble_blocked");
        }
        var safeSlotIndex = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (safeSlotIndex is < 0 or > 11 || safeSlotIndex >= Game1.player.Items.Count ||
            Game1.player.Items[safeSlotIndex] is not null)
        {
            reasons.Add("slime_ball_empty_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
        {
            reasons.Add("slime_ball_restore_slot_drifted");
        }

        if (slimeBall is not null)
        {
            var expected = ProjectSlimeBallOutputs(target);
            if (request.RequiredFragility != 2 ||
                request.SlimeBallSeedDaysPlayed != checked((int)Game1.stats.DaysPlayed) ||
                request.SlimeBallSeedUniqueGameId != checked((long)Game1.uniqueIDForThisGame) ||
                request.SlimeBallExpectedSlimeQuantity != expected.Slime ||
                request.SlimeBallExpectedPetrifiedSlimeQuantity != expected.Petrified ||
                request.SlimeBallExpectedLocationActionReturn != true ||
                !string.Equals(request.ItemId, slimeBall.ItemId, StringComparison.Ordinal) ||
                !string.Equals(request.QualifiedItemId, slimeBall.QualifiedItemId, StringComparison.Ordinal) ||
                !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
                !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
                !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
                !string.Equals(request.ExpectedActionType, "SlimeBall", StringComparison.Ordinal) ||
                !string.Equals(request.NativeContract, SlimeBallNativeContract, StringComparison.Ordinal))
            {
                reasons.Add("slime_ball_projection_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickSlimeBallCollection()
    {
        var active = activeSlimeBallCollection;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_timeout");
            return;
        }
        if (active.ActionIssued)
        {
            var objectRemoved = !active.Location.objects.ContainsKey(active.Target.ToVector2());
            var slimeDelta = CountConservedItem(active.Location, SlimeQualifiedItemId) - active.SlimeTotalBefore;
            var petrifiedDelta = CountConservedItem(active.Location, PetrifiedSlimeQualifiedItemId) - active.PetrifiedTotalBefore;
            if (objectRemoved && active.NativeHandled == active.ExpectedLocationActionReturn &&
                slimeDelta == active.ExpectedSlimeQuantity && petrifiedDelta == active.ExpectedPetrifiedSlimeQuantity &&
                Game1.activeClickableMenu is null)
            {
                CompleteSlimeBallCollection(active, true);
            }
            else if (active.ElapsedTicks - active.ActionIssuedAtTick > 120)
            {
                CompleteSlimeBallCollection(active, false, "slime_ball_native_output_receipt_mismatch");
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
            CompleteSlimeBallCollection(active, false, "slime_ball_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteSlimeBallCollection(active, false, "slime_ball_path_exhausted");
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
                CompleteSlimeBallCollection(active, false, "slime_ball_dynamic_path_blocked");
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
                CompleteSlimeBallCollection(active, false, "slime_ball_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) ||
            !ReferenceEquals(current, active.SlimeBall) || current.Fragility != 2 ||
            !string.Equals(current.QualifiedItemId, SlimeBallQualifiedItemId, StringComparison.Ordinal))
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_object_replaced_or_drifted_while_moving");
            return;
        }
        if (Game1.player.Items[active.SafeSlotIndex] is not null)
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_safe_slot_filled_while_moving");
            return;
        }
        if (IsDestructiveObjectTrap(active.Location, active.Stand))
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_destructive_object_trap_preamble_blocked");
            return;
        }

        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.CurrentItem is not null || Game1.player.ActiveObject is not null)
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_empty_hand_selection_failed");
            return;
        }
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        active.ActionIssuedAtTick = active.ElapsedTicks;
        if (!active.NativeHandled)
        {
            CompleteSlimeBallCollection(active, false, "slime_ball_native_action_not_handled");
        }
    }

    private void CompleteSlimeBallCollection(ActiveSlimeBallCollection active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeSlimeBallCollection = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var request = active.Pending.Request;
        var slimeAfter = CountConservedItem(active.Location, SlimeQualifiedItemId);
        var petrifiedAfter = CountConservedItem(active.Location, PetrifiedSlimeQualifiedItemId);
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_adjacent_stand",
                "empty_toolbar_slot_selected_for_native_location_action",
                "native_GameLocation_checkAction_removed_exact_slime_ball",
                "seeded_slime_and_petrified_slime_outputs_conserved_across_inventory_and_debris",
                "remaining_debris_deferred_to_shared_pickup_executor",
                "selected_toolbar_slot_restored"
            }
            : reasons.Length == 0 ? new[] { "slime_ball_post_state_mismatch" } : reasons;
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
            TrainingImpactScope = "policy_training",
            PrimitiveKind = "collect_slime_ball",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = SlimeBallRequestedEffect(request),
            ObservedEffect = SlimeBallObservedEffect(active.Location, active.Target) +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";slime_total_delta=" + (slimeAfter - active.SlimeTotalBefore) +
                ";petrified_slime_total_delta=" + (petrifiedAfter - active.PetrifiedTotalBefore) +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.objects[" + active.Target.X + "," + active.Target.Y + "].present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "conserved_output[(O)766]", Before = active.SlimeTotalBefore.ToString(), After = slimeAfter.ToString() },
                new SimulatedFactChange { Path = "conserved_output[(O)557]", Before = active.PetrifiedTotalBefore.ToString(), After = petrifiedAfter.ToString() },
                new SimulatedFactChange { Path = "player.current_tool_index", Before = active.RestoreSlotIndex.ToString(), After = Game1.player.CurrentToolIndex.ToString() }
            }
        });
    }

    private static (int Slime, int Petrified) ProjectSlimeBallOutputs(Point target)
    {
        var random = Utility.CreateRandom(
            Game1.stats.DaysPlayed,
            Game1.uniqueIDForThisGame,
            target.X * 77d,
            target.Y * 777d,
            2d);
        var slime = random.Next(10, 21);
        var petrified = 0;
        while (random.NextDouble() < 0.33)
        {
            petrified++;
        }
        return (slime, petrified);
    }

    private static int CountConservedItem(GameLocation location, string qualifiedItemId)
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

    private static TrainingExecutionResult SlimeBallBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "collect_slime_ball", SlimeBallRequestedEffect(request),
            SlimeBallObservedEffect(Game1.currentLocation,
                new Point(request.TargetTileX.GetValueOrDefault(-1), request.TargetTileY.GetValueOrDefault(-1))), reasons);

    private static string SlimeBallRequestedEffect(TrainingExecutionRequest request) =>
        "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY + "].present=false" +
        ";conserved_output[(O)766]+=" + request.SlimeBallExpectedSlimeQuantity +
        ";conserved_output[(O)557]+=" + request.SlimeBallExpectedPetrifiedSlimeQuantity +
        ";selected_slot_restored=true";

    private static string SlimeBallObservedEffect(GameLocation? location, Point target)
    {
        if (location is null)
        {
            return "location=unavailable";
        }
        var present = location.objects.TryGetValue(target.ToVector2(), out var item) &&
            string.Equals(item.QualifiedItemId, SlimeBallQualifiedItemId, StringComparison.Ordinal);
        return "location=" + location.NameOrUniqueName +
            ";slime_ball_present=" + present.ToString().ToLowerInvariant() +
            ";slime_total=" + CountConservedItem(location, SlimeQualifiedItemId) +
            ";petrified_slime_total=" + CountConservedItem(location, PetrifiedSlimeQualifiedItemId);
    }

    private sealed class ActiveSlimeBallCollection
    {
        public ActiveSlimeBallCollection(PendingExecution pending, GameLocation location, StardewObject slimeBall,
            Point target, Point stand, List<Point> path, int maxMovementTiles, int slimeTotalBefore,
            int petrifiedTotalBefore)
        {
            Pending = pending;
            Location = location;
            SlimeBall = slimeBall;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            ExpectedSlimeQuantity = pending.Request.SlimeBallExpectedSlimeQuantity!.Value;
            ExpectedPetrifiedSlimeQuantity = pending.Request.SlimeBallExpectedPetrifiedSlimeQuantity!.Value;
            ExpectedLocationActionReturn = pending.Request.SlimeBallExpectedLocationActionReturn!.Value;
            SlimeTotalBefore = slimeTotalBefore;
            PetrifiedTotalBefore = petrifiedTotalBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject SlimeBall { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public int RestoreSlotIndex { get; }
        public int ExpectedSlimeQuantity { get; }
        public int ExpectedPetrifiedSlimeQuantity { get; }
        public bool ExpectedLocationActionReturn { get; }
        public int SlimeTotalBefore { get; }
        public int PetrifiedTotalBefore { get; }
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
