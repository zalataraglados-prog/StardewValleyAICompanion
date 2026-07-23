using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ReadQuestProgressMapper : IQuestProgressMapper
{
    public QuestProgressRef MapQuest(Quest quest)
    {
        return new QuestProgressRef
        {
            Id = quest.id.Value,
            Title = quest._questTitle,
            Description = quest._questDescription,
            CurrentObjective = quest._currentObjective,
            TitleAvailable = quest._questTitle is not null && quest._questTitle.Length > 0,
            DescriptionAvailable = quest._questDescription is not null && quest._questDescription.Length > 0,
            CurrentObjectiveAvailable = quest._currentObjective is not null && quest._currentObjective.Length > 0,
            QuestType = quest.questType.Value,
            Accepted = quest.accepted.Value,
            Completed = quest.completed.Value,
            DailyQuest = quest.dailyQuest.Value,
            CanBeCancelled = quest.canBeCancelled.Value,
            DayQuestAccepted = quest.dayQuestAccepted.Value,
            DaysLeft = quest.daysLeft.Value,
            MoneyReward = quest.moneyReward.Value,
            RewardDescription = quest.rewardDescription.Value ?? string.Empty,
            ShowNew = quest.showNew.Value,
            Destroy = quest.destroy.Value,
            NextQuests = quest.nextQuests.ToArray(),
            ModData = quest.modData.Pairs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            ObsoleteCompletionString = quest.obsolete_completionString ?? string.Empty,
            RuntimeType = ResolveRuntimeClass(quest),
            PerTypeFields = MapPerTypeQuestFields(quest)
        };
    }

    public PerTypeQuestFields MapPerTypeQuestFields(Quest quest)
    {
        var fields = new PerTypeQuestFields
        {
            Available = true,
            UnsupportedSubtype = string.Empty,
            IsBaseQuest = quest.GetType() == typeof(Quest)
        };

        switch (quest)
        {
            case Quest _ when quest.GetType() == typeof(Quest):
                break;
            case CraftingQuest cq:
                fields.ItemId = cq.ItemId.Value ?? string.Empty;
                break;
            case ItemDeliveryQuest idq:
                fields.TargetNpc = idq.target.Value ?? string.Empty;
                fields.ItemId = idq.ItemId.Value ?? string.Empty;
                fields.TargetCount = idq.number.Value;
                fields.TargetMessage = idq.targetMessage ?? string.Empty;
                break;
            case SlayMonsterQuest smq:
                fields.MonsterName = smq.monsterName.Value ?? string.Empty;
                fields.TargetNpc = smq.target.Value ?? string.Empty;
                fields.NumberToKill = smq.numberToKill.Value;
                fields.NumberKilled = smq.numberKilled.Value;
                fields.TargetCount = smq.numberToKill.Value;
                fields.CurrentCount = smq.numberKilled.Value;
                fields.IgnoreFarmMonsters = smq.ignoreFarmMonsters.Value;
                fields.Reward = smq.reward.Value;
                fields.TargetMessage = smq.targetMessage ?? string.Empty;
                break;
            case SocializeQuest sq:
                fields.WhoToGreet = sq.whoToGreet.ToArray();
                fields.TotalToGreet = sq.total.Value;
                break;
            case GoSomewhereQuest gsq:
                fields.TargetLocation = gsq.whereToGo.Value ?? string.Empty;
                break;
            case FishingQuest fq:
                fields.TargetNpc = fq.target.Value ?? string.Empty;
                fields.NumberToFish = fq.numberToFish.Value;
                fields.NumberFished = fq.numberFished.Value;
                fields.TargetCount = fq.numberToFish.Value;
                fields.CurrentCount = fq.numberFished.Value;
                fields.ItemId = fq.ItemId.Value ?? string.Empty;
                fields.Reward = fq.reward.Value;
                fields.TargetMessage = fq.targetMessage ?? string.Empty;
                break;
            case HaveBuildingQuest hbq:
                fields.BuildingType = hbq.buildingType.Value ?? string.Empty;
                break;
            case ItemHarvestQuest ihq:
                fields.ItemId = ihq.ItemId.Value ?? string.Empty;
                fields.TargetCount = ihq.Number.Value;
                break;
            case ResourceCollectionQuest rcq:
                fields.TargetNpc = rcq.target.Value ?? string.Empty;
                fields.NumberCollected = rcq.numberCollected.Value;
                fields.NumberRequired = rcq.number.Value;
                fields.TargetCount = rcq.number.Value;
                fields.CurrentCount = rcq.numberCollected.Value;
                fields.ItemId = rcq.ItemId.Value ?? string.Empty;
                fields.Reward = rcq.reward.Value;
                fields.TargetMessage = rcq.targetMessage.Value ?? string.Empty;
                break;
            case LostItemQuest liq:
                fields.NpcName = liq.npcName.Value ?? string.Empty;
                fields.LocationOfItem = liq.locationOfItem.Value ?? string.Empty;
                fields.ItemId = liq.ItemId.Value ?? string.Empty;
                fields.TileX = liq.tileX.Value;
                fields.TileY = liq.tileY.Value;
                fields.ItemFound = liq.itemFound.Value;
                break;
            case SecretLostItemQuest sliq:
                fields = MapSecretLostItemFields(sliq);
                break;
            default:
                fields.Available = false;
                fields.UnsupportedSubtype = quest.GetType().FullName ?? quest.GetType().Name;
                fields.UnavailableReason = "unsupported_subclass:" + (quest.GetType().FullName ?? quest.GetType().Name);
                break;
        }

        return fields;
    }

    private static PerTypeQuestFields MapSecretLostItemFields(SecretLostItemQuest sliq)
    {
        return new PerTypeQuestFields
        {
            Available = true,
            NpcName = sliq.npcName.Value ?? string.Empty,
            FriendshipReward = sliq.friendshipReward.Value,
            ExclusiveQuestId = sliq.exclusiveQuestId.Value ?? string.Empty,
            ItemId = sliq.ItemId.Value ?? string.Empty,
            ItemFound = sliq.itemFound.Value
        };
    }

    public SpecialOrderProgressRef MapSpecialOrder(SpecialOrder order)
    {
        return new SpecialOrderProgressRef
        {
            QuestKey = order.questKey.Value,
            QuestName = order.questName.Value,
            QuestDescription = order.questDescription.Value,
            Requester = order.requester.Value,
            OrderType = order.orderType.Value,
            SpecialRule = order.specialRule.Value ?? string.Empty,
            IsIslandOrder = MapIsIslandOrder(order),
            AppliedSpecialRules = order.appliedSpecialRules,
            Participants = order.participants.Pairs
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            SeenParticipants = order.seenParticipants.Pairs
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            UnclaimedRewards = order.unclaimedRewards.Pairs
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            DonatedItems = order.donatedItems
                .Select(MapDonatedItem)
                .OrderBy(item => item.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(item => item.Stack)
                .ToArray(),
            PreSelectedItems = order.preSelectedItems.Pairs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            SelectedRandomElements = order.selectedRandomElements.Pairs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            GenerationSeed = order.generationSeed.Value,
            ReadyForRemoval = order.readyForRemoval.Value,
            ItemToRemoveOnEnd = order.itemToRemoveOnEnd.Value ?? string.Empty,
            MailToRemoveOnEnd = order.mailToRemoveOnEnd.Value ?? string.Empty,
            QuestState = order.questState.Value.ToString(),
            DueDate = order.dueDate.Value,
            Duration = order.questDuration.Value.ToString(),
            Objectives = order.objectives
                .Select(MapObjective)
                .ToArray(),
            Rewards = order.rewards
                .Select(MapReward)
                .ToArray()
        };
    }

    private int MapIsIslandOrder(SpecialOrder order)
    {
        var questKey = order.questKey.Value;
        if (string.IsNullOrWhiteSpace(questKey) || Game1.content is null)
        {
            return -1;
        }

        var data = DataLoader.SpecialOrders(Game1.content);
        if (data is null || !data.TryGetValue(questKey, out var value) || value is null)
        {
            return -1;
        }

        return ClassifyIslandByTags(value.RequiredTags);
    }

    internal static int ClassifyIslandByTags(string? requiredTags)
    {
        if (requiredTags is not null && requiredTags.Contains("island"))
        {
            return 1;
        }

        return 0;
    }

    public SpecialOrderDonatedItemRef MapDonatedItem(Item? item)
    {
        if (item is null)
        {
            return new SpecialOrderDonatedItemRef
            {
                IsNullEntry = true,
                ItemId = string.Empty,
                QualifiedItemId = string.Empty,
                Stack = 0,
                Quality = 0
            };
        }

        return new SpecialOrderDonatedItemRef
        {
            IsNullEntry = false,
            ItemId = item.ItemId ?? string.Empty,
            QualifiedItemId = item.QualifiedItemId ?? string.Empty,
            Stack = item.Stack,
            Quality = item.Quality,
            ModData = item.modData.Pairs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        };
    }

    public SpecialOrderObjectiveProgressRef MapObjective(OrderObjective objective)
    {
        return new SpecialOrderObjectiveProgressRef
        {
            Description = objective.description.Value,
            CurrentCount = objective.currentCount.Value,
            MaxCount = objective.maxCount.Value,
            RuntimeType = ResolveObjectiveRuntimeClass(objective),
            FailOnCompletion = objective.failOnCompletion.Value,
            Complete = objective.IsComplete(),
            PerTypeFields = MapPerTypeObjectiveFields(objective)
        };
    }

    public PerTypeObjectiveFields MapPerTypeObjectiveFields(OrderObjective objective)
    {
        var fields = new PerTypeObjectiveFields { Available = true };

        switch (objective)
        {
            case CollectObjective co:
                fields.AcceptableContextTagSets = co.acceptableContextTagSets?.ToArray() ?? Array.Empty<string>();
                break;
            case DeliverObjective dvo:
                fields.AcceptableContextTagSets = dvo.acceptableContextTagSets?.ToArray() ?? Array.Empty<string>();
                fields.TargetName = dvo.targetName.Value ?? string.Empty;
                fields.Message = dvo.message.Value ?? string.Empty;
                break;
            case DonateObjective dno:
                fields.DropBox = dno.dropBox.Value ?? string.Empty;
                fields.DropBoxGameLocation = dno.dropBoxGameLocation.Value ?? string.Empty;
                fields.ResolvedDropBoxGameLocation = dno.GetDropboxLocationName() ?? string.Empty;
                fields.DropBoxTileX = dno.dropBoxTileLocation.Value.X;
                fields.DropBoxTileY = dno.dropBoxTileLocation.Value.Y;
                fields.AcceptableContextTagSets = dno.acceptableContextTagSets?.ToArray() ?? Array.Empty<string>();
                fields.MinimumCapacity = dno.minimumCapacity.Value;
                fields.Confirmed = dno.confirmed.Value;
                break;
            case FishObjective fo:
                fields.AcceptableContextTagSets = fo.acceptableContextTagSets?.ToArray() ?? Array.Empty<string>();
                break;
            case GiftObjective go:
                fields.AcceptableContextTagSets = go.acceptableContextTagSets?.ToArray() ?? Array.Empty<string>();
                fields.MinimumLikeLevel = go.minimumLikeLevel.Value.ToString();
                break;
            case JKScoreObjective _:
                break;
            case ReachMineFloorObjective rmfo:
                fields.SkullCave = rmfo.skullCave.Value;
                break;
            case ShipObjective sho:
                fields.AcceptableContextTagSets = sho.acceptableContextTagSets?.ToArray() ?? Array.Empty<string>();
                fields.UseShipmentValue = sho.useShipmentValue.Value;
                break;
            case SlayObjective slo:
                fields.TargetNames = slo.targetNames?.ToArray() ?? Array.Empty<string>();
                fields.IgnoreFarmMonsters = slo.ignoreFarmMonsters.Value;
                break;
            default:
                fields.Available = false;
                fields.UnavailableReason = "unknown_objective_subclass:" + objective.GetType().FullName;
                break;
        }

        return fields;
    }

    public SpecialOrderRewardProgressRef MapReward(OrderReward reward)
    {
        var rewardFields = new SpecialOrderRewardProgressRef
        {
            RuntimeType = ResolveRewardRuntimeClass(reward),
            Available = true
        };

        switch (reward)
        {
            case MoneyReward mr:
                rewardFields.Amount = mr.amount.Value;
                rewardFields.Multiplier = mr.multiplier.Value;
                break;
            case FriendshipReward fr:
                rewardFields.TargetName = fr.targetName.Value ?? string.Empty;
                rewardFields.Amount = fr.amount.Value;
                break;
            case GemsReward gr:
                rewardFields.Amount = gr.amount.Value;
                break;
            case MailReward mlr:
                rewardFields.NoLetter = mlr.noLetter.Value;
                rewardFields.GrantedMails = mlr.grantedMails?.ToArray() ?? Array.Empty<string>();
                rewardFields.Host = mlr.host.Value;
                break;
            case ObjectReward orw:
                rewardFields.ItemKey = orw.itemKey.Value ?? string.Empty;
                rewardFields.Amount = orw.amount.Value;
                break;
            case ResetEventReward rer:
                rewardFields.ResetEvents = rer.resetEvents?.ToArray() ?? Array.Empty<string>();
                break;
            default:
                rewardFields.Available = false;
                rewardFields.UnavailableReason = "unknown_reward_subclass:" + reward.GetType().FullName;
                break;
        }

        return rewardFields;
    }

    public CompletedQuestProgressRef? MapCompletedQuests(Farmer? player)
    {
        if (player is null)
        {
            return null;
        }

        return new CompletedQuestProgressRef
        {
            TotalCount = Game1.stats.QuestsCompleted,
            RetainedCompletedQuests = player.questLog
                .Select(MapQuest)
                .Where(quest => quest.Completed)
                .OrderBy(quest => quest.Id, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<QuestProgressRef>(),
            HistoryIdentityAvailable = false,
            HistoryIdentitySource = "No verified global completed quest ID collection; Game1.stats.QuestsCompleted is the verified total count."
        };
    }

    private static string ResolveRuntimeClass(Quest quest)
    {
        if (quest.GetType() == typeof(Quest))
        {
            return "Quest";
        }

        return quest switch
        {
            CraftingQuest _ => "CraftingQuest",
            ItemDeliveryQuest _ => "ItemDeliveryQuest",
            SlayMonsterQuest _ => "SlayMonsterQuest",
            SocializeQuest _ => "SocializeQuest",
            GoSomewhereQuest _ => "GoSomewhereQuest",
            FishingQuest _ => "FishingQuest",
            HaveBuildingQuest _ => "HaveBuildingQuest",
            ResourceCollectionQuest _ => "ResourceCollectionQuest",
            LostItemQuest _ => "LostItemQuest",
            SecretLostItemQuest _ => "SecretLostItemQuest",
            ItemHarvestQuest _ => "ItemHarvestQuest",
            _ => quest.GetType().FullName ?? quest.GetType().Name
        };
    }

    private static string ResolveObjectiveRuntimeClass(OrderObjective objective)
    {
        return objective switch
        {
            CollectObjective _ => "CollectObjective",
            DeliverObjective _ => "DeliverObjective",
            DonateObjective _ => "DonateObjective",
            FishObjective _ => "FishObjective",
            GiftObjective _ => "GiftObjective",
            JKScoreObjective _ => "JKScoreObjective",
            ReachMineFloorObjective _ => "ReachMineFloorObjective",
            ShipObjective _ => "ShipObjective",
            SlayObjective _ => "SlayObjective",
            _ => objective.GetType().FullName ?? objective.GetType().Name
        };
    }

    private static string ResolveRewardRuntimeClass(OrderReward reward)
    {
        return reward switch
        {
            MoneyReward _ => "MoneyReward",
            FriendshipReward _ => "FriendshipReward",
            GemsReward _ => "GemsReward",
            MailReward _ => "MailReward",
            ObjectReward _ => "ObjectReward",
            ResetEventReward _ => "ResetEventReward",
            _ => reward.GetType().FullName ?? reward.GetType().Name
        };
    }
}
