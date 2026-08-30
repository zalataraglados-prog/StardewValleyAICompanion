using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveBobberSelection : INativeObjectInteractionMovement
    {
        public ActiveBobberSelection(PendingExecution pending, GameLocation location, Point target, Point stand,
            List<Point> path, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 3600;
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int StageTicks { get; set; }
        public bool ActionIssued { get; set; }
        public bool IconClicked { get; set; }
        public bool CloseClicked { get; set; }
        public bool NativeHandled { get; set; }
    }
}
