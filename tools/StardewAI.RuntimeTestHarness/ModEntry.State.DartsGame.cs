using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum DartsGameStage
    {
        Move,
        WaitDialogue,
        WaitMinigame,
        Play,
        WaitResultDialogue,
        Verify
    }

    private sealed class ActiveDartsGame : INativeObjectInteractionMovement
    {
        public ActiveDartsGame(
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
        public int MaxTicks => 18000;
        public string StartedAt { get; }
        public DartsGameStage Stage { get; set; }
        public Darts? Game { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int CompletedThrows { get; set; }
        public int FinalPoints { get; set; } = -1;
        public bool PerfectVictory { get; set; }
        public bool InputPressed { get; set; }
        public int AimTargetThrowIndex { get; set; } = -1;
        public int AimSettlingTicks { get; set; }
        public bool NativeCheckActionHandled { get; set; }
        public bool NativeYesObserved { get; set; }
        public int ResultDialogueClicks { get; set; }
        public int StableCompletionTicks { get; set; }
        public List<int> HitScores { get; } = new();
        public List<string> ShotTrace { get; } = new();
    }
}
