using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveCommunityCenterRewardClaim
    {
        public ActiveCommunityCenterRewardClaim(
            PendingExecution pending,
            CommunityCenter communityCenter,
            Point interactionTile,
            Point standTile,
            List<Point> path,
            int maxMovementTiles,
            int bundleId,
            int areaId,
            string claimMode,
            string qualifiedItemId,
            int inventoryItemTotalBefore)
        {
            Pending = pending;
            CommunityCenter = communityCenter;
            InteractionTile = interactionTile;
            StandTile = standTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            BundleId = bundleId;
            AreaId = areaId;
            ClaimMode = claimMode;
            QualifiedItemId = qualifiedItemId;
            InventoryItemTotalBefore = inventoryItemTotalBefore;
            LastObservedTile = StardewValley.Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public CommunityCenter CommunityCenter { get; }
        public Point InteractionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int BundleId { get; }
        public int AreaId { get; }
        public string ClaimMode { get; }
        public string QualifiedItemId { get; }
        public int InventoryItemTotalBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool EntryIssued { get; set; }
        public bool PresentButtonClicked { get; set; }
        public bool RewardClicked { get; set; }
        public bool RewardMenuExitIssued { get; set; }
        public bool ParentExitIssued { get; set; }
        public int MenuWaitTicks { get; set; }
        public int SettlementTicks { get; set; }
    }
}
