using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveFieldOfficeDonation
    {
        public ActiveFieldOfficeDonation(
            PendingExecution pending,
            IslandFieldOffice office,
            Point actionTile,
            Point standTile,
            List<Point> path)
        {
            Pending = pending;
            Office = office;
            ActionTile = actionTile;
            StandTile = standTile;
            Path = path;
            LastTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public IslandFieldOffice Office { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public Point LastTile { get; set; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StuckTicks { get; set; }
        public int Cooldown { get; set; }
        public int QuestionWaitTicks { get; set; }
        public int DialogueAdvanceTicks { get; set; }
        public bool DeskIssued { get; set; }
        public bool DonateChosen { get; set; }
        public bool InventoryClicked { get; set; }
        public bool PieceClicked { get; set; }
        public bool RemainderReturned { get; set; }
        public bool ExitClicked { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }
}
