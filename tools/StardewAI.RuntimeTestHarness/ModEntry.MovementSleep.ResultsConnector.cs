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

}
