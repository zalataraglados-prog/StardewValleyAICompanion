using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveJojaDevelopment
    {
        public ActiveJojaDevelopment(PendingExecution pending, JojaMart mart, Point actionTile, Point standTile, List<Point> path, int maxMovementTiles)
        {
            Pending = pending;
            Mart = mart;
            ActionTile = actionTile;
            StandTile = standTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            GreetingRequired = pending.Request.ExpectedGreetingBefore == false;
            LastObservedTile = StardewValley.Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public JojaMart Mart { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public bool GreetingRequired { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool OpenIssued { get; set; }
        public bool GreetingCompleted { get; set; }
        public bool OfferResponseChosen { get; set; }
        public bool PurchaseIssued { get; set; }
        public int DialogueCooldown { get; set; }
        public int SettlementTicks { get; set; }
    }
}
