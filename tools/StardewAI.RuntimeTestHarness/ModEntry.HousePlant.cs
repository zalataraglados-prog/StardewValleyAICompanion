using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string HousePlantNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)0..7->CheckForActionOnHousePlant;empty_hand;location_calls_object_twice_only_when_first_returns_false";

    private void StartHousePlantRotation(PendingExecution pending)
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
            !request.HousePlantCurrentSpriteIndex.HasValue ||
            !request.HousePlantExpectedSpriteIndex.HasValue ||
            !request.HousePlantExpectedObjectActionCalls.HasValue ||
            !request.HousePlantExpectedLocationActionReturn.HasValue)
        {
            pending.Completion.SetResult(HousePlantBlocked(request, "house_plant_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(HousePlantBlocked(request, "house_plant_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateHousePlantTarget(location, target, stand, request, out var plant);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(HousePlantBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(HousePlantBlocked(request, "house_plant_path_unavailable:" + pathReason));
            return;
        }

        activeHousePlantRotation = new ActiveHousePlantRotation(
            pending, location, plant!, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateHousePlantTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? plant)
    {
        var reasons = new List<string>();
        plant = null;
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.Name, "House Plant", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !IsCanonicalHousePlantQualifiedItemId(item.QualifiedItemId) ||
            item.ParentSheetIndex is < 0 or > 7)
        {
            reasons.Add("house_plant_exact_object_missing_or_drifted");
        }
        else
        {
            plant = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("house_plant_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
        {
            reasons.Add("house_plant_destructive_object_trap_preamble_blocked");
        }
        var safeSlotIndex = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (safeSlotIndex is < 0 or > 11 ||
            safeSlotIndex >= Game1.player.Items.Count ||
            Game1.player.Items[safeSlotIndex] is not null)
        {
            reasons.Add("house_plant_empty_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 ||
            request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
        {
            reasons.Add("house_plant_restore_slot_drifted");
        }

        if (plant is not null)
        {
            var expectedSprite = plant.ParentSheetIndex == 7 ? 1 : plant.ParentSheetIndex + 1;
            var expectedCalls = plant.ParentSheetIndex == 7 ? 2 : 1;
            if (request.HousePlantCurrentSpriteIndex != plant.ParentSheetIndex ||
                request.HousePlantExpectedSpriteIndex != expectedSprite ||
                request.HousePlantExpectedObjectActionCalls != expectedCalls ||
                request.HousePlantExpectedLocationActionReturn != true ||
                !string.Equals(request.ItemId, plant.ItemId, StringComparison.Ordinal) ||
                !string.Equals(request.QualifiedItemId, plant.QualifiedItemId, StringComparison.Ordinal) ||
                !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
                !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
                !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
                !string.Equals(request.ExpectedActionType, "HousePlant", StringComparison.Ordinal) ||
                !string.Equals(request.NativeContract, HousePlantNativeContract, StringComparison.Ordinal))
            {
                reasons.Add("house_plant_projection_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickHousePlantRotation()
    {
        var active = activeHousePlantRotation;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteHousePlantRotation(active, false, "house_plant_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteHousePlantRotation(active, false, "house_plant_timeout");
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
            CompleteHousePlantRotation(active, false, "house_plant_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteHousePlantRotation(active, false, "house_plant_path_exhausted");
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
                CompleteHousePlantRotation(active, false, "house_plant_dynamic_path_blocked");
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
                CompleteHousePlantRotation(active, false, "house_plant_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var currentPlant) ||
            !ReferenceEquals(currentPlant, active.Plant) ||
            currentPlant.ParentSheetIndex != active.BeforeSpriteIndex ||
            !string.Equals(currentPlant.ItemId, active.BeforeItemId, StringComparison.Ordinal) ||
            !string.Equals(currentPlant.QualifiedItemId, active.BeforeQualifiedItemId, StringComparison.Ordinal))
        {
            CompleteHousePlantRotation(active, false, "house_plant_object_replaced_or_drifted_while_moving");
            return;
        }
        if (Game1.player.Items[active.SafeSlotIndex] is not null)
        {
            CompleteHousePlantRotation(active, false, "house_plant_safe_slot_filled_while_moving");
            return;
        }
        if (IsDestructiveObjectTrap(active.Location, active.Stand))
        {
            CompleteHousePlantRotation(active, false, "house_plant_destructive_object_trap_preamble_blocked");
            return;
        }

        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.CurrentItem is not null || Game1.player.ActiveObject is not null)
        {
            CompleteHousePlantRotation(active, false, "house_plant_empty_hand_selection_failed");
            return;
        }
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);

        var verified = active.NativeHandled == active.ExpectedLocationActionReturn &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var afterPlant) &&
            ReferenceEquals(afterPlant, active.Plant) &&
            afterPlant.ParentSheetIndex == active.ExpectedSpriteIndex &&
            string.Equals(afterPlant.ItemId, active.BeforeItemId, StringComparison.Ordinal) &&
            string.Equals(afterPlant.QualifiedItemId, active.BeforeQualifiedItemId, StringComparison.Ordinal) &&
            Game1.activeClickableMenu is null;
        CompleteHousePlantRotation(active, verified,
            verified ? Array.Empty<string>() : new[] { "house_plant_native_receipt_mismatch" });
    }

    private void CompleteHousePlantRotation(ActiveHousePlantRotation active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeHousePlantRotation = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var request = active.Pending.Request;
        var current = active.Location.objects.TryGetValue(active.Target.ToVector2(), out var item) ? item : null;
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_adjacent_stand",
                "empty_toolbar_slot_selected_for_native_location_action",
                "native_GameLocation_checkAction_advanced_exact_house_plant_frame",
                "canonical_item_identity_unchanged",
                "selected_toolbar_slot_restored"
            }
            : reasons.Length == 0 ? new[] { "house_plant_post_state_mismatch" } : reasons;
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
            PrimitiveKind = "rotate_house_plant",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = HousePlantRequestedEffect(request),
            ObservedEffect = "parent_sheet_index=" + (current?.ParentSheetIndex.ToString() ?? "missing") +
                ";item_id=" + (current?.ItemId ?? "missing") +
                ";qualified_item_id=" + (current?.QualifiedItemId ?? "missing") +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.objects[" + active.Target.X + "," + active.Target.Y + "].parent_sheet_index",
                    Before = active.BeforeSpriteIndex.ToString(),
                    After = current?.ParentSheetIndex.ToString() ?? "missing"
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

    private static TrainingExecutionResult HousePlantBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "rotate_house_plant", HousePlantRequestedEffect(request),
            "house_plant_current_state=" + HousePlantCurrentObserved(request), reasons);

    private static string HousePlantRequestedEffect(TrainingExecutionRequest request) =>
        "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY + "].parent_sheet_index=" +
        request.HousePlantExpectedSpriteIndex + ";item_identity_unchanged=true;selected_slot_restored=true";

    private static string HousePlantCurrentObserved(TrainingExecutionRequest request)
    {
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || Game1.currentLocation is null ||
            !Game1.currentLocation.objects.TryGetValue(
                new Vector2(request.TargetTileX.Value, request.TargetTileY.Value), out var item))
        {
            return "missing";
        }
        return item.ParentSheetIndex + ":" + item.ItemId + ":" + item.QualifiedItemId;
    }

    private static bool IsCanonicalHousePlantQualifiedItemId(string qualifiedItemId) =>
        qualifiedItemId is "(BC)0" or "(BC)1" or "(BC)2" or "(BC)3" or
            "(BC)4" or "(BC)5" or "(BC)6" or "(BC)7";

    private static bool IsDestructiveObjectTrap(GameLocation location, Point stand) =>
        Neighbors(stand).All(tile =>
            location.objects.TryGetValue(tile.ToVector2(), out var item) &&
            !item.isPassable());

    private sealed class ActiveHousePlantRotation
    {
        public ActiveHousePlantRotation(PendingExecution pending, GameLocation location, StardewObject plant,
            Point target, Point stand, List<Point> path, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Plant = plant;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            BeforeSpriteIndex = plant.ParentSheetIndex;
            BeforeItemId = plant.ItemId;
            BeforeQualifiedItemId = plant.QualifiedItemId;
            ExpectedSpriteIndex = pending.Request.HousePlantExpectedSpriteIndex!.Value;
            ExpectedLocationActionReturn = pending.Request.HousePlantExpectedLocationActionReturn!.Value;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject Plant { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public int RestoreSlotIndex { get; }
        public int BeforeSpriteIndex { get; }
        public string BeforeItemId { get; }
        public string BeforeQualifiedItemId { get; }
        public int ExpectedSpriteIndex { get; }
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
