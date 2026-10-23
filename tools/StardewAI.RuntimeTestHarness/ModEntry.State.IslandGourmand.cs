using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveGourmandRequest
    {
        public ActiveGourmandRequest(
            PendingExecution pending,
            IslandFarmCave cave,
            Point actionTile,
            Point standTile,
            List<Point> path)
        {
            Pending = pending;
            Cave = cave;
            ActionTile = actionTile;
            StandTile = standTile;
            Path = path;
            LastTile = Game1.player.TilePoint;
            DebrisBefore = cave.debris.ToHashSet();
        }

        public PendingExecution Pending { get; }
        public IslandFarmCave Cave { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public HashSet<Debris> DebrisBefore { get; }
        public HashSet<Debris> ObservedWalnutDebris { get; } = new();
        public bool Completing => Pending.Request.GourmandInteractionStage == "complete_request";
        public Point LastTile { get; set; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StuckTicks { get; set; }
        public int Cooldown { get; set; }
        public int DialogueTicks { get; set; }
        public int SettlementWaitTicks { get; set; }
        public bool ActionIssued { get; set; }
        public bool YesAnswered { get; set; }
        public bool SawNativeEvent { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }
}
