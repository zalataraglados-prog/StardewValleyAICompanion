using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Constants;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string StatueOfBlessingsQualifiedItemId = "(BC)StatueOfBlessings";
    private const string StatueOfBlessingsNativeContract = "Object.checkForAction_StatueOfBlessings->CheckForActionOnBlessedStatue->Farmer.applyBuff(statue_of_blessings_N)";

    private void StartStatueBlessingClaim(PendingExecution pending)
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
            !request.StatueBlessingId.HasValue || !request.StatueBlessingDaysPlayed.HasValue)
        {
            pending.Completion.SetResult(StatueBlessingBlocked(request, "statue_blessing_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(StatueBlessingBlocked(request, "statue_blessing_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateStatueBlessingTarget(location, target, stand, request, out var statue);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(StatueBlessingBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(StatueBlessingBlocked(request, "statue_blessing_path_unavailable:" + pathReason));
            return;
        }

        activeStatueBlessingClaim = new ActiveStatueBlessingClaim(pending, location, statue!, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateStatueBlessingTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? statue)
    {
        var reasons = new List<string>();
        statue = null;
        if (Game1.player.stats.Get(StatKeys.Mastery(0)) < 1)
        {
            reasons.Add("statue_blessing_farming_mastery_required");
        }
        if (Game1.player.hasBeenBlessedByStatueToday || StatueBlessingActiveBuffIds().Length != 0)
        {
            reasons.Add("statue_blessing_already_claimed_today");
        }
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !string.Equals(item.QualifiedItemId, StatueOfBlessingsQualifiedItemId, StringComparison.Ordinal) ||
            !item.bigCraftable.Value)
        {
            reasons.Add("statue_blessing_exact_object_missing_or_drifted");
        }
        else
        {
            statue = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("statue_blessing_interaction_geometry_drifted");
        }

        var expectedId = CurrentStatueBlessingId(out var upperBound);
        if (request.StatueBlessingId != expectedId ||
            request.StatueBlessingDaysPlayed != Game1.stats.DaysPlayed ||
            request.StatueBlessingRandomUpperBoundExclusive != upperBound ||
            !string.Equals(request.StatueBlessingBuffId, "statue_of_blessings_" + expectedId, StringComparison.Ordinal) ||
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, StatueOfBlessingsQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "StatueOfBlessings", StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, StatueOfBlessingsNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("statue_blessing_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickStatueBlessingClaim()
    {
        var active = activeStatueBlessingClaim;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteStatueBlessingClaim(active, false, "statue_blessing_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteStatueBlessingClaim(active, false, "statue_blessing_timeout");
            return;
        }

        if (active.ActionIssued)
        {
            var activeBuffs = StatueBlessingActiveBuffIds();
            if (Game1.player.hasBeenBlessedByStatueToday &&
                activeBuffs.SequenceEqual(new[] { active.ExpectedBuffId }) &&
                Game1.activeClickableMenu is null)
            {
                CompleteStatueBlessingClaim(active, true);
            }
            else if (active.ElapsedTicks - active.ActionIssuedAtTick > 180)
            {
                CompleteStatueBlessingClaim(active, false, "statue_blessing_native_receipt_mismatch");
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
            CompleteStatueBlessingClaim(active, false, "statue_blessing_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteStatueBlessingClaim(active, false, "statue_blessing_path_exhausted");
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
                CompleteStatueBlessingClaim(active, false, "statue_blessing_dynamic_path_blocked");
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
                CompleteStatueBlessingClaim(active, false, "statue_blessing_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var currentStatue) ||
            !ReferenceEquals(currentStatue, active.Statue))
        {
            CompleteStatueBlessingClaim(active, false, "statue_blessing_object_replaced_while_moving");
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
            CompleteStatueBlessingClaim(active, false, "statue_blessing_native_action_not_handled");
        }
    }

    private void CompleteStatueBlessingClaim(ActiveStatueBlessingClaim active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeStatueBlessingClaim = null;
        var request = active.Pending.Request;
        var activeBuffs = StatueBlessingActiveBuffIds();
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_adjacent_stand",
                "native_Object_CheckForActionOnBlessedStatue_applied_daily_blessing",
                "exactly_one_predicted_statue_blessing_buff_observed",
                "native_hasBeenBlessedByStatueToday_lock_observed"
            }
            : reasons.Length == 0 ? new[] { "statue_blessing_post_state_mismatch" } : reasons;
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
            PrimitiveKind = "claim_statue_blessing",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = StatueBlessingRequestedEffect(request),
            ObservedEffect = "active_statue_blessing_buffs=" + string.Join(",", activeBuffs) +
                ";has_been_blessed_today=" + Game1.player.hasBeenBlessedByStatueToday.ToString().ToLowerInvariant() +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.statue_blessing.has_been_blessed_today", Before = "false", After = Game1.player.hasBeenBlessedByStatueToday.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "current_location.statue_blessing.active_blessing_buffs", Before = string.Empty, After = string.Join(",", activeBuffs) }
            }
        });
    }

    private static TrainingExecutionResult StatueBlessingBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "claim_statue_blessing", StatueBlessingRequestedEffect(request),
            "active_statue_blessing_buffs=" + string.Join(",", StatueBlessingActiveBuffIds()) +
            ";has_been_blessed_today=" + Game1.player.hasBeenBlessedByStatueToday.ToString().ToLowerInvariant(), reasons);

    private static string StatueBlessingRequestedEffect(TrainingExecutionRequest request) =>
        "player.has_buff[" + request.StatueBlessingBuffId + "]=true;player.has_been_blessed_today=true";

    private static int CurrentStatueBlessingId(out int upperBound)
    {
        var random = Utility.CreateDaySaveRandom(Game1.stats.DaysPlayed * 777);
        for (var i = 0; i < 8; i++)
        {
            random.Next();
        }
        upperBound = Game1.isRaining || Utility.isFestivalDay() ? 6 : 7;
        return random.Next(upperBound);
    }

    private static string[] StatueBlessingActiveBuffIds() => Game1.player.buffs.AppliedBuffs.Keys
        .Where(id => id.StartsWith("statue_of_blessings_", StringComparison.Ordinal))
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    private sealed class ActiveStatueBlessingClaim
    {
        public ActiveStatueBlessingClaim(PendingExecution pending, GameLocation location, StardewObject statue,
            Point target, Point stand, List<Point> path, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Statue = statue;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject Statue { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public string ExpectedBuffId => Pending.Request.StatueBlessingBuffId;
        public int MaxMovementTiles { get; }
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
