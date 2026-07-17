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
    }}
