using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveToolUpgrade : INativeObjectInteractionMovement
    {
        public ActiveToolUpgrade(
            PendingExecution pending,
            GameLocation location,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            bool claim)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            Claim = claim;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            MoneyBefore = Game1.player.Money;
            SourceCountBefore = CountInventoryItems(
                pending.Request.ToolUpgradeSourceQualifiedItemId);
            TargetCountBefore = CountInventoryItems(
                claim
                    ? pending.Request.ToolUpgradePendingQualifiedItemId
                    : pending.Request.ToolUpgradeTargetQualifiedItemId);
            TradeCountBefore = CountInventoryItems(
                pending.Request.ToolUpgradeTradeQualifiedItemId);
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 2400;
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool Claim { get; }
        public bool CounterActionIssued { get; set; }
        public bool UpgradeAnswered { get; set; }
        public bool PurchaseClicked { get; set; }
        public bool ReceiptObserved { get; set; }
        public int MoneyBefore { get; }
        public int SourceCountBefore { get; }
        public int TargetCountBefore { get; }
        public int TradeCountBefore { get; }
    }
}
