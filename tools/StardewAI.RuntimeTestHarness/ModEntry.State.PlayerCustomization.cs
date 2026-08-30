using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActivePlayerCustomization : INativeObjectInteractionMovement
    {
        public ActivePlayerCustomization(PendingExecution pending, GameLocation location, Point target, Point stand,
            List<Point> path, int maxMovementTiles)
        {
            Pending = pending; Location = location; Target = target; Stand = stand; Path = path;
            MaxMovementTiles = maxMovementTiles; LastPosition = Game1.player.Position; LastObservedTile = Game1.player.TilePoint;
            MoneyBefore = Game1.player.Money;
            HatBefore = Game1.player.hat.Value?.QualifiedItemId ?? string.Empty;
            ShirtBefore = Game1.player.shirtItem.Value?.QualifiedItemId ?? string.Empty;
            PantsBefore = Game1.player.pantsItem.Value?.QualifiedItemId ?? string.Empty;
            ReturnedBefore = Game1.player.team.returnedDonations.Count;
            BeforeLooseCounts = new[] { HatBefore, ShirtBefore, PantsBefore }.Where(qid => qid.Length > 0)
                .Distinct(StringComparer.Ordinal).ToDictionary(qid => qid,
                    qid => Game1.player.Items.Count(item => item?.QualifiedItemId == qid), StringComparer.Ordinal);
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 7200;
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int StageTicks { get; set; }
        public bool ActionIssued { get; set; }
        public bool DialogueAnswered { get; set; }
        public bool TextEntered { get; set; }
        public bool OkClicked { get; set; }
        public bool EventSeen { get; set; }
        public bool EventSkipClicked { get; set; }
        public int NativeControlInputs { get; set; }
        public int MoneyBefore { get; }
        public string HatBefore { get; }
        public string ShirtBefore { get; }
        public string PantsBefore { get; }
        public int ReturnedBefore { get; }
        public Dictionary<string, int> BeforeLooseCounts { get; }
    }
}
