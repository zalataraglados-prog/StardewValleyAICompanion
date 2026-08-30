using Microsoft.Xna.Framework;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum CalicoJackStage
    {
        Move,
        WaitDialogue,
        WaitMinigame,
        WaitInitialDeal,
        PlayerTurn,
        DealerTurn,
        WaitResult,
        WaitQuit
    }

    private sealed class ActiveCalicoJack : INativeObjectInteractionMovement
    {
        public ActiveCalicoJack(
            PendingExecution pending,
            GameLocation location,
            Point interaction,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            int[] expectedPlayerCards,
            int[] expectedDealerCards)
        {
            Pending = pending;
            Location = location;
            Interaction = interaction;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            ExpectedInitialPlayerCards = expectedPlayerCards;
            ExpectedInitialDealerCards = expectedDealerCards;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            ExpectedCurrentBet = pending.Request.CalicoBet!.Value;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 12000;
        public string StartedAt { get; }
        public int[] ExpectedInitialPlayerCards { get; }
        public int[] ExpectedInitialDealerCards { get; }
        public CalicoJackStage Stage { get; set; }
        public CalicoJack? Game { get; set; }
        public CalicoJackRandomCursor? RandomCursor { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int LastPlayerCardCount { get; set; }
        public int LastDealerCardCount { get; set; }
        public int? PendingExpectedPlayerCard { get; set; }
        public int ExpectedCurrentBet { get; set; }
        public int NativeHitClicks { get; set; }
        public int NativeStandClicks { get; set; }
        public int NativeDealerDrawsVerified { get; set; }
        public int DecisionCount { get; set; }
        public bool FirstDecisionVerified { get; set; }
        public bool SettlementVerified { get; set; }
        public string ObservedOutcome { get; set; } = string.Empty;
    }
}
