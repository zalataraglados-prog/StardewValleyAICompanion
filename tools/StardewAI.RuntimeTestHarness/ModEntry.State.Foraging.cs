using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveSpawnedObjectPickup
    {
        public ActiveSpawnedObjectPickup(
            PendingExecution pending,
            GameLocation location,
            StardewObject targetObject,
            Point target,
            Point stand,
            List<Point> path,
            string qualifiedItemId,
            int expectedQuantity,
            int expectedQuality,
            int expectedForagingExperience,
            int expectedFarmingExperience,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            TargetObject = targetObject;
            Target = target;
            Stand = stand;
            Path = path;
            QualifiedItemId = qualifiedItemId;
            ExpectedQuantity = expectedQuantity;
            ExpectedQuality = expectedQuality;
            ExpectedForagingExperience = expectedForagingExperience;
            ExpectedFarmingExperience = expectedFarmingExperience;
            MaxMovementTiles = maxMovementTiles;
            ItemCountBefore = CountInventoryItem(qualifiedItemId);
            QualityItemCountBefore = CountInventoryItemAtQuality(qualifiedItemId, expectedQuality);
            InventoryBefore = InventoryStackSignature();
            ForagingExperienceBefore = Game1.player.experiencePoints[Farmer.foragingSkill];
            FarmingExperienceBefore = Game1.player.experiencePoints[Farmer.farmingSkill];
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            RequestedEffect = "current_location.objects[" + target.X + "," + target.Y + "].present=false;qualified_item_id=" + qualifiedItemId + ";quantity=" + expectedQuantity;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject TargetObject { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public string QualifiedItemId { get; }
        public int ExpectedQuantity { get; }
        public int ExpectedQuality { get; }
        public int ExpectedForagingExperience { get; }
        public int ExpectedFarmingExperience { get; }
        public int MaxMovementTiles { get; }
        public int ItemCountBefore { get; }
        public int QualityItemCountBefore { get; }
        public string InventoryBefore { get; }
        public int ForagingExperienceBefore { get; }
        public int FarmingExperienceBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool ActionIssued { get; set; }
    }
}
