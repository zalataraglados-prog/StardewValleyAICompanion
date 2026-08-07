using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley.Locations;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveMuseumDonation
    {
        public ActiveMuseumDonation(
            PendingExecution pending,
            LibraryMuseum museum,
            Point actionTile,
            Point standTile,
            Point donationTile,
            List<Point> path,
            int maxMovementTiles,
            int inventorySlotIndex,
            string qualifiedItemId,
            int stackBefore,
            int donatedCountBefore,
            bool achievementBefore,
            bool rewardClaimedBefore,
            bool prerequisiteEventBefore,
            Quest? fieldGuideQuestBefore,
            string pendingRewardIdsBeforeJson)
        {
            Pending = pending;
            Museum = museum;
            ActionTile = actionTile;
            StandTile = standTile;
            DonationTile = donationTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            InventorySlotIndex = inventorySlotIndex;
            QualifiedItemId = qualifiedItemId;
            StackBefore = stackBefore;
            DonatedCountBefore = donatedCountBefore;
            AchievementBefore = achievementBefore;
            RewardClaimedBefore = rewardClaimedBefore;
            PrerequisiteEventBefore = prerequisiteEventBefore;
            FieldGuideQuestBefore = fieldGuideQuestBefore;
            PendingRewardIdsBeforeJson = pendingRewardIdsBeforeJson;
            LastObservedTile = StardewValley.Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public LibraryMuseum Museum { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public Point DonationTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int InventorySlotIndex { get; }
        public string QualifiedItemId { get; }
        public int StackBefore { get; }
        public int DonatedCountBefore { get; }
        public bool AchievementBefore { get; }
        public bool RewardClaimedBefore { get; }
        public bool PrerequisiteEventBefore { get; }
        public Quest? FieldGuideQuestBefore { get; }
        public string PendingRewardIdsBeforeJson { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool OpenIssued { get; set; }
        public int OpenWaitTicks { get; set; }
        public int MenuReadyWaitTicks { get; set; }
        public bool InventoryClickIssued { get; set; }
        public bool DonationClickIssued { get; set; }
        public bool CloseIssued { get; set; }
        public int SettlementTicks { get; set; }
    }
}
