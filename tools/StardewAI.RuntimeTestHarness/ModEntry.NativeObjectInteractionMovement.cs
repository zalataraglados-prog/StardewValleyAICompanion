using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum NativeObjectMovementStatus
    {
        Moving,
        Ready,
        Failed
    }

    private interface INativeObjectInteractionMovement
    {
        GameLocation Location { get; }
        Point Stand { get; }
        List<Point> Path { get; }
        int MaxMovementTiles { get; }
        int MaxTicks { get; }
        int ElapsedTicks { get; set; }
        int PathIndex { get; set; }
        int StuckTicks { get; set; }
        int MovementTiles { get; set; }
        Vector2 LastPosition { get; set; }
        Point LastObservedTile { get; set; }
    }

    private NativeObjectMovementStatus AdvanceNativeObjectInteractionMovement(
        INativeObjectInteractionMovement active,
        string reasonPrefix,
        out string failureReason)
    {
        failureReason = string.Empty;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            failureReason = reasonPrefix + "_location_changed";
            return NativeObjectMovementStatus.Failed;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            failureReason = reasonPrefix + "_timeout";
            return NativeObjectMovementStatus.Failed;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
        }
        if (active.MovementTiles > active.MaxMovementTiles)
        {
            failureReason = reasonPrefix + "_movement_budget_exceeded";
            return NativeObjectMovementStatus.Failed;
        }
        if (playerTile == active.Stand)
        {
            StopAllMovement();
            return NativeObjectMovementStatus.Ready;
        }
        if (active.PathIndex >= active.Path.Count)
        {
            failureReason = reasonPrefix + "_path_exhausted";
            return NativeObjectMovementStatus.Failed;
        }

        var next = active.Path[active.PathIndex];
        if (playerTile == next)
        {
            active.PathIndex++;
            return NativeObjectMovementStatus.Moving;
        }
        if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
        {
            failureReason = reasonPrefix + "_dynamic_path_blocked";
            return NativeObjectMovementStatus.Failed;
        }
        var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
        active.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(playerTile, next));
        MovePlayerForTick();
        if (Game1.player.TilePoint == next)
            active.PathIndex++;
        active.StuckTicks = moved ? 0 : active.StuckTicks + 1;
        if (active.StuckTicks > 45)
        {
            failureReason = reasonPrefix + "_movement_stuck";
            return NativeObjectMovementStatus.Failed;
        }
        return NativeObjectMovementStatus.Moving;
    }
}
