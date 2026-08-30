using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveGeodeProcessing : INativeObjectInteractionMovement
    {
        public ActiveGeodeProcessing(PendingExecution pending, GameLocation location, Point target, Point stand,
            List<Point> path, int maxMovementTiles, List<GeodeAcceptedOutput> accepted)
        {
            Pending = pending; Location = location; Target = target; Stand = stand; Path = path;
            MaxMovementTiles = maxMovementTiles; Accepted = accepted;
            LastPosition = Game1.player.Position; LastObservedTile = Game1.player.TilePoint;
            InventoryBefore = GeodeInventoryCounts();
            MailBefore = Game1.player.mailReceived.ToHashSet(StringComparer.Ordinal);
            MineralFoundBefore = accepted.Select(row => row.QualifiedItemId).Distinct(StringComparer.Ordinal)
                .ToDictionary(qid => qid, GeodeMineralFoundCount, StringComparer.Ordinal);
            ArtifactFoundBefore = accepted.Select(row => row.QualifiedItemId).Distinct(StringComparer.Ordinal)
                .ToDictionary(qid => qid, GeodeArtifactFoundCount, StringComparer.Ordinal);
            StoneGatheredBefore = Game1.stats.StoneGathered; CopperFoundBefore = Game1.stats.CopperFound;
            IronFoundBefore = Game1.stats.IronFound; GoldFoundBefore = Game1.stats.GoldFound;
            IridiumFoundBefore = Game1.stats.IridiumFound;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 3600;
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool CounterActionIssued { get; set; }
        public bool ProcessAnswered { get; set; }
        public bool InventoryClicked { get; set; }
        public bool GeodeSpotClicked { get; set; }
        public bool AnimationStarted { get; set; }
        public bool HeldStackReturned { get; set; }
        public bool CloseClicked { get; set; }
        public List<GeodeAcceptedOutput> Accepted { get; }
        public Dictionary<string, int> InventoryBefore { get; }
        public HashSet<string> MailBefore { get; }
        public Dictionary<string, int> MineralFoundBefore { get; }
        public Dictionary<string, int> ArtifactFoundBefore { get; }
        public uint StoneGatheredBefore { get; }
        public uint CopperFoundBefore { get; }
        public uint IronFoundBefore { get; }
        public uint GoldFoundBefore { get; }
        public uint IridiumFoundBefore { get; }
        public GeodeAcceptedOutput? ActualOutput { get; set; }
    }

    private sealed record GeodeAcceptedOutput(string QualifiedItemId, int Stack, int Quality, string SetFlagOnPickup,
        bool InventoryPersists, string PickupEffectKind, string[] ExpectedMailAdditions);
}
