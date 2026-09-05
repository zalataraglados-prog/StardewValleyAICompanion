using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartTileMove(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(pending.Request, reasons.ToArray()));
            return;
        }

        var primitiveKind = MovementPrimitiveKind(pending.Request);
        if ((pending.Request.OptionId == "executor.move_to_tile" ||
             pending.Request.OptionId == "executor.traverse_connector") &&
            (!pending.Request.TargetTileX.HasValue || !pending.Request.TargetTileY.HasValue))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                "player.tile=missing",
                "player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "movement_target_tile_required"));
            return;
        }

        if (pending.Request.OptionId == "executor.traverse_connector" &&
            string.IsNullOrWhiteSpace(pending.Request.ExpectedTargetLocation))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                ConnectorRequestedEffect(pending.Request),
                ConnectorObservedEffect(),
                "connector_expected_target_location_required"));
            return;
        }

        if (activeTileMove is not null)
        {
            pending.Completion.SetResult(Blocked(pending.Request, "movement_executor_busy"));
            return;
        }

        var startTile = Game1.player.TilePoint;
        var requestedTargetTile = ResolveTargetTile(pending.Request, startTile);
        var connectorTargetBlock = ValidateConnectorTarget(pending.Request, requestedTargetTile);
        if (!string.IsNullOrWhiteSpace(connectorTargetBlock))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                ConnectorRequestedEffect(pending.Request),
                ConnectorObservedEffect(),
                connectorTargetBlock));
            return;
        }

        var targetTile = requestedTargetTile;
        Point? connectorActionTile = null;
        int? connectorExitDirection = null;
        if (pending.Request.OptionId == "executor.traverse_connector" && IsActionConnectorKind(pending.Request.ConnectorKind))
        {
            connectorActionTile = requestedTargetTile;

            if (string.Equals(pending.Request.ConnectorKind, "building_door", StringComparison.OrdinalIgnoreCase))
            {
                var building = Game1.currentLocation.buildings
                    .FirstOrDefault(b =>
                        b.humanDoor.X >= 0 && b.humanDoor.Y >= 0 &&
                        b.tileX.Value + b.humanDoor.X == requestedTargetTile.X &&
                        b.tileY.Value + b.humanDoor.Y == requestedTargetTile.Y);
                if (building is null)
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_building_door_building_not_found"));
                    return;
                }

                var standTile = new Point(requestedTargetTile.X, requestedTargetTile.Y + 1);
                if (!IsTileTraversableForPlan(Game1.currentLocation, standTile, avoidSoftObstacles: true))
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_building_door_stand_tile_blocked"));
                    return;
                }

                targetTile = standTile;
            }
            else
            {
                var standTile = FindConnectorActionStandTile(Game1.currentLocation, startTile, requestedTargetTile);
                if (standTile is null)
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_action_stand_tile_unavailable"));
                    return;
                }

                targetTile = standTile.Value;
            }
        }
        else if (pending.Request.OptionId == "executor.traverse_connector" &&
            IsStepOntoConnectorKind(pending.Request.ConnectorKind) &&
            IsTileOnMap(Game1.currentLocation, requestedTargetTile))
        {
            connectorActionTile = requestedTargetTile;
            var standTile = FindConnectorActionStandTile(Game1.currentLocation, startTile, requestedTargetTile);
            if (standTile is null)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(
                    pending.Request,
                    primitiveKind,
                    ConnectorRequestedEffect(pending.Request),
                    ConnectorObservedEffect(),
                    "connector_warp_stand_tile_unavailable"));
                return;
            }

            targetTile = standTile.Value;
        }
        else if (pending.Request.OptionId == "executor.traverse_connector" &&
            string.Equals(pending.Request.ConnectorKind, "warp", StringComparison.OrdinalIgnoreCase) &&
            !IsTileOnMap(Game1.currentLocation, requestedTargetTile) &&
            TryResolveBoundaryWarpStandTile(Game1.currentLocation, requestedTargetTile, out var boundaryStandTile, out var boundaryDirection))
        {
            targetTile = boundaryStandTile;
            connectorExitDirection = boundaryDirection;
        }

        if (startTile == targetTile)
        {
            if (pending.Request.OptionId == "executor.traverse_connector")
            {
                if (!IsActionConnectorKind(pending.Request.ConnectorKind) &&
                    !IsStepOntoConnectorKind(pending.Request.ConnectorKind))
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_already_on_target_without_location_change"));
                    return;
                }

                activeTileMove = new ActiveTileMove(pending, startTile, targetTile, new List<Point>(), connectorActionTile, connectorExitDirection);
                return;
            }

            pending.Completion.SetResult(CompletedMove(pending, startTile, startTile, startTile, "verified", new[] { "already_at_target_tile" }));
            return;
        }

        var maxTiles = Math.Clamp(pending.Request.MaxMovementTiles ?? pending.Request.MaxCrops, 1, 512);
        var path = TryBuildTilePath(
            Game1.currentLocation,
            startTile,
            targetTile,
            maxTiles,
            out var blockReason,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                "player.tile=" + targetTile.X + "," + targetTile.Y,
                "player.tile=" + startTile.X + "," + startTile.Y,
                blockReason));
            return;
        }

        activeTileMove = new ActiveTileMove(pending, startTile, targetTile, path, connectorActionTile, connectorExitDirection);
        Monitor.Log($"Started collision-checked tile move from {startTile.X},{startTile.Y} to {targetTile.X},{targetTile.Y} with {path.Count} path tile(s).", LogLevel.Info);
    }

    private void TickTileMove(UpdateTickedEventArgs e)
    {
        if (activeTileMove is null)
        {
            return;
        }

        var move = activeTileMove;
        if (activeVolcanoCombat is not null &&
            Game1.currentLocation is VolcanoDungeon)
        {
            return;
        }

        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            CompleteBlockedMove(move, "world_not_ready_during_movement");
            return;
        }

        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, move.LocationId, StringComparison.Ordinal))
        {
            if (move.AllowsLocationChange)
            {
                CompleteConnectorMoveAfterLocationChange(move);
                return;
            }

            CompleteBlockedMove(move, "location_changed_during_movement");
            return;
        }

        if (TickMovementIncidentalDialogue(move))
        {
            return;
        }

        var connectorCommitReady =
            move.AllowsLocationChange &&
            string.Equals(
                move.Pending.Request.OptionId,
                "executor.traverse_connector",
                StringComparison.Ordinal) &&
            (ManhattanDistance(
                    Game1.player.TilePoint,
                    move.TargetTile) <= 1 ||
                move.ConnectorActionTile.HasValue &&
                ManhattanDistance(
                    Game1.player.TilePoint,
                    move.ConnectorActionTile.Value) <= 1);
        if (!connectorCommitReady &&
            (string.Equals(
                 move.Pending.Request.OptionId,
                 "executor.move_to_tile",
                 StringComparison.Ordinal) ||
             string.Equals(
                 move.Pending.Request.OptionId,
                 "executor.traverse_connector",
                 StringComparison.Ordinal)) &&
            Game1.currentLocation is VolcanoDungeon volcano &&
            ImmediateVolcanoThreat(volcano))
        {
            if (!TryStartReactiveVolcanoCombat(
                    volcano,
                    "movement_immediate_threat"))
            {
                CompleteBlockedMove(
                    move,
                    "volcano_movement_unsafe_monster_window");
            }
            return;
        }

        if (Game1.player.TilePoint == move.TargetTile)
        {
            if (move.AllowsLocationChange)
            {
                move.PathIndex = move.Path.Count;
                move.StuckTicks = 0;
                move.LastPosition = Game1.player.Position;
                if (TryTriggerConnectorAction(move))
                {
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, move.LocationId, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    return;
                }

                if (move.ConnectorExitDirection.HasValue)
                {
                    var beforeLocation = Game1.currentLocation.NameOrUniqueName;
                    StartMoving(move.ConnectorExitDirection.Value);
                    MovePlayerForTick();
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, beforeLocation, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    move.Tick++;
                    if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
                    {
                        CompleteBlockedMove(move, "connector_boundary_warp_timeout");
                    }

                    return;
                }

                if (IsStepOntoConnectorKind(move.Pending.Request.ConnectorKind) &&
                    move.ConnectorActionTile.HasValue)
                {
                    var beforeLocation = Game1.currentLocation.NameOrUniqueName;
                    var warpStepDirection = DirectionTo(Game1.player.TilePoint, move.ConnectorActionTile.Value);
                    StartMoving(warpStepDirection);
                    MovePlayerForTick();
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, beforeLocation, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    move.Tick++;
                    if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
                    {
                        CompleteBlockedMove(move, "connector_warp_step_timeout");
                    }

                    return;
                }

                move.Tick++;
                if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
                {
                    CompleteBlockedMove(move, "connector_target_reached_without_location_change");
                }

                return;
            }

            if (!IsFarmerCenteredOnTile(move.TargetTile))
            {
                SettleFarmerOnTargetTile(move);
                return;
            }

            CompleteMove(move, "verified", new[] { "target_tile_reached", "target_tile_centered" });
            return;
        }

        if (move.PathIndex >= move.Path.Count)
        {
            if (move.AllowsLocationChange)
            {
                if (move.ConnectorActionTile.HasValue &&
                    IsStepOntoConnectorKind(
                        move.Pending.Request.ConnectorKind) &&
                    Game1.player.TilePoint ==
                        move.ConnectorActionTile.Value)
                {
                    StopAllMovement();
                    move.CurrentDirection = null;
                    move.Tick++;
                    if (!config.DisableMovementTimeouts &&
                        move.Tick > move.MaxTicks)
                    {
                        CompleteBlockedMove(
                            move,
                            "connector_warp_step_timeout");
                    }
                    return;
                }

                CompleteBlockedMove(move, "connector_path_exhausted_before_location_change");
                return;
            }

            CompleteMove(move, "observed_mismatch", new[] { "path_exhausted_before_target_tile" });
            return;
        }

        var currentTile = Game1.player.TilePoint;
        var nextTile = move.Path[move.PathIndex];
        if (currentTile == nextTile)
        {
            move.PathIndex++;
            move.StuckTicks = 0;
            move.LastPosition = Game1.player.Position;
            return;
        }

        if (IsTileOccupiedByCharacter(Game1.currentLocation, nextTile))
        {
            TickTileMoveSoftObstacle(move, currentTile, nextTile);
            return;
        }

        move.SoftObstacleTicks = 0;

        if (!IsTileWalkable(Game1.currentLocation, nextTile))
        {
            if (TryClearRemovableObstacle(Game1.currentLocation, nextTile, move))
            {
                StopAllMovement();
                move.CurrentDirection = null;
                ReplanTileMove(move, avoidSoftObstacles: true);
                return;
            }

            if (ReplanTileMove(move, avoidSoftObstacles: true))
            {
                StopAllMovement();
                move.CurrentDirection = null;
                return;
            }

            CompleteBlockedMove(move, "movement_hard_obstacle_not_clearable");
            return;
        }

        if (!AreAdjacent(currentTile, nextTile))
        {
            if (ReplanTileMove(move, avoidSoftObstacles: true))
            {
                StopAllMovement();
                move.CurrentDirection = null;
                return;
            }
            CompleteBlockedMove(move, "movement_path_desynchronized");
            return;
        }

        var direction = DirectionTo(currentTile, nextTile);
        var movedSinceLastTick =
            Vector2.DistanceSquared(
                move.LastPosition,
                Game1.player.Position) >= 0.01f;
        if (move.CurrentDirection.HasValue &&
            move.CurrentDirection.Value != direction &&
            movedSinceLastTick &&
            !HasReachedTurnCenter(currentTile, move.CurrentDirection.Value))
        {
            direction = move.CurrentDirection.Value;
        }
        move.LastPosition = Game1.player.Position;
        StartMovingIfNeeded(move, direction);
        MovePlayerForTick();

        if (Game1.player.TilePoint == nextTile)
        {
            move.PathIndex++;
        }

        if (!movedSinceLastTick)
        {
            move.StuckTicks++;
        }
        else
        {
            move.StuckTicks = 0;
            move.LastPosition = Game1.player.Position;
        }

        if (move.StuckTicks > 45)
        {
            CompleteBlockedMove(
                move,
                "movement_stuck_or_collision_blocked:" +
                "tile=" + currentTile.X + "," + currentTile.Y +
                ";next=" + nextTile.X + "," + nextTile.Y +
                ";direction=" + direction +
                ";can_move=" + Game1.player.CanMove.ToString().ToLowerInvariant() +
                ";using_tool=" + Game1.player.UsingTool.ToString().ToLowerInvariant() +
                ";menu=" + (Game1.activeClickableMenu?.GetType().FullName ?? "none") +
                ";standing_pixel=" + Game1.player.StandingPixel.X + "," + Game1.player.StandingPixel.Y);
            return;
        }

        move.Tick++;
        if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
        {
            CompleteBlockedMove(move, "movement_timeout");
        }
    }

    private bool TickMovementIncidentalDialogue(ActiveTileMove move)
    {
        if (move.IncidentalDialogueButtonHeld)
        {
            if (!TryApplySmapiLeftButtonOverride(
                    pressed: false,
                    out var releaseReason))
            {
                CompleteBlockedMove(
                    move,
                    "movement_incidental_dialogue_release_failed:" +
                        releaseReason);
                return true;
            }
            move.IncidentalDialogueButtonHeld = false;
            return true;
        }

        if (Game1.activeClickableMenu is null)
        {
            return false;
        }
        if (Game1.activeClickableMenu is not DialogueBox dialogue ||
            dialogue.isQuestion ||
            dialogue.characterDialogue is not null ||
            Game1.eventUp)
        {
            CompleteBlockedMove(
                move,
                "movement_interrupted_by_non_incidental_menu");
            return true;
        }

        StopAllMovement();
        move.CurrentDirection = null;
        move.StuckTicks = 0;
        move.LastPosition = Game1.player.Position;
        if (dialogue.transitioning || dialogue.safetyTimer > 0)
        {
            return true;
        }
        if (move.IncidentalDialoguePressAttempts >= 16)
        {
            CompleteBlockedMove(
                move,
                "movement_incidental_dialogue_dismiss_budget_exceeded");
            return true;
        }
        if (!TryApplySmapiLeftButtonOverride(
                pressed: true,
                out var pressReason))
        {
            CompleteBlockedMove(
                move,
                "movement_incidental_dialogue_press_failed:" +
                    pressReason);
            return true;
        }

        if (move.IncidentalDialoguePressAttempts == 0)
        {
            move.Pending.MovementIncidentalDialogues++;
        }
        move.IncidentalDialoguePressAttempts++;
        move.IncidentalDialogueButtonHeld = true;
        return true;
    }

    private void ReleaseMovementIncidentalDialogueButton(
        ActiveTileMove move)
    {
        if (!move.IncidentalDialogueButtonHeld)
        {
            return;
        }
        TryApplySmapiLeftButtonOverride(pressed: false, out _);
        move.IncidentalDialogueButtonHeld = false;
    }

    private static bool IsFarmerCenteredOnTile(Point tile)
    {
        var center = Game1.player.StandingPixel;
        var targetX = tile.X * Game1.tileSize + Game1.tileSize / 2;
        var targetY = tile.Y * Game1.tileSize + Game1.tileSize / 2;
        var tolerance = Math.Max(
            2,
            (int)Math.Ceiling(Game1.player.getMovementSpeed() / 2f));
        return Math.Abs(center.X - targetX) <= tolerance &&
            Math.Abs(center.Y - targetY) <= tolerance;
    }

    private static bool HasReachedTurnCenter(Point tile, int currentDirection)
    {
        var center = Game1.player.StandingPixel;
        var targetX = tile.X * Game1.tileSize + Game1.tileSize / 2;
        var targetY = tile.Y * Game1.tileSize + Game1.tileSize / 2;
        var tolerance = Math.Max(
            2,
            (int)Math.Ceiling(Game1.player.getMovementSpeed() / 2f));
        return currentDirection switch
        {
            0 => center.Y <= targetY + tolerance,
            1 => center.X >= targetX - tolerance,
            2 => center.Y >= targetY - tolerance,
            3 => center.X <= targetX + tolerance,
            _ => true
        };
    }

    private void SettleFarmerOnTargetTile(ActiveTileMove move)
    {
        var currentPosition = Game1.player.Position;
        var movedSinceLastTick =
            Vector2.DistanceSquared(move.LastPosition, currentPosition) >= 0.01f;
        var center = Game1.player.StandingPixel;
        var targetCenter = new Point(
            move.TargetTile.X * Game1.tileSize + Game1.tileSize / 2,
            move.TargetTile.Y * Game1.tileSize + Game1.tileSize / 2);
        var direction = DirectionToPixel(
            center,
            targetCenter,
            Game1.player.FacingDirection);

        move.LastPosition = currentPosition;
        StartMovingIfNeeded(move, direction);
        MovePlayerForTick();

        move.StuckTicks = movedSinceLastTick ? 0 : move.StuckTicks + 1;
        if (move.StuckTicks > 45)
        {
            CompleteBlockedMove(
                move,
                "movement_target_tile_centering_stuck_or_collision_blocked");
            return;
        }

        move.Tick++;
        if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
        {
            CompleteBlockedMove(move, "movement_target_tile_centering_timeout");
        }
    }

    private bool TryTriggerConnectorAction(ActiveTileMove move)
    {
        if (move.ConnectorActionAttempted)
        {
            return false;
        }

        var kind = move.Pending.Request.ConnectorKind;
        if (!IsActionConnectorKind(kind))
        {
            return false;
        }

        move.ConnectorActionAttempted = true;
        var actionTile = move.ConnectorActionTile ?? move.TargetTile;

        if (string.Equals(kind, "building_door", StringComparison.OrdinalIgnoreCase))
        {
            if (!ValidateBuildingDoorConnector(move, actionTile))
            {
                return true;
            }
        }
        else
        {
            var rawAction = Game1.currentLocation.doesTileHaveProperty(actionTile.X, actionTile.Y, "Action", "Buildings");
            if (string.IsNullOrWhiteSpace(rawAction))
            {
                CompleteBlockedMove(move, "connector_action_property_missing");
                return true;
            }

            var actionType = rawAction.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (!IsConnectorActionTypeWhitelisted(actionType))
            {
                CompleteBlockedMove(move, "connector_action_type_not_whitelisted");
                return true;
            }

            if (string.Equals(kind, "locked_door_warp", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actionType, "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
            {
                CompleteBlockedMove(move, "connector_action_type_mismatch");
                return true;
            }

            if (string.Equals(kind, "action_warp", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actionType, "Warp", StringComparison.OrdinalIgnoreCase))
            {
                CompleteBlockedMove(move, "connector_action_type_mismatch");
                return true;
            }
        }

        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, actionTile));
        var handled = Game1.currentLocation.checkAction(
            new TileLocation(actionTile.X, actionTile.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled)
        {
            CompleteBlockedMove(move, "connector_action_not_handled");
        }

        return true;
    }

    private static int? ParseIntPart(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length && int.TryParse(parts[index], out var value)
            ? value
            : null;
    }

    private void CompleteConnectorMoveAfterLocationChange(ActiveTileMove move)
    {
        ReleaseMovementIncidentalDialogueButton(move);
        StopAllMovement();
        activeTileMove = null;

        var request = move.Pending.Request;
        var observedLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var observedTile = Game1.player.TilePoint;
        var reasons = new List<string>();

        if (!string.Equals(observedLocation, request.ExpectedTargetLocation, StringComparison.Ordinal))
        {
            reasons.Add("connector_unexpected_target_location");
        }

        if (request.ExpectedArrivalTileX.HasValue && request.ExpectedArrivalTileY.HasValue)
        {
            var expectedArrival = new Point(request.ExpectedArrivalTileX.Value, request.ExpectedArrivalTileY.Value);
            if (observedTile != expectedArrival)
            {
                reasons.Add("connector_native_arrival_tile_adjusted");
            }
        }

        var verified = string.Equals(
            observedLocation,
            request.ExpectedTargetLocation,
            StringComparison.Ordinal);
        if (verified && reasons.Count == 0)
        {
            reasons.Add("connector_location_changed_as_expected");
        }

        move.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = move.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "traverse_connector",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons.ToArray(),
            RequestedEffect = ConnectorRequestedEffect(request),
            ObservedEffect = ConnectorObservedEffect() + MovementCostSuffix(move.Pending),
            BlockReasons = verified ? Array.Empty<string>() : reasons.ToArray(),
            ChangedFacts = ConnectorChangedFacts(move.Pending, move.LocationId, move.StartTile, observedLocation, observedTile)
        });
    }

    private static string MovementCostSuffix(PendingExecution pending)
    {
        return pending.MovementExtraTicks > 0 ||
            pending.MovementClearanceActions > 0 ||
            pending.MovementIncidentalDialogues > 0
            ? ";movement_extra_ticks=" + pending.MovementExtraTicks +
                ";clearance_actions=" + pending.MovementClearanceActions +
                ";incidental_dialogues=" +
                pending.MovementIncidentalDialogues
            : string.Empty;
    }

    private static SimulatedFactChange[] MovementChangedFacts(PendingExecution pending, Point startTile, Point observedTile)
    {
        return pending.ChangedFacts
            .Prepend(new SimulatedFactChange
            {
                Path = "player.tile",
                Before = startTile.X + "," + startTile.Y,
                After = observedTile.X + "," + observedTile.Y
            })
            .ToArray();
    }

    private static SimulatedFactChange[] ConnectorChangedFacts(PendingExecution pending, string startLocation, Point startTile, string observedLocation, Point observedTile)
    {
        return pending.ChangedFacts
            .Prepend(new SimulatedFactChange
            {
                Path = "player.tile",
                Before = startTile.X + "," + startTile.Y,
                After = observedTile.X + "," + observedTile.Y
            })
            .Prepend(new SimulatedFactChange
            {
                Path = "player.location_id",
                Before = startLocation,
                After = observedLocation
            })
            .ToArray();
    }

}
