using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;

namespace StardewAI.TransparentBridge.Adapters;

public interface IQuestProgressMapper
{
    QuestProgressRef MapQuest(Quest quest);
    PerTypeQuestFields MapPerTypeQuestFields(Quest quest);
    SpecialOrderProgressRef MapSpecialOrder(SpecialOrder order);
    SpecialOrderObjectiveProgressRef MapObjective(OrderObjective objective);
    PerTypeObjectiveFields MapPerTypeObjectiveFields(OrderObjective objective);
    SpecialOrderRewardProgressRef MapReward(OrderReward reward);
    CompletedQuestProgressRef? MapCompletedQuests(Farmer? player);
    SpecialOrderDonatedItemRef MapDonatedItem(Item? item);
}
