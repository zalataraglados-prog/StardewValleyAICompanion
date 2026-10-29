using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool TryReleaseOccupiedPathStart(
        ActiveTileMove move,
        Point pathStartTile)
    {
        var location = Game1.currentLocation;
        if (!IsTileOccupiedByCharacter(location, pathStartTile))
        {
            return false;
        }

        StopAllMovement("occupied_path_start_replan");
        move.CurrentDirection = null;
        if (!TryBuildOccupiedStartEgressPath(
                location,
                pathStartTile,
                move.TargetTile,
                Math.Max(1, 512 - move.PathIndex),
                out var path))
        {
            return false;
        }

        // Native push-through can leave the farmer sharing a tile with a pet or
        // NPC. First move toward the free adjacent tile with the greatest
        // separation from the overlapping character, then follow the ordinary
        // collision-safe path using the same native directional input lease.
        move.Path = path;
        move.PathIndex = 0;
        move.NeedsPathStartCentering = false;
        move.StuckTicks = 0;
        move.SoftObstacleTicks = 0;
        move.LastPosition = Game1.player.Position;
        move.Pending.MovementExtraTicks += 30;
        return true;
    }

    private static bool TryBuildOccupiedStartEgressPath(
        GameLocation location,
        Point startTile,
        Point targetTile,
        int maxTiles,
        out List<Point> path)
    {
        path = new List<Point>();
        var startRectangle = TileRectangle(startTile);
        var overlappingBounds = location.characters
            .Select(character => character.GetBoundingBox())
            .Where(bounds => bounds.Intersects(startRectangle))
            .ToArray();
        if (overlappingBounds.Length == 0)
        {
            return false;
        }

        List<Point>? bestPath = null;
        long bestClearance = long.MinValue;
        foreach (var egressTile in Neighbors(startTile))
        {
            if (!IsTileOnMap(location, egressTile) ||
                !IsTileWalkable(location, egressTile) ||
                IsTileOccupiedByCharacter(location, egressTile))
            {
                continue;
            }

            var tail = TryBuildTilePath(
                location,
                egressTile,
                targetTile,
                Math.Max(0, maxTiles - 1),
                out _,
                avoidSoftObstacles: true,
                allowRemovableObstacles: false);
            if (tail is null)
            {
                continue;
            }

            var clearance = OccupiedStartClearanceSquared(
                egressTile,
                overlappingBounds);
            var candidatePath = new List<Point>(tail.Count + 1) { egressTile };
            candidatePath.AddRange(tail);
            if (bestPath is not null &&
                (clearance < bestClearance ||
                 clearance == bestClearance && candidatePath.Count >= bestPath.Count))
            {
                continue;
            }

            bestClearance = clearance;
            bestPath = candidatePath;
        }

        if (bestPath is null)
        {
            return false;
        }

        path = bestPath;
        return true;
    }

    private static long OccupiedStartClearanceSquared(
        Point tile,
        IReadOnlyList<Rectangle> overlappingBounds)
    {
        var center = TileRectangle(tile).Center;
        var minimum = long.MaxValue;
        foreach (var bounds in overlappingBounds)
        {
            var deltaX = (long)center.X - bounds.Center.X;
            var deltaY = (long)center.Y - bounds.Center.Y;
            minimum = Math.Min(minimum, deltaX * deltaX + deltaY * deltaY);
        }

        return minimum;
    }

    private void TickTileMoveSoftObstacle(
        ActiveTileMove move,
        Point currentTile,
        Point nextTile)
    {
        move.SoftObstacleTicks++;
        if (move.SoftObstacleTicks == 1 || move.SoftObstacleTicks % 30 == 0)
        {
            StopAllMovement("soft_obstacle_replan");
            move.CurrentDirection = null;
            if (ReplanTileMove(move, avoidSoftObstacles: true))
            {
                return;
            }
        }

        if (move.SoftObstacleTicks > 180)
        {
            CompleteBlockedMove(move, "movement_soft_obstacle_timeout");
            return;
        }

        if (!AreAdjacent(currentTile, nextTile))
        {
            StopAllMovement("soft_obstacle_path_desynchronized");
            move.CurrentDirection = null;
            return;
        }

        // Native movement lets pets and NPCs yield when no alternate route exists.
        var direction = DirectionTo(currentTile, nextTile);
        StartMovingIfNeeded(move, direction);
        MovePlayerForTick();
        if (Game1.player.TilePoint != nextTile)
        {
            return;
        }

        move.PathIndex++;
        move.SoftObstacleTicks = 0;
        move.StuckTicks = 0;
        move.LastPosition = Game1.player.Position;
    }
}
