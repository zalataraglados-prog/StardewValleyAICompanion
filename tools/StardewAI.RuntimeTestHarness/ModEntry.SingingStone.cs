using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string SingingStoneQualifiedItemId = "(BC)94";
    private const string SingingStoneNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)94->CheckForActionOnSingingStone->Game1.random.Next(2400)_floor_to_100->Game1.playSound_crystal_pitch->shakeTimer_100";

    private void StartSingingStone(PendingExecution pending)
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
            !request.SingingStonePitchMin.HasValue || !request.SingingStonePitchMax.HasValue ||
            !request.SingingStonePitchStep.HasValue || !request.SingingStonePitchOutcomeCount.HasValue ||
            !request.SingingStoneExpectedShakeTimer.HasValue ||
            !request.SingingStoneExpectedLocationActionReturn.HasValue)
        {
            pending.Completion.SetResult(SingingStoneBlocked(request, "singing_stone_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(SingingStoneBlocked(request, "singing_stone_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateSingingStoneTarget(location, target, stand, request, out var stone);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(SingingStoneBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(SingingStoneBlocked(request, "singing_stone_path_unavailable:" + pathReason));
            return;
        }

        nativeObjectInteractions.SingingStone = new ActiveSingingStone(
            pending, location, stone!, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateSingingStoneTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? stone)
    {
        var reasons = new List<string>();
        stone = null;
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.Name, "Singing Stone", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !string.Equals(item.ItemId, "94", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, SingingStoneQualifiedItemId, StringComparison.Ordinal))
        {
            reasons.Add("singing_stone_exact_object_missing_or_drifted");
        }
        else
        {
            stone = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("singing_stone_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
            reasons.Add("singing_stone_destructive_object_trap_preamble_blocked");

        var safeSlotIndex = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (safeSlotIndex is < 0 or > 11 || safeSlotIndex >= Game1.player.Items.Count)
        {
            reasons.Add("singing_stone_safe_toolbar_slot_drifted");
        }
        else
        {
            var safeItem = Game1.player.Items[safeSlotIndex];
            var safeKindMatches = request.SingingStoneSafeSlotKind switch
            {
                "empty" => safeItem is null,
                "tool" => safeItem is Tool,
                _ => false
            };
            if (!safeKindMatches)
                reasons.Add("singing_stone_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
            reasons.Add("singing_stone_restore_slot_drifted");

        if (stone is not null &&
            (request.SingingStonePitchMin != 0 || request.SingingStonePitchMax != 2300 ||
             request.SingingStonePitchStep != 100 || request.SingingStonePitchOutcomeCount != 24 ||
             request.SingingStoneExpectedShakeTimer != 100 ||
             request.SingingStoneExpectedLocationActionReturn != true ||
             !string.Equals(request.SingingStoneSoundName, "crystal", StringComparison.Ordinal) ||
             !string.Equals(request.SingingStonePitchRngSource, "Game1.random_shared_unread", StringComparison.Ordinal) ||
             !string.Equals(request.SingingStoneExactNextPitchStatus, "unavailable_shared_rng_state_not_consumed", StringComparison.Ordinal) ||
             !string.Equals(request.ItemId, stone.ItemId, StringComparison.Ordinal) ||
             !string.Equals(request.QualifiedItemId, stone.QualifiedItemId, StringComparison.Ordinal) ||
             !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
             !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
             !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
             !string.Equals(request.ExpectedActionType, "SingingStone", StringComparison.Ordinal) ||
             !string.Equals(request.NativeContract, SingingStoneNativeContract, StringComparison.Ordinal)))
        {
            reasons.Add("singing_stone_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickSingingStone()
    {
        var active = nativeObjectInteractions.SingingStone;
        if (active is null)
            return;
        var movement = AdvanceNativeObjectInteractionMovement(active, "singing_stone", out var movementFailure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            CompleteSingingStone(active, false, movementFailure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;

        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var currentStone) ||
            !ReferenceEquals(currentStone, active.Stone) ||
            !string.Equals(currentStone.ItemId, active.BeforeItemId, StringComparison.Ordinal) ||
            !string.Equals(currentStone.QualifiedItemId, active.BeforeQualifiedItemId, StringComparison.Ordinal))
        {
            CompleteSingingStone(active, false, "singing_stone_object_replaced_or_drifted_while_moving");
            return;
        }
        var safeItem = Game1.player.Items[active.SafeSlotIndex];
        if ((active.SafeSlotKind == "empty" && safeItem is not null) ||
            (active.SafeSlotKind == "tool" && safeItem is not Tool) ||
            IsDestructiveObjectTrap(active.Location, active.Stand))
        {
            CompleteSingingStone(active, false, "singing_stone_safe_context_drifted_while_moving");
            return;
        }

        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.ActiveObject is not null)
        {
            CompleteSingingStone(active, false, "singing_stone_active_object_selection_failed");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);

        var verified = active.NativeHandled == active.ExpectedLocationActionReturn &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var afterStone) &&
            ReferenceEquals(afterStone, active.Stone) &&
            afterStone.shakeTimer == active.ExpectedShakeTimer &&
            string.Equals(afterStone.ItemId, active.BeforeItemId, StringComparison.Ordinal) &&
            string.Equals(afterStone.QualifiedItemId, active.BeforeQualifiedItemId, StringComparison.Ordinal) &&
            Game1.activeClickableMenu is null;
        CompleteSingingStone(active, verified,
            verified ? Array.Empty<string>() : new[] { "singing_stone_native_receipt_mismatch" });
    }

    private void CompleteSingingStone(ActiveSingingStone active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        nativeObjectInteractions.SingingStone = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var current = active.Location.objects.TryGetValue(active.Target.ToVector2(), out var item) ? item : null;
        var verificationReasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "safe_toolbar_slot_selected_without_active_object",
                "native_GameLocation_checkAction_played_singing_stone_branch",
                "native_shake_timer_100_observed",
                "shared_rng_pitch_correctly_remained_distribution_only",
                "canonical_item_identity_unchanged",
                "selected_toolbar_slot_restored"
            }
            : reasons.Length == 0 ? new[] { "singing_stone_post_state_mismatch" } : reasons;
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
            TrainingImpactScope = "player_command_only_executor_evidence",
            PrimitiveKind = "play_singing_stone",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = SingingStoneRequestedEffect(active.Pending.Request),
            ObservedEffect = "shake_timer=" + (current?.shakeTimer.ToString() ?? "missing") +
                ";item_id=" + (current?.ItemId ?? "missing") +
                ";qualified_item_id=" + (current?.QualifiedItemId ?? "missing") +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.objects[" + active.Target.X + "," + active.Target.Y + "].shake_timer",
                    Before = active.BeforeShakeTimer.ToString(),
                    After = current?.shakeTimer.ToString() ?? "missing"
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

    private static TrainingExecutionResult SingingStoneBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "play_singing_stone", SingingStoneRequestedEffect(request),
            "singing_stone_current_state=" + SingingStoneCurrentObserved(request), reasons);

    private static string SingingStoneRequestedEffect(TrainingExecutionRequest request) =>
        "native_sound=crystal;pitch_distribution=uniform_0_2300_step_100;shake_timer=" +
        request.SingingStoneExpectedShakeTimer + ";item_identity_unchanged=true;selected_slot_restored=true";

    private static string SingingStoneCurrentObserved(TrainingExecutionRequest request)
    {
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || Game1.currentLocation is null ||
            !Game1.currentLocation.objects.TryGetValue(
                new Vector2(request.TargetTileX.Value, request.TargetTileY.Value), out var item))
        {
            return "missing";
        }
        return item.shakeTimer + ":" + item.ItemId + ":" + item.QualifiedItemId;
    }

    private sealed class ActiveSingingStone : INativeObjectInteractionMovement
    {
        public ActiveSingingStone(PendingExecution pending, GameLocation location, StardewObject stone,
            Point target, Point stand, List<Point> path, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Stone = stone;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            SafeSlotKind = pending.Request.SingingStoneSafeSlotKind;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            BeforeShakeTimer = stone.shakeTimer;
            BeforeItemId = stone.ItemId;
            BeforeQualifiedItemId = stone.QualifiedItemId;
            ExpectedShakeTimer = pending.Request.SingingStoneExpectedShakeTimer!.Value;
            ExpectedLocationActionReturn = pending.Request.SingingStoneExpectedLocationActionReturn!.Value;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject Stone { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public string SafeSlotKind { get; }
        public int RestoreSlotIndex { get; }
        public int BeforeShakeTimer { get; }
        public string BeforeItemId { get; }
        public string BeforeQualifiedItemId { get; }
        public int ExpectedShakeTimer { get; }
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
    }
}
