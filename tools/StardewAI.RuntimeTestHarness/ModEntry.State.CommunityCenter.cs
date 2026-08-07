using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveCommunityCenterDonation
    {
        public ActiveCommunityCenterDonation(
            PendingExecution pending,
            CommunityCenter communityCenter,
            Point noteTile,
            Point interactionTile,
            Point standTile,
            List<Point> path,
            int maxMovementTiles,
            int inventorySlotIndex,
            string qualifiedItemId,
            int stackBefore,
            int bundleId,
            int areaId,
            int ingredientIndex,
            int completedCountBefore,
            int inventoryItemTotalBefore,
            bool rewardAvailableBefore,
            bool areaCompleteBefore,
            bool areaMailPendingBefore,
            bool bulletinThankYouPendingBefore,
            int completeBundleCountBefore,
            bool allAreasCompleteBefore)
        {
            Pending = pending;
            CommunityCenter = communityCenter;
            NoteTile = noteTile;
            InteractionTile = interactionTile;
            StandTile = standTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            InventorySlotIndex = inventorySlotIndex;
            QualifiedItemId = qualifiedItemId;
            StackBefore = stackBefore;
            BundleId = bundleId;
            AreaId = areaId;
            IngredientIndex = ingredientIndex;
            CompletedCountBefore = completedCountBefore;
            InventoryItemTotalBefore = inventoryItemTotalBefore;
            RewardAvailableBefore = rewardAvailableBefore;
            AreaCompleteBefore = areaCompleteBefore;
            AreaMailPendingBefore = areaMailPendingBefore;
            BulletinThankYouPendingBefore = bulletinThankYouPendingBefore;
            CompleteBundleCountBefore = completeBundleCountBefore;
            AllAreasCompleteBefore = allAreasCompleteBefore;
            LastObservedTile = StardewValley.Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public CommunityCenter CommunityCenter { get; }
        public Point NoteTile { get; }
        public Point InteractionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int InventorySlotIndex { get; }
        public string QualifiedItemId { get; }
        public int StackBefore { get; }
        public int BundleId { get; }
        public int AreaId { get; }
        public int IngredientIndex { get; }
        public int CompletedCountBefore { get; }
        public int InventoryItemTotalBefore { get; }
        public bool RewardAvailableBefore { get; }
        public bool AreaCompleteBefore { get; }
        public bool AreaMailPendingBefore { get; }
        public bool BulletinThankYouPendingBefore { get; }
        public int CompleteBundleCountBefore { get; }
        public bool AllAreasCompleteBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool OpenIssued { get; set; }
        public int OpenWaitTicks { get; set; }
        public bool BundleClickIssued { get; set; }
        public bool BundlePageObservedOpen { get; set; }
        public bool InventoryClickIssued { get; set; }
        public bool IngredientClickIssued { get; set; }
        public bool RemainderReturnClickIssued { get; set; }
        public bool BackClickIssued { get; set; }
        public bool ExitIssued { get; set; }
        public int SettlementTicks { get; set; }
    }
}
