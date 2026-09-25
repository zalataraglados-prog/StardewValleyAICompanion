using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveCommunityCenterVaultPayment
    {
        public ActiveCommunityCenterVaultPayment(
            PendingExecution pending,
            CommunityCenter communityCenter,
            Point interactionTile,
            Point standTile,
            List<Point> path,
            int maxMovementTiles,
            int bundleId,
            int moneyBefore,
            int completedCountBefore,
            bool rewardAvailableBefore,
            bool areaCompleteBefore,
            bool areaMailPendingBefore,
            int completeBundleCountBefore,
            bool allAreasCompleteBefore)
        {
            Pending = pending;
            CommunityCenter = communityCenter;
            InteractionTile = interactionTile;
            StandTile = standTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            BundleId = bundleId;
            MoneyBefore = moneyBefore;
            CompletedCountBefore = completedCountBefore;
            RewardAvailableBefore = rewardAvailableBefore;
            AreaCompleteBefore = areaCompleteBefore;
            AreaMailPendingBefore = areaMailPendingBefore;
            CompleteBundleCountBefore = completeBundleCountBefore;
            AllAreasCompleteBefore = allAreasCompleteBefore;
            LastObservedTile = StardewValley.Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public CommunityCenter CommunityCenter { get; }
        public Point InteractionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int BundleId { get; }
        public int MoneyBefore { get; }
        public int CompletedCountBefore { get; }
        public bool RewardAvailableBefore { get; }
        public bool AreaCompleteBefore { get; }
        public bool AreaMailPendingBefore { get; }
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
        public bool PurchaseClickIssued { get; set; }
        public bool BackClickIssued { get; set; }
        public bool ExitIssued { get; set; }
        public int SettlementTicks { get; set; }
    }
}
