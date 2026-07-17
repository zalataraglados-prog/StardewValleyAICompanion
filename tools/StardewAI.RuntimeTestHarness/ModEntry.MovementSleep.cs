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
        var path = TryBuildTilePath(Game1.currentLocation, startTile, targetTile, maxTiles, out var blockReason);
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

            CompleteMove(move, "verified", new[] { "target_tile_reached" });
            return;
        }

        if (move.PathIndex >= move.Path.Count)
        {
            if (move.AllowsLocationChange)
            {
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
            StopAllMovement();
            move.CurrentDirection = null;
            move.SoftObstacleTicks++;
            if (move.SoftObstacleTicks % 30 == 0)
            {
                ReplanTileMove(move, avoidSoftObstacles: true);
            }

            if (move.SoftObstacleTicks > 180)
            {
                CompleteBlockedMove(move, "movement_soft_obstacle_timeout");
            }

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
            CompleteBlockedMove(move, "movement_path_desynchronized");
            return;
        }

        var direction = DirectionTo(currentTile, nextTile);
        var movedSinceLastTick = Vector2.DistanceSquared(move.LastPosition, Game1.player.Position) >= 0.01f;
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
            CompleteBlockedMove(move, "movement_stuck_or_collision_blocked");
            return;
        }

        move.Tick++;
        if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
        {
            CompleteBlockedMove(move, "movement_timeout");
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
                reasons.Add("connector_unexpected_arrival_tile");
            }
        }

        if (reasons.Count == 0)
        {
            reasons.Add("connector_location_changed_as_expected");
        }

        var verified = reasons.Count == 1 && reasons[0] == "connector_location_changed_as_expected";
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
        return pending.MovementExtraTicks > 0 || pending.MovementClearanceActions > 0
            ? ";movement_extra_ticks=" + pending.MovementExtraTicks + ";clearance_actions=" + pending.MovementClearanceActions
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

    private bool ReplanTileMove(ActiveTileMove move, bool avoidSoftObstacles)
    {
        var currentTile = Game1.player.TilePoint;
        var remainingTiles = Math.Max(1, 512 - move.PathIndex);
        var path = TryBuildTilePath(Game1.currentLocation, currentTile, move.TargetTile, remainingTiles, out _, avoidSoftObstacles);
        if (path is null)
        {
            return false;
        }

        move.Path = path;
        move.PathIndex = 0;
        move.CurrentDirection = null;
        move.StuckTicks = 0;
        move.SoftObstacleTicks = 0;
        move.Pending.MovementExtraTicks += 30;
        return true;
    }

    private bool TryClearRemovableObstacle(GameLocation location, Point tile, ActiveTileMove move)
    {
        if (!CanClearRouteObstacles(location))
        {
            return false;
        }

        var key = new Vector2(tile.X, tile.Y);
        var before = ObstacleLabel(location, tile);
        var tool = SelectClearanceTool(location, tile);
        if (tool is null)
        {
            return false;
        }

        var staminaBefore = Game1.player.Stamina;
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, tile));
        ApplyClearanceTool(location, tile, tool);

        if (Game1.activeClickableMenu is DialogueBox)
        {
            Game1.exitActiveMenu();
            return false;
        }

        if (!IsTileWalkable(location, tile) && location.objects.ContainsKey(key))
        {
            return false;
        }

        move.Pending.MovementClearanceActions++;
        move.Pending.MovementExtraTicks += ClearanceTickCost(tool);
        move.Pending.ChangedFacts.Add(new SimulatedFactChange
        {
            Path = "movement.clearance[" + tile.X + "," + tile.Y + "]",
            Before = before,
            After = ObstacleLabel(location, tile)
        });
        move.Pending.ChangedFacts.Add(new SimulatedFactChange
        {
            Path = "player.energy",
            Before = staminaBefore.ToString("0.###"),
            After = Game1.player.Stamina.ToString("0.###")
        });
        return true;
    }

    private TrainingExecutionResult ExecuteClearObstacle(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", "current_location.obstacle=clear", ClearObstacleObservedEffect(null), "clear_obstacle_target_tile_required");
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = "current_location.obstacle[" + target.X + "," + target.Y + "]=clear";
        if (!CanClearRouteObstacles(location))
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_location_not_whitelisted");
        }

        if (ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_target_not_adjacent");
        }

        var tool = SelectClearanceTool(location, target);
        if (tool is null)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_no_matching_tool_or_obstacle");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var before = ObstacleLabel(location, target);
        var staminaBefore = Game1.player.Stamina;
        var swings = Math.Clamp(request.MaxCrops, 1, 64);
        var observedLabels = new List<string> { before };
        for (var swing = 0; swing < swings; swing++)
        {
            if (ObstacleLabel(location, target) == "clear")
            {
                break;
            }

            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
            ApplyClearanceTool(location, target, tool);
            if (Game1.activeClickableMenu is DialogueBox)
            {
                Game1.exitActiveMenu();
                break;
            }

            observedLabels.Add(ObstacleLabel(location, target));
        }

        var after = ObstacleLabel(location, target);
        var verified = after == "clear";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            EnergyBefore = staminaBefore,
            EnergyAfter = Game1.player.Stamina,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "clear_obstacle",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_obstacle_cleared", "tool=" + tool.GetType().Name }
                : new[] { "target_obstacle_still_present", "tool=" + tool.GetType().Name },
            RequestedEffect = requested,
            ObservedEffect = "before=" + before + ";after=" + after + ";labels=" + string.Join(">", observedLabels),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "target_obstacle_still_present" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.obstacle[" + target.X + "," + target.Y + "]",
                    Before = before,
                    After = after
                },
                new SimulatedFactChange
                {
                    Path = "player.energy",
                    Before = staminaBefore.ToString("0.###"),
                    After = Game1.player.Stamina.ToString("0.###")
                }
            }
        };
    }

    private static void ApplyClearanceTool(GameLocation location, Point target, Tool tool)
    {
        var tile = new Vector2(target.X, target.Y);
        if (location.terrainFeatures.TryGetValue(tile, out var feature))
        {
            if (feature.performToolAction(tool, 0, tile))
            {
                location.terrainFeatures.Remove(tile);
            }

            return;
        }

        tool.DoFunction(location, target.X * Game1.tileSize, target.Y * Game1.tileSize, 0, Game1.player);
    }

    private static string ClearObstacleObservedEffect(Point? target)
    {
        return target.HasValue
            ? "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.Value.X + "," + target.Value.Y + ";obstacle=" + ObstacleLabel(Game1.currentLocation, target.Value)
            : "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static Tool? SelectClearanceTool(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            if (obj is BreakableContainer)
            {
                return FindHeavyTool();
            }

            if (obj.IsBreakableStone())
            {
                return FindTool<Pickaxe>();
            }

            if (obj.IsWeeds())
            {
                return FindScythe() ?? FindHeavyTool();
            }

            if (obj.IsTwig())
            {
                return FindTool<Axe>();
            }

            return null;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return feature switch
            {
                Grass => FindScythe() ?? FindHeavyTool(),
                Tree => FindTool<Axe>(),
                FruitTree => FindTool<Axe>(),
                _ => null
            };
        }

        var tileRect = TileRectangle(tile);
        foreach (var largeFeature in location.largeTerrainFeatures)
        {
            if (largeFeature.getBoundingBox().Intersects(tileRect))
            {
                return FindTool<Axe>();
            }
        }

        return null;
    }

    private static TTool? FindTool<TTool>() where TTool : Tool
    {
        return Game1.player.Items.OfType<TTool>().FirstOrDefault();
    }

    private static Tool? FindScythe()
    {
        return Game1.player.Items.OfType<MeleeWeapon>().FirstOrDefault(weapon => weapon.isScythe());
    }

    private static Tool? FindHeavyTool()
    {
        return Game1.player.Items.OfType<Tool>().FirstOrDefault(tool => tool.isHeavyHitter());
    }

    private static int ClearanceTickCost(Tool tool)
    {
        return tool switch
        {
            MeleeWeapon => 30,
            Axe => 60,
            Pickaxe => 60,
            _ => 60
        };
    }

    private static string ObstacleLabel(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            return "object:" + obj.QualifiedItemId + ":" + obj.Name;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return "terrain_feature:" + feature.GetType().Name;
        }

        var tileRect = TileRectangle(tile);
        if (location.largeTerrainFeatures.Any(feature => feature.getBoundingBox().Intersects(tileRect)))
        {
            return "large_terrain_feature";
        }

        if (location.resourceClumps.Any(clump => clump.getBoundingBox().Intersects(tileRect)))
        {
            return "resource_clump";
        }

        return "clear";
    }

    private static bool IsRemovableObstacle(GameLocation location, Point tile)
    {
        return CanClearRouteObstacles(location) && SelectClearanceTool(location, tile) is not null;
    }

    private static bool CanClearRouteObstacles(GameLocation location)
    {
        return location.IsFarm
            || location is MineShaft
            || location is VolcanoDungeon
            || string.Equals(location.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTileOccupiedByCharacter(GameLocation location, Point tile)
    {
        var tileRect = TileRectangle(tile);
        return location.characters.Any(character => character.GetBoundingBox().Intersects(tileRect));
    }

    private static XnaRectangle TileRectangle(Point tile)
    {
        return new XnaRectangle(tile.X * Game1.tileSize, tile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
    }

    private static bool IsTileTraversableForPlan(GameLocation location, Point tile, bool avoidSoftObstacles, bool allowRemovableObstacles = true)
    {
        if (!IsTileOnMap(location, tile))
        {
            return false;
        }

        if (avoidSoftObstacles && IsTileOccupiedByCharacter(location, tile))
        {
            return false;
        }

        return IsTileWalkable(location, tile) || allowRemovableObstacles && IsRemovableObstacle(location, tile) || IsTileOccupiedByCharacter(location, tile);
    }

    private static bool IsTileHardBlocked(GameLocation location, Point tile)
    {
        return !IsTileWalkable(location, tile) && !IsRemovableObstacle(location, tile) && !IsTileOccupiedByCharacter(location, tile);
    }

    private static string MovementHardBlockReason(GameLocation location, Point tile)
    {
        if (!IsTileOnMap(location, tile))
        {
            return "movement_target_tile_out_of_map";
        }

        if (IsTileOccupiedByCharacter(location, tile))
        {
            return "movement_target_soft_obstacle";
        }

        if (IsRemovableObstacle(location, tile))
        {
            return "movement_target_requires_clearance";
        }

        return "movement_target_tile_hard_blocked";
    }

    private void CompleteMove(ActiveTileMove move, string verificationStatus, string[] verificationReasons)
    {
        StopAllMovement();
        activeTileMove = null;
        move.Pending.Completion.SetResult(CompletedMove(move.Pending, move.StartTile, move.TargetTile, Game1.player.TilePoint, verificationStatus, verificationReasons));
    }

    private void CompleteBlockedMove(ActiveTileMove move, string reason)
    {
        StopAllMovement();
        activeTileMove = null;
        move.Pending.Completion.SetResult(BlockedWithPrimitive(
            move.Pending.Request,
            MovementPrimitiveKind(move.Pending.Request),
            MovementRequestedEffect(move.Pending.Request, move.TargetTile),
            MovementObservedEffect(),
            reason));
    }

    private static string MovementPrimitiveKind(TrainingExecutionRequest request)
    {
        return request.OptionId == "executor.traverse_connector" ? "traverse_connector" : "move_to_tile";
    }

    private static string MovementRequestedEffect(TrainingExecutionRequest request, Point targetTile)
    {
        return request.OptionId == "executor.traverse_connector"
            ? ConnectorRequestedEffect(request)
            : "player.tile=" + targetTile.X + "," + targetTile.Y;
    }

    private static string MovementObservedEffect()
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static string ConnectorRequestedEffect(TrainingExecutionRequest request)
    {
        var arrival = request.ExpectedArrivalTileX.HasValue && request.ExpectedArrivalTileY.HasValue
            ? ";arrival_tile=" + request.ExpectedArrivalTileX.Value + "," + request.ExpectedArrivalTileY.Value
            : string.Empty;
        return "connector.target_location=" + request.ExpectedTargetLocation + arrival;
    }

    private static string ConnectorObservedEffect()
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static bool IsActionConnectorKind(string kind)
    {
        return string.Equals(kind, "action_warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "locked_door_warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "building_door", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStepOntoConnectorKind(string kind)
    {
        return string.Equals(kind, "warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "touch_action_warp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConnectorActionTypeWhitelisted(string actionType)
    {
        return string.Equals(actionType, "Warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionType, "LockedDoorWarp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidateConnectorTarget(TrainingExecutionRequest request, Point connectorTile)
    {
        if (!string.Equals(request.OptionId, "executor.traverse_connector", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(request.ConnectorKind, "building_door", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? targetLocation = null;
        int? targetX = null;
        int? targetY = null;
        if (string.Equals(request.ConnectorKind, "warp", StringComparison.OrdinalIgnoreCase))
        {
            var warp = Game1.currentLocation.warps.FirstOrDefault(candidate => candidate.X == connectorTile.X && candidate.Y == connectorTile.Y);
            if (warp is null)
            {
                return "connector_warp_source_missing";
            }

            targetLocation = warp.TargetName;
            targetX = warp.TargetX;
            targetY = warp.TargetY;
        }
        else
        {
            var sourceProperty = string.Equals(request.ConnectorKind, "touch_action_warp", StringComparison.OrdinalIgnoreCase)
                ? "TouchAction"
                : "Action";
            var layer = string.Equals(sourceProperty, "TouchAction", StringComparison.Ordinal) ? "Back" : "Buildings";
            var rawAction = Game1.currentLocation.doesTileHaveProperty(connectorTile.X, connectorTile.Y, sourceProperty, layer);
            var parts = rawAction?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (parts.Length < 4)
            {
                return "connector_action_source_missing_or_unparseable";
            }

            var touchAction = string.Equals(sourceProperty, "TouchAction", StringComparison.Ordinal);
            var expectedBranch = string.Equals(request.ConnectorKind, "locked_door_warp", StringComparison.OrdinalIgnoreCase)
                ? "LockedDoorWarp"
                : "Warp";
            if (!string.Equals(parts[0], expectedBranch, StringComparison.OrdinalIgnoreCase))
            {
                return "connector_action_type_mismatch";
            }

            targetLocation = touchAction ? parts[1] : parts[3];
            targetX = ParseIntPart(parts, touchAction ? 2 : 1);
            targetY = ParseIntPart(parts, touchAction ? 3 : 2);
        }

        if (!string.Equals(targetLocation, request.ExpectedTargetLocation, StringComparison.OrdinalIgnoreCase))
        {
            return "connector_source_target_location_mismatch";
        }

        if (request.ExpectedArrivalTileX.HasValue && request.ExpectedArrivalTileY.HasValue &&
            (targetX != request.ExpectedArrivalTileX || targetY != request.ExpectedArrivalTileY))
        {
            return "connector_source_arrival_tile_mismatch";
        }

        return null;
    }

    private bool ValidateBuildingDoorConnector(ActiveTileMove move, Point actionTile)
    {
        var location = Game1.currentLocation;
        var building = location.buildings
            .FirstOrDefault(b =>
                b.humanDoor.X >= 0 && b.humanDoor.Y >= 0 &&
                b.tileX.Value + b.humanDoor.X == actionTile.X &&
                b.tileY.Value + b.humanDoor.Y == actionTile.Y);

        if (building is null)
        {
            CompleteBlockedMove(move, "building_door_no_building_at_action_tile");
            return false;
        }

        if (building.daysOfConstructionLeft.Value > 0)
        {
            CompleteBlockedMove(move, "building_under_construction");
            return false;
        }

        var indoors = building.GetIndoors();
        if (indoors is null)
        {
            CompleteBlockedMove(move, "building_door_no_indoor_location");
            return false;
        }

        if (indoors.warps.Count == 0)
        {
            CompleteBlockedMove(move, "building_door_no_indoor_warps");
            return false;
        }

        var expectedLocation = move.Pending.Request.ExpectedTargetLocation;
        if (!string.Equals(indoors.NameOrUniqueName, expectedLocation, StringComparison.OrdinalIgnoreCase))
        {
            CompleteBlockedMove(move, "building_door_target_location_mismatch:expected=" + expectedLocation + ";actual=" + indoors.NameOrUniqueName);
            return false;
        }

        var playerTile = Game1.player.TilePoint;
        var expectedStandTile = new Point(actionTile.X, actionTile.Y + 1);
        if (Game1.player.TilePoint != expectedStandTile)
        {
            CompleteBlockedMove(move, "building_door_player_not_on_stand_tile:expected=" + expectedStandTile.X + "," + expectedStandTile.Y + ";actual=" + playerTile.X + "," + playerTile.Y);
            return false;
        }

        return true;
    }

    private static Point? FindConnectorActionStandTile(GameLocation location, Point startTile, Point actionTile)
    {
        return Neighbors(actionTile)
            .Where(tile => IsTileTraversableForPlan(location, tile, avoidSoftObstacles: true))
            .OrderBy(tile => Math.Abs(startTile.X - tile.X) + Math.Abs(startTile.Y - tile.Y))
            .Cast<Point?>()
            .FirstOrDefault();
    }

    private static bool TryResolveBoundaryWarpStandTile(GameLocation location, Point warpTile, out Point standTile, out int direction)
    {
        var dimensions = MapDimensions(location);
        var width = dimensions.X;
        var height = dimensions.Y;
        if (width <= 0 || height <= 0)
        {
            standTile = Point.Zero;
            direction = 0;
            return false;
        }

        if (warpTile.X < 0 && warpTile.Y >= 0 && warpTile.Y < height)
        {
            standTile = new Point(0, warpTile.Y);
            direction = 3;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        if (warpTile.X >= width && warpTile.Y >= 0 && warpTile.Y < height)
        {
            standTile = new Point(width - 1, warpTile.Y);
            direction = 1;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        if (warpTile.Y < 0 && warpTile.X >= 0 && warpTile.X < width)
        {
            standTile = new Point(warpTile.X, 0);
            direction = 0;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        if (warpTile.Y >= height && warpTile.X >= 0 && warpTile.X < width)
        {
            standTile = new Point(warpTile.X, height - 1);
            direction = 2;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        standTile = Point.Zero;
        direction = 0;
        return false;
    }

    private static Point MapDimensions(GameLocation location)
    {
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        if (layers.Length == 0)
        {
            return Point.Zero;
        }

        return new Point(layers.Max(layer => layer.LayerWidth), layers.Max(layer => layer.LayerHeight));
    }

    private static TrainingExecutionResult BlockedWithPrimitive(TrainingExecutionRequest request, string primitiveKind, string requestedEffect, string observedEffect, params string[] reasons)
    {
        var result = Blocked(request, reasons);
        result.FeedbackAvailable = true;
        result.PrimitiveKind = primitiveKind;
        result.PrimitiveVerificationStatus = "blocked";
        result.PrimitiveVerificationReasons = reasons;
        result.RequestedEffect = requestedEffect;
        result.ObservedEffect = observedEffect;
        return result;
    }

    private static Point ResolveTargetTile(TrainingExecutionRequest request, Point startTile)
    {
        if (request.TargetTileX.HasValue && request.TargetTileY.HasValue)
        {
            return new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        }

        var steps = Math.Clamp(request.MaxCrops, 1, 8);
        return new Point(startTile.X + steps, startTile.Y);
    }

    private static List<Point>? TryBuildTilePath(GameLocation location, Point startTile, Point targetTile, int maxTiles, out string blockReason, bool avoidSoftObstacles = false, bool allowRemovableObstacles = true)
    {
        blockReason = string.Empty;
        if (IsTileHardBlocked(location, targetTile))
        {
            blockReason = MovementHardBlockReason(location, targetTile);
            return null;
        }

        var costs = new Dictionary<string, int>(StringComparer.Ordinal) { [TileKey(startTile)] = 0 };
        var steps = new Dictionary<string, int>(StringComparer.Ordinal) { [TileKey(startTile)] = 0 };
        var previous = new Dictionary<string, Point>(StringComparer.Ordinal);
        var queue = new PriorityQueue<Point, int>();
        queue.Enqueue(startTile, 0);

        while (queue.Count > 0)
        {
            queue.TryDequeue(out var current, out var dequeuedCost);
            var currentKey = TileKey(current);
            if (!costs.TryGetValue(currentKey, out var currentCost) || dequeuedCost != currentCost)
            {
                continue;
            }
            if (current == targetTile)
            {
                return ReconstructPath(startTile, targetTile, previous);
            }

            foreach (var next in Neighbors(current))
            {
                var key = TileKey(next);
                if (!IsTileTraversableForPlan(location, next, avoidSoftObstacles, allowRemovableObstacles))
                {
                    continue;
                }

                var nextSteps = steps[currentKey] + 1;
                if (nextSteps > maxTiles)
                {
                    continue;
                }
                var nextCost = currentCost + MovementTraversalCost(location, next);
                if (costs.TryGetValue(key, out var knownCost) && knownCost <= nextCost)
                {
                    continue;
                }

                costs[key] = nextCost;
                steps[key] = nextSteps;
                previous[key] = current;
                queue.Enqueue(next, nextCost);
            }
        }

        blockReason = "movement_no_collision_safe_path";
        return null;
    }

    private static int MovementTraversalCost(GameLocation location, Point tile)
    {
        if (IsTileWalkable(location, tile))
        {
            return 32;
        }
        if (IsTileOccupiedByCharacter(location, tile))
        {
            return 192;
        }

        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            if (obj.IsBreakableStone() && FindTool<Pickaxe>() is Pickaxe pickaxe)
            {
                var damage = Math.Max(1, pickaxe.UpgradeLevel + 1) + Math.Max(0, pickaxe.additionalPower.Value);
                var swings = Math.Max(1, (int)Math.Ceiling(obj.MinutesUntilReady / (double)damage));
                return 32 + swings * ClearanceTickCost(pickaxe);
            }
            if (obj is BreakableContainer)
            {
                return 32 + 3 * 30;
            }
            if (obj.IsWeeds())
            {
                return 32 + 30;
            }
            if (obj.IsTwig())
            {
                return 32 + 60;
            }
        }

        return 32 + 8 * 60;
    }

    private static List<Point> ReconstructPath(Point startTile, Point targetTile, Dictionary<string, Point> previous)
    {
        var path = new List<Point>();
        var current = targetTile;
        while (current != startTile)
        {
            path.Add(current);
            current = previous[TileKey(current)];
        }

        path.Reverse();
        return path;
    }

    private static bool IsTileOnMap(GameLocation location, Point tile)
    {
        return location.isTileOnMap(new Vector2(tile.X, tile.Y));
    }

    private static bool IsTileWalkable(GameLocation location, Point tile)
    {
        var rectangle = new XnaRectangle(tile.X * Game1.tileSize + 1, tile.Y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2);
        return !location.isCollidingPosition(rectangle, Game1.viewport, isFarmer: true, 0, glider: false, Game1.player, pathfinding: true);
    }

    private static IEnumerable<Point> Neighbors(Point tile)
    {
        yield return new Point(tile.X + 1, tile.Y);
        yield return new Point(tile.X - 1, tile.Y);
        yield return new Point(tile.X, tile.Y + 1);
        yield return new Point(tile.X, tile.Y - 1);
    }

    private static int ManhattanDistance(Point left, Point right)
    {
        return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
    }

    private static bool AreAdjacent(Point left, Point right)
    {
        return ManhattanDistance(left, right) == 1;
    }

    private static int DirectionTo(Point from, Point to)
    {
        if (to.Y < from.Y)
        {
            return 0;
        }

        if (to.X > from.X)
        {
            return 1;
        }

        if (to.Y > from.Y)
        {
            return 2;
        }

        return 3;
    }

    private static string TileKey(Point tile)
    {
        return tile.X + "," + tile.Y;
    }

    private void StartMoving(int direction)
    {
        Game1.player.forceCanMove();
        Game1.player.faceDirection(direction);
        executorMovementDirection = direction;
    }

    private void StopAllMovement()
    {
        executorMovementDirection = null;
        ApplyExecutorMovementInput(out _);
    }

    private static void MovePlayerForTick()
    {
        // Movement is consumed by the game's native update on the next tick.
    }

    private bool ApplyExecutorMovementInput(out string reason)
    {
        reason = string.Empty;
        var buttons = new[] { SButton.W, SButton.D, SButton.S, SButton.A };
        for (var direction = 0; direction < buttons.Length; direction++)
        {
            if (!TryApplySmapiButtonOverride(buttons[direction], executorMovementDirection == direction, out reason))
            {
                return false;
            }
        }

        return true;
    }

    private void StartMovingIfNeeded(ActiveTileMove move, int direction)
    {
        if (move.CurrentDirection == direction)
        {
            return;
        }

        StartMoving(direction);
        move.CurrentDirection = direction;
    }
}
