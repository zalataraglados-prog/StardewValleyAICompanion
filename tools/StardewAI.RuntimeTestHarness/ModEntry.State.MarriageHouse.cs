using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveFarmhouseUpgrade
    {
        public ActiveFarmhouseUpgrade(PendingExecution pending, GameLocation house, Point actionTile, Point standTile, List<Point> path, int maxMovementTiles)
        {
            Pending = pending;
            House = house;
            ActionTile = actionTile;
            StandTile = standTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation House { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool OpenIssued { get; set; }
        public bool UpgradeResponseChosen { get; set; }
        public bool PurchaseIssued { get; set; }
        public int DialogueCooldown { get; set; }
        public int SettlementTicks { get; set; }
    }
}
