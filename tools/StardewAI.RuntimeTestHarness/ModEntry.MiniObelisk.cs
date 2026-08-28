using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string MiniObeliskQualifiedItemId = "(BC)238";
    private const string MiniObeliskNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)238->CheckForActionOnMiniObelisk;native_first_two_nonzero_pair;farther_from_interaction_stand;landing_order_down_left_right_up;IsTileBlockedBy_All_ignorePassables_All;fade_delay_50ms";

    private void StartMiniObeliskUse(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        if (!HasRequiredMiniObeliskFields(request))
        {
            pending.Completion.SetResult(MiniObeliskBlocked(request, "mini_obelisk_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(MiniObeliskBlocked(request, "mini_obelisk_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var source = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var reasons = ValidateMiniObeliskTarget(location, source, stand, request, out var nativePair);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(MiniObeliskBlocked(request, reasons));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(MiniObeliskBlocked(request, "mini_obelisk_path_unavailable:" + pathReason));
            return;
        }

        nativeObjectInteractions.MiniObelisk = new ActiveMiniObeliskUse(
            pending, location, nativePair!, source, stand,
            new Point(request.MiniObeliskDestinationTileX!.Value, request.MiniObeliskDestinationTileY!.Value),
            new Point(request.MiniObeliskLandingTileX!.Value, request.MiniObeliskLandingTileY!.Value),
            path, maxMovementTiles);
    }

    private static bool HasRequiredMiniObeliskFields(TrainingExecutionRequest request) =>
        request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue &&
        request.SafeSlotIndex.HasValue && request.RestoreSlotIndex.HasValue &&
        request.MiniObeliskPairMemberIndex.HasValue &&
        request.MiniObeliskPairFirstTileX.HasValue && request.MiniObeliskPairFirstTileY.HasValue &&
        request.MiniObeliskPairSecondTileX.HasValue && request.MiniObeliskPairSecondTileY.HasValue &&
        request.MiniObeliskDestinationTileX.HasValue && request.MiniObeliskDestinationTileY.HasValue &&
        request.MiniObeliskLandingTileX.HasValue && request.MiniObeliskLandingTileY.HasValue &&
        request.MiniObeliskExpectedDelayMilliseconds.HasValue &&
        request.MiniObeliskExpectedLocationActionReturn.HasValue;

    private static string[] ValidateMiniObeliskTarget(
        GameLocation location,
        Point source,
        Point stand,
        TrainingExecutionRequest request,
        out RuntimeMiniObeliskPair? nativePair)
    {
        var reasons = new List<string>();
        nativePair = ReadRuntimeNativeMiniObeliskPair(location);
        if (nativePair is null)
        {
            reasons.Add("mini_obelisk_native_pair_missing_or_zero_sentinel_blocked");
            return reasons.ToArray();
        }
        if (!IsExactBaseMiniObelisk(nativePair.First.Item) || !IsExactBaseMiniObelisk(nativePair.Second.Item))
            reasons.Add("mini_obelisk_native_pair_not_exact_base_objects");

        var expectedFirst = new Point(request.MiniObeliskPairFirstTileX!.Value, request.MiniObeliskPairFirstTileY!.Value);
        var expectedSecond = new Point(request.MiniObeliskPairSecondTileX!.Value, request.MiniObeliskPairSecondTileY!.Value);
        if (nativePair.First.Tile.ToPoint() != expectedFirst || nativePair.Second.Tile.ToPoint() != expectedSecond)
            reasons.Add("mini_obelisk_native_pair_order_drifted");

        var memberIndex = request.MiniObeliskPairMemberIndex!.Value;
        var expectedSource = memberIndex switch
        {
            0 => nativePair.First,
            1 => nativePair.Second,
            _ => null
        };
        if (expectedSource is null || expectedSource.Tile.ToPoint() != source ||
            !location.objects.TryGetValue(source.ToVector2(), out var sourceObject) ||
            !ReferenceEquals(sourceObject, expectedSource.Item))
        {
            reasons.Add("mini_obelisk_source_not_requested_native_pair_member");
        }

        if (!AreAdjacent(source, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("mini_obelisk_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
            reasons.Add("mini_obelisk_destructive_object_trap_preamble_blocked");

        var destination = ReadRuntimeNativeMiniObeliskDestination(stand, nativePair);
        var expectedDestination = new Point(
            request.MiniObeliskDestinationTileX!.Value,
            request.MiniObeliskDestinationTileY!.Value);
        if (destination.ToPoint() != expectedDestination || expectedDestination == source)
            reasons.Add("mini_obelisk_native_destination_drifted");

        var landing = ReadRuntimeFirstNativeMiniObeliskLanding(location, destination);
        var expectedLanding = new Point(request.MiniObeliskLandingTileX!.Value, request.MiniObeliskLandingTileY!.Value);
        if (landing is null || landing.Value.ToPoint() != expectedLanding)
            reasons.Add("mini_obelisk_native_landing_drifted");

        var safeSlotIndex = request.SafeSlotIndex!.Value;
        if (safeSlotIndex is < 0 or > 11 || safeSlotIndex >= Game1.player.Items.Count)
        {
            reasons.Add("mini_obelisk_safe_toolbar_slot_drifted");
        }
        else
        {
            var safeItem = Game1.player.Items[safeSlotIndex];
            var safeKindMatches = request.MiniObeliskSafeSlotKind switch
            {
                "empty" => safeItem is null,
                "tool" => safeItem is Tool,
                _ => false
            };
            if (!safeKindMatches)
                reasons.Add("mini_obelisk_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
            reasons.Add("mini_obelisk_restore_slot_drifted");

        if (expectedSource is not null &&
            (request.MiniObeliskExpectedDelayMilliseconds != 50 ||
             request.MiniObeliskExpectedLocationActionReturn != true ||
             !string.Equals(request.ItemId, expectedSource.Item.ItemId, StringComparison.Ordinal) ||
             !string.Equals(request.QualifiedItemId, expectedSource.Item.QualifiedItemId, StringComparison.Ordinal) ||
             !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
             !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
             !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
             !string.Equals(request.ExpectedActionType, "MiniObelisk", StringComparison.Ordinal) ||
             !string.Equals(request.NativeContract, MiniObeliskNativeContract, StringComparison.Ordinal)))
        {
            reasons.Add("mini_obelisk_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickMiniObeliskUse()
    {
        var active = nativeObjectInteractions.MiniObelisk;
        if (active is null)
            return;
        if (active.ActionIssued)
        {
            TickMiniObeliskReceipt(active);
            return;
        }

        var movement = AdvanceNativeObjectInteractionMovement(active, "mini_obelisk", out var movementFailure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            CompleteMiniObelisk(active, false, movementFailure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;

        var reasons = ValidateMiniObeliskTarget(
            active.Location, active.Source, active.Stand, active.Pending.Request, out var currentPair);
        if (reasons.Length > 0 || currentPair is null ||
            !ReferenceEquals(currentPair.First.Item, active.Pair.First.Item) ||
            !ReferenceEquals(currentPair.Second.Item, active.Pair.Second.Item))
        {
            CompleteMiniObelisk(active, false,
                reasons.Length > 0 ? reasons : new[] { "mini_obelisk_pair_replaced_while_moving" });
            return;
        }

        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.ActiveObject is not null)
        {
            CompleteMiniObelisk(active, false, "mini_obelisk_active_object_selection_failed");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Source));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Source.X, active.Source.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        active.ActionIssuedAtTick = active.ElapsedTicks;
        if (active.NativeHandled != active.ExpectedLocationActionReturn)
            CompleteMiniObelisk(active, false, "mini_obelisk_native_action_return_mismatch");
    }

    private void TickMiniObeliskReceipt(ActiveMiniObeliskUse active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteMiniObelisk(active, false, "mini_obelisk_location_changed_during_native_delay");
            return;
        }
        if (!PairReferencesRemainExact(active))
        {
            CompleteMiniObelisk(active, false, "mini_obelisk_pair_replaced_during_native_delay");
            return;
        }

        var arrived = Game1.player.TilePoint == active.Landing && Game1.displayFarmer &&
            Game1.activeClickableMenu is null && !Game1.dialogueUp;
        if (arrived)
        {
            CompleteMiniObelisk(active, true);
            return;
        }
        if (active.ElapsedTicks - active.ActionIssuedAtTick > 180)
            CompleteMiniObelisk(active, false, "mini_obelisk_native_delay_receipt_timeout");
    }

    private static bool PairReferencesRemainExact(ActiveMiniObeliskUse active)
    {
        var currentPair = ReadRuntimeNativeMiniObeliskPair(active.Location);
        return currentPair is not null &&
            currentPair.First.Tile == active.Pair.First.Tile &&
            currentPair.Second.Tile == active.Pair.Second.Tile &&
            ReferenceEquals(currentPair.First.Item, active.Pair.First.Item) &&
            ReferenceEquals(currentPair.Second.Item, active.Pair.Second.Item) &&
            IsExactBaseMiniObelisk(currentPair.First.Item) &&
            IsExactBaseMiniObelisk(currentPair.Second.Item);
    }

    private void CompleteMiniObelisk(ActiveMiniObeliskUse active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        nativeObjectInteractions.MiniObelisk = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var verificationReasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "native_first_two_nonzero_mini_obelisk_pair_replayed",
                "native_farther_endpoint_and_ordered_landing_recomputed_at_action_time",
                "native_GameLocation_checkAction_started_mini_obelisk_branch",
                "native_delayed_teleport_landed_on_exact_projected_tile",
                "canonical_pair_references_and_identity_unchanged",
                "selected_toolbar_slot_restored"
            }
            : reasons.Length == 0 ? new[] { "mini_obelisk_post_state_mismatch" } : reasons;
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
            TrainingImpactScope = "executor_calibration_only_not_strategy_desire",
            PrimitiveKind = "use_mini_obelisk",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = MiniObeliskRequestedEffect(active.Pending.Request),
            ObservedEffect = "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";native_destination=" + active.Destination.X + "," + active.Destination.Y +
                ";display_farmer=" + Game1.displayFarmer.ToString().ToLowerInvariant() +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";pair_identity_unchanged=" + PairReferencesRemainExact(active).ToString().ToLowerInvariant() +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.tile",
                    Before = active.Stand.X + "," + active.Stand.Y,
                    After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y
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

    private static TrainingExecutionResult MiniObeliskBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(request, "use_mini_obelisk", MiniObeliskRequestedEffect(request),
            "mini_obelisk_current_state=" + MiniObeliskCurrentObserved(request), reasons);

    private static string MiniObeliskRequestedEffect(TrainingExecutionRequest request) =>
        "source=" + request.TargetTileX + "," + request.TargetTileY +
        ";destination=" + request.MiniObeliskDestinationTileX + "," + request.MiniObeliskDestinationTileY +
        ";landing=" + request.MiniObeliskLandingTileX + "," + request.MiniObeliskLandingTileY +
        ";delay_ms=" + request.MiniObeliskExpectedDelayMilliseconds +
        ";pair_identity_unchanged=true;selected_slot_restored=true";

    private static string MiniObeliskCurrentObserved(TrainingExecutionRequest request)
    {
        var pair = Game1.currentLocation is null ? null : ReadRuntimeNativeMiniObeliskPair(Game1.currentLocation);
        return pair is null
            ? "native_pair=missing"
            : "native_pair=" + (int)pair.First.Tile.X + "," + (int)pair.First.Tile.Y + "|" +
                (int)pair.Second.Tile.X + "," + (int)pair.Second.Tile.Y +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static RuntimeMiniObeliskPair? ReadRuntimeNativeMiniObeliskPair(GameLocation location)
    {
        RuntimeMiniObeliskMember? first = null;
        RuntimeMiniObeliskMember? second = null;
        var firstTile = Vector2.Zero;
        var secondTile = Vector2.Zero;
        foreach (var row in location.objects.Pairs)
        {
            if (!row.Value.bigCraftable.Value ||
                !string.Equals(row.Value.QualifiedItemId, MiniObeliskQualifiedItemId, StringComparison.Ordinal))
            {
                continue;
            }
            if (firstTile == Vector2.Zero)
            {
                firstTile = row.Key;
                first = new RuntimeMiniObeliskMember(row.Key, row.Value);
            }
            else if (secondTile == Vector2.Zero)
            {
                secondTile = row.Key;
                second = new RuntimeMiniObeliskMember(row.Key, row.Value);
                break;
            }
        }
        return secondTile == Vector2.Zero || first is null || second is null
            ? null
            : new RuntimeMiniObeliskPair(first, second);
    }

    private static Vector2 ReadRuntimeNativeMiniObeliskDestination(Point stand, RuntimeMiniObeliskPair pair)
    {
        var standTile = stand.ToVector2();
        return Vector2.Distance(standTile, pair.First.Tile) > Vector2.Distance(standTile, pair.Second.Tile)
            ? pair.First.Tile
            : pair.Second.Tile;
    }

    private static Vector2? ReadRuntimeFirstNativeMiniObeliskLanding(GameLocation location, Vector2 destination)
    {
        var candidates = new[]
        {
            new Vector2(destination.X, destination.Y + 1f),
            new Vector2(destination.X - 1f, destination.Y),
            new Vector2(destination.X + 1f, destination.Y),
            new Vector2(destination.X, destination.Y - 1f)
        };
        foreach (var candidate in candidates)
        {
            if (!location.IsTileBlockedBy(candidate, CollisionMask.All, CollisionMask.All))
                return candidate;
        }
        return null;
    }

    private static bool IsExactBaseMiniObelisk(StardewObject item) =>
        item.GetType() == typeof(StardewObject) && item.bigCraftable.Value &&
        string.Equals(item.QualifiedItemId, MiniObeliskQualifiedItemId, StringComparison.Ordinal);

    private sealed record RuntimeMiniObeliskMember(Vector2 Tile, StardewObject Item);

    private sealed record RuntimeMiniObeliskPair(
        RuntimeMiniObeliskMember First,
        RuntimeMiniObeliskMember Second);

    private sealed class ActiveMiniObeliskUse : INativeObjectInteractionMovement
    {
        public ActiveMiniObeliskUse(
            PendingExecution pending,
            GameLocation location,
            RuntimeMiniObeliskPair pair,
            Point source,
            Point stand,
            Point destination,
            Point landing,
            List<Point> path,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Pair = pair;
            Source = source;
            Stand = stand;
            Destination = destination;
            Landing = landing;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            ExpectedLocationActionReturn = pending.Request.MiniObeliskExpectedLocationActionReturn!.Value;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public RuntimeMiniObeliskPair Pair { get; }
        public Point Source { get; }
        public Point Stand { get; }
        public Point Destination { get; }
        public Point Landing { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public int RestoreSlotIndex { get; }
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
        public bool ActionIssued { get; set; }
        public int ActionIssuedAtTick { get; set; }
    }
}
