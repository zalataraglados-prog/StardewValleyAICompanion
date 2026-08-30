using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum MultiplayerWalletStage
    {
        Move,
        WaitInitialDialogue,
        WaitSecondaryDialogue,
        WaitRecipientDialogue,
        EnterTransferAmount,
        WaitReceipt
    }

    private sealed class ActiveMultiplayerWallet : INativeObjectInteractionMovement
    {
        public ActiveMultiplayerWallet(
            PendingExecution pending,
            ManorHouse manor,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles)
        {
            Pending = pending;
            Manor = manor;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public ManorHouse Manor { get; }
        public GameLocation Location => Manor;
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 3600;
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public MultiplayerWalletStage Stage { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int DigitIndex { get; set; } = -1;
        public bool NativeActionHandled { get; set; }
    }
}
