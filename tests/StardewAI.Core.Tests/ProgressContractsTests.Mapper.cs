using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using StardewAI.TransparentBridge.Adapters;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;

namespace StardewAI.Core.Tests;

public sealed partial class ProgressContractsTests
{
    [Fact]
    public void ReadQuestProgressMapperMapsBaseQuestWithoutGame1()
    {
        var quest = new Quest();
        quest.questType.Value = 1;
        quest.accepted.Value = true;
        quest.completed.Value = false;
        quest.dailyQuest.Value = false;
        quest.canBeCancelled.Value = true;
        quest.moneyReward.Value = 100;
        quest._questTitle = "Introductions";
        quest._questDescription = "Meet everyone";
        quest._currentObjective = "Speak to 5 people";
        quest.rewardDescription.Value = "100g";
        quest.showNew.Value = true;
        quest.destroy.Value = false;
        quest.nextQuests.Add("11");
        quest.modData["custom_key"] = "custom_value";
        quest.obsolete_completionString = "legacy";

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.NotNull(result);
        Assert.Equal("Introductions", result.Title);
        Assert.True(result.TitleAvailable);
        Assert.True(result.DescriptionAvailable);
        Assert.True(result.CurrentObjectiveAvailable);
        Assert.Equal(1, result.QuestType);
        Assert.True(result.Accepted);
        Assert.False(result.Completed);
        Assert.True(result.CanBeCancelled);
        Assert.Equal(100, result.MoneyReward);
        Assert.Equal("100g", result.RewardDescription);
        Assert.True(result.ShowNew);
        Assert.False(result.Destroy);
        Assert.Contains("11", result.NextQuests);
        Assert.True(result.ModData.ContainsKey("custom_key"));
        Assert.Equal("custom_value", result.ModData["custom_key"]);
        Assert.Equal("legacy", result.ObsoleteCompletionString);
        Assert.Equal("Quest", result.RuntimeType);
        Assert.True(result.PerTypeFields.IsBaseQuest);
        Assert.True(result.PerTypeFields.Available);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsItemDeliveryQuestSubclass()
    {
        var quest = new ItemDeliveryQuest();
        quest.questType.Value = 3;
        quest.accepted.Value = true;
        quest.target.Value = "Lewis";
        quest.ItemId.Value = "(O)70";
        quest.number.Value = 1;
        quest.targetMessage = "Bring a parsnip";
        quest._questTitle = "Delivery";

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.NotNull(result);
        Assert.Equal("ItemDeliveryQuest", result.RuntimeType);
        Assert.Equal("Lewis", result.PerTypeFields.TargetNpc);
        Assert.Equal("(O)70", result.PerTypeFields.ItemId);
        Assert.Equal(1, result.PerTypeFields.TargetCount);
        Assert.Equal("Bring a parsnip", result.PerTypeFields.TargetMessage);
        Assert.True(result.PerTypeFields.Available);
        Assert.False(result.PerTypeFields.IsBaseQuest);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsSlayMonsterQuest()
    {
        var quest = new SlayMonsterQuest();
        quest.monsterName.Value = "Green Slime";
        quest.target.Value = "Adventurer's Guild";
        quest.numberToKill.Value = 10;
        quest.numberKilled.Value = 3;
        quest.ignoreFarmMonsters.Value = true;
        quest.reward.Value = 500;
        quest.targetMessage = "Slay them";

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.Equal("SlayMonsterQuest", result.RuntimeType);
        Assert.Equal("Green Slime", result.PerTypeFields.MonsterName);
        Assert.Equal("Adventurer's Guild", result.PerTypeFields.TargetNpc);
        Assert.Equal(10, result.PerTypeFields.NumberToKill);
        Assert.Equal(3, result.PerTypeFields.NumberKilled);
        Assert.Equal(10, result.PerTypeFields.TargetCount);
        Assert.Equal(3, result.PerTypeFields.CurrentCount);
        Assert.True(result.PerTypeFields.IgnoreFarmMonsters);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsItemHarvestQuestType9()
    {
        var quest = new ItemHarvestQuest();
        quest.questType.Value = 9;
        quest.ItemId.Value = "24";
        quest.Number.Value = 5;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.Equal("ItemHarvestQuest", result.RuntimeType);
        Assert.Equal("24", result.PerTypeFields.ItemId);
        Assert.Equal(5, result.PerTypeFields.TargetCount);
        Assert.True(result.PerTypeFields.Available);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsLostItemQuestType9()
    {
        var quest = new LostItemQuest();
        quest.questType.Value = 9;
        quest.npcName.Value = "Robin";
        quest.locationOfItem.Value = "ScienceHouse";
        quest.ItemId.Value = "70";
        quest.tileX.Value = 3;
        quest.tileY.Value = 5;
        quest.itemFound.Value = false;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.Equal("LostItemQuest", result.RuntimeType);
        Assert.Equal("Robin", result.PerTypeFields.NpcName);
        Assert.Equal("ScienceHouse", result.PerTypeFields.LocationOfItem);
        Assert.Equal("70", result.PerTypeFields.ItemId);
        Assert.Equal(3, result.PerTypeFields.TileX);
        Assert.Equal(5, result.PerTypeFields.TileY);
        Assert.False(result.PerTypeFields.ItemFound);
        Assert.True(result.PerTypeFields.Available);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsSecretLostItemQuestType9()
    {
        var quest = new SecretLostItemQuest();
        quest.questType.Value = 9;
        quest.npcName.Value = "Wizard";
        quest.friendshipReward.Value = 250;
        quest.exclusiveQuestId.Value = "secretQuest";
        quest.ItemId.Value = "72";
        quest.itemFound.Value = false;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.Equal("SecretLostItemQuest", result.RuntimeType);
        Assert.Equal("Wizard", result.PerTypeFields.NpcName);
        Assert.Equal(250, result.PerTypeFields.FriendshipReward);
        Assert.Equal("secretQuest", result.PerTypeFields.ExclusiveQuestId);
        Assert.Equal("72", result.PerTypeFields.ItemId);
        Assert.False(result.PerTypeFields.ItemFound);
        Assert.True(result.PerTypeFields.Available);
    }

    [Fact]
    public void ReadQuestProgressMapperTitleAvailableFailsClosedForEmpty()
    {
        var quest = new Quest();
        quest._questTitle = "";
        quest._questDescription = "";
        quest._currentObjective = "";

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapQuest(quest);

        Assert.False(result.TitleAvailable);
        Assert.False(result.DescriptionAvailable);
        Assert.False(result.CurrentObjectiveAvailable);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsSpecialOrderLifecycleDictionary()
    {
        var order = new SpecialOrder();
        order.questKey.Value = "qiCrop";
        order.questName.Value = "Qi Crop";
        order.questDescription.Value = "Grow Qi fruit";
        order.requester.Value = "Qi";
        order.orderType.Value = "Qi";
        order.specialRule.Value = "noFertilizer";
        order.appliedSpecialRules = true;
        order.generationSeed.Value = 12345;
        order.readyForRemoval.Value = false;
        order.itemToRemoveOnEnd.Value = "qiCrop";
        order.mailToRemoveOnEnd.Value = "qiComplete";
        order.questState.Value = SpecialOrderStatus.InProgress;
        order.dueDate.Value = 120;
        order.questDuration.Value = StardewValley.GameData.SpecialOrders.QuestDuration.Week;

        order.participants[123] = true;
        order.seenParticipants[123] = true;
        order.unclaimedRewards[456] = true;

        order.preSelectedItems["seed"] = "qiBean";
        order.selectedRandomElements["count"] = 42;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapSpecialOrder(order);

        Assert.NotNull(result);
        Assert.Equal("qiCrop", result.QuestKey);
        Assert.Equal("Qi Crop", result.QuestName);
        Assert.Equal("Qi", result.Requester);
        Assert.Equal("noFertilizer", result.SpecialRule);
        Assert.True(result.AppliedSpecialRules);
        Assert.Equal(12345, result.GenerationSeed);
        Assert.False(result.ReadyForRemoval);
        Assert.Equal("qiCrop", result.ItemToRemoveOnEnd);
        Assert.Equal("qiComplete", result.MailToRemoveOnEnd);
        Assert.Equal("InProgress", result.QuestState);
        Assert.Equal(120, result.DueDate);
        Assert.True(result.Participants.ContainsKey(123));
        Assert.True(result.Participants[123]);
        Assert.True(result.SeenParticipants.ContainsKey(123));
        Assert.True(result.UnclaimedRewards.ContainsKey(456));
        Assert.Equal("qiBean", result.PreSelectedItems["seed"]);
        Assert.Equal(42, result.SelectedRandomElements["count"]);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsCollectObjective()
    {
        var objective = new CollectObjective();
        objective.currentCount.Value = 3;
        objective.maxCount.Value = 10;
        objective.description.Value = "Collect items";
        objective.failOnCompletion.Value = false;
        objective.acceptableContextTagSets.Add("item_spring");

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapObjective(objective);

        Assert.Equal("CollectObjective", result.RuntimeType);
        Assert.Equal(3, result.CurrentCount);
        Assert.Equal(10, result.MaxCount);
        Assert.Equal("Collect items", result.Description);
        Assert.False(result.FailOnCompletion);
        Assert.True(result.PerTypeFields.Available);
        Assert.Contains("item_spring", result.PerTypeFields.AcceptableContextTagSets);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsDonateObjective()
    {
        var objective = new DonateObjective();
        objective.currentCount.Value = 2;
        objective.maxCount.Value = 10;
        objective.dropBox.Value = "shipping";
        objective.dropBoxGameLocation.Value = "Farm";
        objective.dropBoxTileLocation.Value = new Microsoft.Xna.Framework.Vector2(10.5f, 20.5f);
        objective.minimumCapacity.Value = 5;
        objective.confirmed.Value = false;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapObjective(objective);

        Assert.Equal("DonateObjective", result.RuntimeType);
        Assert.Equal("shipping", result.PerTypeFields.DropBox);
        Assert.Equal("Farm", result.PerTypeFields.DropBoxGameLocation);
        Assert.Equal("Farm", result.PerTypeFields.ResolvedDropBoxGameLocation);
        Assert.Equal(10.5f, result.PerTypeFields.DropBoxTileX);
        Assert.Equal(20.5f, result.PerTypeFields.DropBoxTileY);
        Assert.Equal(5, result.PerTypeFields.MinimumCapacity);
        Assert.False(result.PerTypeFields.Confirmed);
        Assert.True(result.PerTypeFields.Available);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsSlayObjective()
    {
        var objective = new SlayObjective();
        objective.currentCount.Value = 0;
        objective.maxCount.Value = 5;
        objective.targetNames.Add("Green Slime");
        objective.targetNames.Add("Bat");
        objective.ignoreFarmMonsters.Value = true;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapObjective(objective);

        Assert.Equal("SlayObjective", result.RuntimeType);
        Assert.Contains("Green Slime", result.PerTypeFields.TargetNames);
        Assert.Contains("Bat", result.PerTypeFields.TargetNames);
        Assert.True(result.PerTypeFields.IgnoreFarmMonsters);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsMoneyReward()
    {
        var reward = new MoneyReward();
        reward.amount.Value = 5000;
        reward.multiplier.Value = 1.5f;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapReward(reward);

        Assert.Equal("MoneyReward", result.RuntimeType);
        Assert.True(result.Available);
        Assert.Equal(5000, result.Amount);
        Assert.Equal(1.5f, result.Multiplier);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsFriendshipReward()
    {
        var reward = new FriendshipReward();
        reward.targetName.Value = "Abigail";
        reward.amount.Value = 250;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapReward(reward);

        Assert.Equal("FriendshipReward", result.RuntimeType);
        Assert.True(result.Available);
        Assert.Equal("Abigail", result.TargetName);
        Assert.Equal(250, result.Amount);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsObjectReward()
    {
        var reward = new ObjectReward();
        reward.itemKey.Value = "(O)70";
        reward.amount.Value = 5;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapReward(reward);

        Assert.Equal("ObjectReward", result.RuntimeType);
        Assert.True(result.Available);
        Assert.Equal("(O)70", result.ItemKey);
        Assert.Equal(5, result.Amount);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsMailReward()
    {
        var reward = new MailReward();
        reward.noLetter.Value = false;
        reward.grantedMails.Add("welcome");
        reward.host.Value = true;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapReward(reward);

        Assert.Equal("MailReward", result.RuntimeType);
        Assert.True(result.Available);
        Assert.False(result.NoLetter);
        Assert.Contains("welcome", result.GrantedMails);
        Assert.True(result.Host);
    }

    [Fact]
    public void ReadQuestProgressMapperMapsResetEventReward()
    {
        var reward = new ResetEventReward();
        reward.resetEvents.Add("event1");

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapReward(reward);

        Assert.Equal("ResetEventReward", result.RuntimeType);
        Assert.True(result.Available);
        Assert.Contains("event1", result.ResetEvents);
    }

    [Fact]
    public void ReadQuestProgressMapperUnknownObjectiveFailsClosed()
    {
        var objective = new UnknownTestObjective();
        objective.currentCount.Value = 0;
        objective.maxCount.Value = 1;

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapObjective(objective);

        Assert.False(result.PerTypeFields.Available);
        Assert.Contains("unknown_objective_subclass", result.PerTypeFields.UnavailableReason);
    }

    [Fact]
    public void ReadQuestProgressMapperUnknownRewardFailsClosed()
    {
        var reward = new UnknownTestReward();

        var mapper = new ReadQuestProgressMapper();
        var result = mapper.MapReward(reward);

        Assert.False(result.Available);
        Assert.Contains("unknown_reward_subclass", result.UnavailableReason);
    }

    [Fact]
    public void ReadQuestProgressMapperDonatedItemNullAndRealDistinguishable()
    {
        var mapper = new ReadQuestProgressMapper();

        var resultNull = mapper.MapDonatedItem(null);
        var donatedItem = new StardewValley.Object();
        donatedItem.ItemId = "70";
        donatedItem.Stack = 5;
        var resultReal = mapper.MapDonatedItem(donatedItem);

        Assert.True(resultNull.IsNullEntry);
        Assert.Empty(resultNull.ItemId);
        Assert.Empty(resultNull.QualifiedItemId);
        Assert.Equal(0, resultNull.Stack);
        Assert.Equal(0, resultNull.Quality);

        Assert.False(resultReal.IsNullEntry);
        Assert.Equal("70", resultReal.ItemId);
        Assert.Equal("(O)70", resultReal.QualifiedItemId);
        Assert.Equal(5, resultReal.Stack);
    }

    [Fact]
    public void QuestProgressMapperInterfaceExistsAndAdapterUsesIt()
    {
        var adapter = new ProgressQuestReadAdapter();
        Assert.NotNull(adapter);
    }

    [Fact]
    public void ReadQuestProgressMapperIsConcreteImplementation()
    {
        var mapper = new ReadQuestProgressMapper();
        Assert.NotNull(mapper);
        Assert.IsAssignableFrom<IQuestProgressMapper>(mapper);
    }

}
