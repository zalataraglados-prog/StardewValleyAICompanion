using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveFieldOfficeSurvey
    {
        public ActiveFieldOfficeSurvey(
            PendingExecution pending,
            IslandFieldOffice office,
            Point actionTile,
            Point standTile,
            List<Point> path,
            bool intentionallyWrong)
        {
            Pending = pending;
            Office = office;
            ActionTile = actionTile;
            StandTile = standTile;
            Path = path;
            IntentionallyWrong = intentionallyWrong;
            LastTile = Game1.player.TilePoint;
            DebrisBefore = office.debris.ToHashSet();
        }

        public PendingExecution Pending { get; }
        public IslandFieldOffice Office { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public bool IntentionallyWrong { get; }
        public HashSet<Debris> DebrisBefore { get; }
        public Point LastTile { get; set; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StuckTicks { get; set; }
        public int Cooldown { get; set; }
        public int QuestionWaitTicks { get; set; }
        public int ResultDialogueTicks { get; set; }
        public int SettlementWaitTicks { get; set; }
        public bool ActionIssued { get; set; }
        public bool PromptAnswered { get; set; }
        public bool NumericAnswerIssued { get; set; }
        public int WalnutDebrisSpawnObservedCount { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }
}
