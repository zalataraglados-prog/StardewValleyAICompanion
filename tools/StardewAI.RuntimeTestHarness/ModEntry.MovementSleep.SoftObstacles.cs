using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool TryReleaseOccupiedPathStart(
        ActiveTileMove move,
        Point pathStartTile)
    {
        if (!IsTileOccupiedByCharacter(Game1.currentLocation, pathStartTile))
        {
            return false;
        }

        StopAllMovement("occupied_path_start_replan");
        move.CurrentDirection = null;
        if (!ReplanTileMove(move, avoidSoftObstacles: true))
        {
            return false;
        }

        // Native push-through can leave the farmer sharing a tile with a pet or
        // NPC. Centering inside that occupied tile is impossible; the replanned
        // path provides the collision-safe native-input escape instead.
        move.NeedsPathStartCentering = false;
        move.StuckTicks = 0;
        move.SoftObstacleTicks = 0;
        move.LastPosition = Game1.player.Position;
        return true;
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
