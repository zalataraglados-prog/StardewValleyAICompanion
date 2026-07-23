using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveQuestDropBoxDonation
    {
        public ActiveQuestDropBoxDonation(
            PendingExecution pending,
            GameLocation location,
            SpecialOrder order,
            DonateObjective objective,
            Point actionTile,
            Point standTile,
            int inventorySlotIndex,
            string qualifiedItemId,
            int stackBefore,
            int expectedAcceptedCount)
        {
            Pending = pending;
            Location = location;
            Order = order;
            Objective = objective;
            ActionTile = actionTile;
            StandTile = standTile;
            InventorySlotIndex = inventorySlotIndex;
            QualifiedItemId = qualifiedItemId;
            StackBefore = stackBefore;
            ExpectedAcceptedCount = expectedAcceptedCount;
            ProgressBefore = objective.GetCount();
            OrderStateBefore = order.questState.Value;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public SpecialOrder Order { get; }
        public DonateObjective Objective { get; }
        public Point ActionTile { get; }
        public Point StandTile { get; }
        public int InventorySlotIndex { get; }
        public string QualifiedItemId { get; }
        public int StackBefore { get; }
        public int ExpectedAcceptedCount { get; }
        public int ProgressBefore { get; }
        public SpecialOrderStatus OrderStateBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public bool OpenIssued { get; set; }
        public int OpenWaitTicks { get; set; }
        public bool InventoryClickIssued { get; set; }
        public bool CloseIssued { get; set; }
        public int SettlementTicks { get; set; }
    }
}
