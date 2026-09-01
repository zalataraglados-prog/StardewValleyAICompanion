using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ExecutorPathCursor
    {
        public ExecutorPathCursor()
        {
            LastPosition = Game1.player.Position;
        }

        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int? CurrentDirection { get; set; }
    }

    private bool TryAdvanceExecutorPath(
        GameLocation location,
        IReadOnlyList<Point> path,
        ExecutorPathCursor cursor,
        out string reason,
        bool waitForSoftObstacle = false)
    {
        while (cursor.PathIndex < path.Count &&
            Game1.player.TilePoint == path[cursor.PathIndex])
        {
            cursor.PathIndex++;
            cursor.StuckTicks = 0;
        }

        if (cursor.PathIndex >= path.Count)
        {
            reason = "path_exhausted_before_target";
            return false;
        }

        var next = path[cursor.PathIndex];
        var occupiedByCharacter = IsTileOccupiedByCharacter(location, next);
        if (occupiedByCharacter && waitForSoftObstacle)
        {
            cursor.LastPosition = Game1.player.Position;
            var obstacleDirection = DirectionTo(Game1.player.TilePoint, next);
            StartMoving(obstacleDirection);
            cursor.CurrentDirection = obstacleDirection;
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                cursor.PathIndex++;
                cursor.StuckTicks = 0;
                reason = string.Empty;
                return true;
            }

            cursor.StuckTicks++;
            if (cursor.StuckTicks > 180)
            {
                reason = "soft_obstacle_timeout";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        if (!IsTileWalkable(location, next) || occupiedByCharacter)
        {
            reason = "dynamic_path_blocked";
            return false;
        }

        var movedSinceLastTick =
            Vector2.DistanceSquared(
                cursor.LastPosition,
                Game1.player.Position) >= 0.01f;
        var direction = DirectionTo(Game1.player.TilePoint, next);
        if (cursor.CurrentDirection.HasValue &&
            cursor.CurrentDirection.Value != direction &&
            movedSinceLastTick &&
            !HasReachedTurnCenter(
                Game1.player.TilePoint,
                cursor.CurrentDirection.Value))
        {
            direction = cursor.CurrentDirection.Value;
        }

        cursor.LastPosition = Game1.player.Position;
        StartMoving(direction);
        cursor.CurrentDirection = direction;
        MovePlayerForTick();

        if (Game1.player.TilePoint == next)
        {
            cursor.PathIndex++;
        }

        cursor.StuckTicks = movedSinceLastTick
            ? 0
            : cursor.StuckTicks + 1;
        if (cursor.StuckTicks > 45)
        {
            reason = "movement_stuck";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
