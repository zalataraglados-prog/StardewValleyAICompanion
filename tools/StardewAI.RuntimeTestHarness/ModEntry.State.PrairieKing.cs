using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum PrairieKingStage
    {
        Move,
        WaitNativeStart,
        EquivalentSession,
        WaitNativeSettlement,
        Verify
    }

    private sealed class ActivePrairieKing : INativeObjectInteractionMovement
    {
        public ActivePrairieKing(
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
            CompletedBefore = Game1.player.stats.Get("completedPrairieKing");
            CompletedWithoutDyingBefore = Game1.player.stats.Get("completedPrairieKingWithoutDying");
            HadSavedProgress = Game1.player.jotpkProgress.Value is not null;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public PrairieKingStage Stage { get; set; }
        public AbigailGame? Game { get; set; }
        public long CompletedBefore { get; }
        public long CompletedWithoutDyingBefore { get; }
        public bool HadSavedProgress { get; }
        public bool NativeEntryObserved { get; set; }
        public bool NativeNewGameObserved { get; set; }
        public bool NativeCompletionTriggerInvoked { get; set; }
        public int EquivalentElapsedTicks { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int MaxTicks => 30000;
        public string StartedAt { get; }
    }
}
