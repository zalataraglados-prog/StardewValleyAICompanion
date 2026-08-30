using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum SlotsStage
    {
        Move,
        WaitStart,
        WaitSettlement,
        WaitDone
    }

    private sealed class ActiveSlots : INativeObjectInteractionMovement
    {
        public ActiveSlots(
            PendingExecution pending,
            GameLocation location,
            Point interaction,
            Point stand,
            List<Point> path,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Interaction = interaction;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 6000;
        public string StartedAt { get; }
        public SlotsStage Stage { get; set; }
        public Slots? Game { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public float[] ResultIcons { get; set; } = Array.Empty<float>();
        public float ObservedPayoutMultiplier { get; set; }
        public int ObservedCoinDelta { get; set; }
        public bool NativeSpinStarted { get; set; }
        public bool SettlementVerified { get; set; }
    }
}
