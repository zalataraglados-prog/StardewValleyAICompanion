using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum CraneGameStage
    {
        Move,
        WaitDialogue,
        WaitMinigame,
        Play,
        WaitRewardMenu,
        TransferRewards,
        Verify
    }

    private sealed class ActiveCraneGame : INativeObjectInteractionMovement
    {
        public ActiveCraneGame(
            PendingExecution pending,
            GameLocation location,
            Point interaction,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            Dictionary<string, int> inventoryBefore)
        {
            Pending = pending;
            Location = location;
            Interaction = interaction;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            InventoryBefore = inventoryBefore;
            MoneyBefore = Game1.player.Money;
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
        public int MaxTicks => 12000;
        public string StartedAt { get; }
        public Dictionary<string, int> InventoryBefore { get; }
        public Dictionary<string, int> ExpectedRewards { get; } = new(StringComparer.Ordinal);
        public HashSet<CraneGame.Prize> AttemptedPrizes { get; } = new();
        public int MoneyBefore { get; }
        public CraneGameStage Stage { get; set; }
        public CraneGame? Game { get; set; }
        public CraneGame.GameLogic? Logic { get; set; }
        public CraneGame.Claw? Claw { get; set; }
        public CraneGame.Prize? Target { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int AttemptsStarted { get; set; }
        public int RewardsTransferred { get; set; }
        public bool NativeCheckActionHandled { get; set; }
        public bool NativeFeeObserved { get; set; }
    }
}
