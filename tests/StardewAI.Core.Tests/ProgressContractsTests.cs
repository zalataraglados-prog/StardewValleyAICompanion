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
    public void ProgressDtosSerializeWithSnakeCaseJsonNames()
    {
        var quest = new QuestProgressRef
        {
            Id = "10",
            Title = "Introductions",
            CurrentObjective = "Meet everyone",
            QuestType = 1,
            Accepted = true,
            Completed = false,
            DailyQuest = false,
            DaysLeft = 0,
            MoneyReward = 100
        };

        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"current_objective\"", json);
        Assert.Contains("\"quest_type\"", json);
        Assert.Contains("\"money_reward\"", json);
    }

    [Fact]
    public void CompletedQuestProgressSerializesVerifiedReadFields()
    {
        var progress = new CompletedQuestProgressRef
        {
            TotalCount = 12,
            HistoryIdentityAvailable = false,
            HistoryIdentitySource = "Game1.stats.QuestsCompleted",
            RetainedCompletedQuests = new[]
            {
                new QuestProgressRef
                {
                    Id = "10",
                    Title = "Introductions",
                    Completed = true
                }
            }
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"total_count\"", json);
        Assert.Contains("\"retained_completed_quests\"", json);
        Assert.Contains("\"history_identity_available\"", json);
        Assert.Contains("\"history_identity_source\"", json);
    }

    [Fact]
    public void CommunityCenterProgressKeepsBundleSlotsExplicit()
    {
        var progress = new CommunityCenterProgressRef
        {
            Bundles = new Dictionary<int, bool[]> { [0] = new[] { true, false, true } },
            BundleRewards = new Dictionary<int, bool> { [0] = false },
            CompletedAreaMailFlags = new[] { "ccPantry" }
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"bundles\"", json);
        Assert.Contains("false", json);
        Assert.Contains("ccPantry", json);
    }

    [Fact]
    public void PerfectionProgressSerializesVerifiedReadFields()
    {
        var progress = new PerfectionProgressRef
        {
            PercentComplete = 0.955,
            PercentFloor = 95,
            PerfectionWaivers = 2,
            EffectivePercentWithWaivers = 0.975,
            IsCompleteWithWaivers = false
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"percent_complete\"", json);
        Assert.Contains("\"perfection_waivers\"", json);
        Assert.Contains("\"effective_percent_with_waivers\"", json);
        Assert.Contains("\"is_complete_with_waivers\"", json);
    }

    [Fact]
    public void GoldenWalnutProgressSerializesVerifiedReadFields()
    {
        var progress = new GoldenWalnutProgressRef
        {
            Current = 12,
            Found = 101,
            FoundCappedForPerfection = 101,
            PerfectionTarget = 130,
            QiRoomActualFound = 100,
            QiRoomUnlockTarget = 100,
            QiRoomUnlocked = true
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"current\"", json);
        Assert.Contains("\"found_capped_for_perfection\"", json);
        Assert.Contains("\"perfection_target\"", json);
        Assert.Contains("\"qi_room_actual_found\"", json);
        Assert.Contains("\"qi_room_unlocked\"", json);
    }

    [Fact]
    public void QuestCandidateRefSerializesWithSnakeCaseJsonNames()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest",
            Family = "ordinary_quest",
            QuestId = "10",
            RuntimeType = "ItemDeliveryQuest",
            Title = "Deliver Item",
            Available = false,
            BlockedDiagnostics = new[] { "delivery_missing_target_and_item", "quest_native_executor_not_implemented" },
            NextActionCategory = "delivery_fields_incomplete",
            RequiredTargetNpc = "Lewis",
            RequiredItemId = "(O)70",
            RequiredTargetCount = 1,
            TimeCostUnknown = true,
            EnergyCostUnknown = true,
            Provenance = "direct_net_fields:ItemDeliveryQuest"
        };

        var json = JsonSerializer.Serialize(candidate, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"candidate_id\"", json);
        Assert.Contains("\"quest:10:ItemDeliveryQuest\"", json);
        Assert.Contains("\"blocked_diagnostics\"", json);
        Assert.Contains("\"delivery_missing_target_and_item\"", json);
        Assert.Contains("\"quest_native_executor_not_implemented\"", json);
        Assert.Contains("\"time_cost_unknown\"", json);
        Assert.Contains("\"energy_cost_unknown\"", json);
        Assert.Contains("\"provenance\"", json);
        Assert.Contains("\"direct_net_fields:ItemDeliveryQuest\"", json);
    }

    [Fact]
    public void QuestCompilerEnvelopeSerializesExecutorBlockAndUnknownCost()
    {
        var envelope = new QuestCompilerEnvelope
        {
            SelectedCandidateId = "quest:10:ItemDeliveryQuest",
            SelectedQuestKey = "10",
            SelectedRuntimeType = "ItemDeliveryQuest",
            Family = "ordinary_quest",
            NextActionCategory = "deliver_to_npc",
            RequiredTargetNpc = "Lewis",
            RequiredItemId = "(O)70",
            RequiredTargetCount = 1,
            TimeEstimate = "unknown",
            EnergyCost = "unknown",
            ExecutorBlockReason = "quest_native_executor_not_implemented",
            LiveEvidence = new QuestCompilerEvidence
            {
                Candidate = new QuestCandidateRef
                {
                    CandidateId = "quest:10:ItemDeliveryQuest",
                    Family = "ordinary_quest",
                    RuntimeType = "ItemDeliveryQuest",
                    Available = false
                }
            }
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"quest_compiler.v1\"", json);
        Assert.Contains("\"executor_block_reason\"", json);
        Assert.Contains("\"quest_native_executor_not_implemented\"", json);
        Assert.Contains("\"time_estimate\"", json);
        Assert.Contains("\"live_evidence\"", json);
        Assert.Contains("\"ItemDeliveryQuest\"", json);
    }

    [Fact]
    public void QuestCompilerEnvelopeSerializesNullLiveEvidence()
    {
        var envelope = new QuestCompilerEnvelope
        {
            ExecutorBlockReason = "quest_candidate_not_found"
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"quest_candidate_not_found\"", json);
        Assert.DoesNotContain("\"live_evidence\"", json);
    }

    [Fact]
    public void Type9DisambiguationViaPerTypeFields()
    {
        var harvest = new QuestProgressRef
        {
            Id = "30",
            QuestType = 9,
            RuntimeType = "type9_ambiguous",
            PerTypeFields = new PerTypeQuestFields
            {
                ItemId = "24",
                CurrentCount = 5,
                TargetCount = 10
            }
        };

        var lostItem = new QuestProgressRef
        {
            Id = "31",
            QuestType = 9,
            RuntimeType = "type9_ambiguous",
            PerTypeFields = new PerTypeQuestFields
            {
                NpcName = "Robin",
                LocationOfItem = "ScienceHouse",
                ItemId = "70",
                TileX = 3,
                TileY = 5
            }
        };

        var secretLost = new QuestProgressRef
        {
            Id = "32",
            QuestType = 9,
            RuntimeType = "type9_ambiguous",
            PerTypeFields = new PerTypeQuestFields
            {
                NpcName = "Wizard",
                FriendshipReward = 250,
                ExclusiveQuestId = "secretQuest",
                ItemId = "72",
                ItemFound = false
            }
        };

        var harvestJson = JsonSerializer.Serialize(harvest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var lostItemJson = JsonSerializer.Serialize(lostItem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var secretLostJson = JsonSerializer.Serialize(secretLost, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"item_id\"", harvestJson);
        Assert.Contains("\"current_count\"", harvestJson);
        Assert.Contains("\"npc_name\"", lostItemJson);
        Assert.Contains("\"location_of_item\"", lostItemJson);
        Assert.Contains("\"tile_x\"", lostItemJson);
        Assert.Contains("\"tile_y\"", lostItemJson);
        Assert.Contains("\"friendship_reward\"", secretLostJson);
        Assert.Contains("\"exclusive_quest_id\"", secretLostJson);
    }

    [Fact]
    public void QuestProgressRefCanonicalJsonPropertyNamesMatchBridgeKeys()
    {
        var itemDelivery = new QuestProgressRef
        {
            Id = "10", Title = "Test", QuestType = 3, RuntimeType = "ItemDeliveryQuest",
            Accepted = true, PerTypeFields = new PerTypeQuestFields { TargetNpc = "Lewis", ItemId = "(O)70", TargetCount = 1 }
        };
        var lostItem = new QuestProgressRef
        {
            Id = "31", QuestType = 9, RuntimeType = "LostItemQuest",
            PerTypeFields = new PerTypeQuestFields { NpcName = "Robin", LocationOfItem = "ScienceHouse", TileX = 3, TileY = 5, ItemFound = false }
        };
        var slayMonster = new QuestProgressRef
        {
            Id = "15", QuestType = 4, RuntimeType = "SlayMonsterQuest",
            Accepted = true, PerTypeFields = new PerTypeQuestFields { MonsterName = "Slime", TargetCount = 10, CurrentCount = 3, IgnoreFarmMonsters = true }
        };

        var idJson = JsonSerializer.Serialize(itemDelivery, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var liJson = JsonSerializer.Serialize(lostItem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var smJson = JsonSerializer.Serialize(slayMonster, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"target_npc\"", idJson);
        Assert.Contains("\"item_id\"", idJson);
        Assert.Contains("\"target_count\"", idJson);
        Assert.Contains("\"npc_name\"", liJson);
        Assert.Contains("\"location_of_item\"", liJson);
        Assert.Contains("\"tile_x\"", liJson);
        Assert.Contains("\"tile_y\"", liJson);
        Assert.Contains("\"item_found\"", liJson);
        Assert.Contains("\"monster_name\"", smJson);
        Assert.Contains("\"ignore_farm_monsters\"", smJson);
        Assert.Contains("\"current_count\"", smJson);
    }

    [Fact]
    public void ObjectivePerTypeFieldsJsonPropertyNamesMatchBridgeKeys()
    {
        var deliver = new SpecialOrderObjectiveProgressRef
        {
            Description = "Deliver to Robin", CurrentCount = 0, MaxCount = 1,
            RuntimeType = "DeliverObjective", FailOnCompletion = false,
            PerTypeFields = new PerTypeObjectiveFields
            {
                TargetName = "Robin", Message = "Bring wood", AcceptableContextTagSets = new[] { "item_wood" }
            }
        };
        var donate = new SpecialOrderObjectiveProgressRef
        {
            Description = "Donate items", CurrentCount = 2, MaxCount = 10,
            RuntimeType = "DonateObjective",
            PerTypeFields = new PerTypeObjectiveFields
            {
                DropBox = "shipping", DropBoxGameLocation = "Farm", DropBoxTileX = 10.5f, DropBoxTileY = 20.5f,
                MinimumCapacity = 5, Confirmed = false
            }
        };
        var slayObj = new SpecialOrderObjectiveProgressRef
        {
            Description = "Slay monsters", CurrentCount = 0, MaxCount = 5,
            RuntimeType = "SlayObjective",
            PerTypeFields = new PerTypeObjectiveFields
            {
                TargetNames = new[] { "Green Slime", "Bat" }, IgnoreFarmMonsters = true
            }
        };

        var delJson = JsonSerializer.Serialize(deliver, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var donJson = JsonSerializer.Serialize(donate, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var slayJson = JsonSerializer.Serialize(slayObj, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"target_name\"", delJson);
        Assert.Contains("\"message\"", delJson);
        Assert.Contains("\"acceptable_context_tag_sets\"", delJson);
        Assert.Contains("\"drop_box\"", donJson);
        Assert.Contains("\"drop_box_game_location\"", donJson);
        Assert.Contains("\"drop_box_tile_x\"", donJson);
        Assert.Contains("\"drop_box_tile_y\"", donJson);
        Assert.Contains("\"minimum_capacity\"", donJson);
        Assert.Contains("\"target_names\"", slayJson);
        Assert.Contains("\"ignore_farm_monsters\"", slayJson);
    }

    [Fact]
    public void RewardProgressRefJsonPropertyNamesMatchBridgeKeys()
    {
        var moneyReward = new SpecialOrderRewardProgressRef
        {
            RuntimeType = "MoneyReward", Available = true, Amount = 5000, Multiplier = 1.5f
        };
        var friendshipReward = new SpecialOrderRewardProgressRef
        {
            RuntimeType = "FriendshipReward", Available = true, TargetName = "Abigail", Amount = 250
        };
        var objectReward = new SpecialOrderRewardProgressRef
        {
            RuntimeType = "ObjectReward", Available = true, ItemKey = "(O)70", Amount = 5
        };
        var mailReward = new SpecialOrderRewardProgressRef
        {
            RuntimeType = "MailReward", Available = true, NoLetter = false, GrantedMails = new[] { "welcome" }, Host = true
        };
        var resetReward = new SpecialOrderRewardProgressRef
        {
            RuntimeType = "ResetEventReward", Available = true, ResetEvents = new[] { "event1" }
        };

        var mJson = JsonSerializer.Serialize(moneyReward, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var fJson = JsonSerializer.Serialize(friendshipReward, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var oJson = JsonSerializer.Serialize(objectReward, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var mlJson = JsonSerializer.Serialize(mailReward, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var rJson = JsonSerializer.Serialize(resetReward, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"amount\"", mJson);
        Assert.Contains("\"multiplier\"", mJson);
        Assert.Contains("\"target_name\"", fJson);
        Assert.Contains("\"item_key\"", oJson);
        Assert.Contains("\"no_letter\"", mlJson);
        Assert.Contains("\"granted_mails\"", mlJson);
        Assert.Contains("\"host\"", mlJson);
        Assert.Contains("\"reset_events\"", rJson);
    }

    [Fact]
    public void SpecialOrderProgressRefFieldsMatchBridgeKeys()
    {
        var order = new SpecialOrderProgressRef
        {
            QuestKey = "qiCrop",
            QuestName = "Qi Crop",
            Requester = "Qi",
            OrderType = "Qi",
            SpecialRule = "noFertilizer",
            IsIslandOrder = 0,
            AppliedSpecialRules = true,
            Participants = new Dictionary<long, bool> { [123] = true },
            QuestState = "InProgress",
            DueDate = 120,
            Duration = "Week"
        };

        var json = JsonSerializer.Serialize(order, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"quest_key\"", json);
        Assert.Contains("\"special_rule\"", json);
        Assert.Contains("\"is_island_order\"", json);
        Assert.Contains("\"applied_special_rules\"", json);
        Assert.Contains("\"participants\"", json);
        Assert.Contains("\"unclaimed_rewards\"", json);
    }

    [Fact]
    public void SpecialOrderProgressRefParticipantMappingsSerialize()
    {
        var order = new SpecialOrderProgressRef
        {
            QuestKey = "qiCrop",
            Participants = new Dictionary<long, bool> { [123] = true, [456] = false },
            SeenParticipants = new Dictionary<long, bool> { [123] = true },
            UnclaimedRewards = new Dictionary<long, bool> { [789] = true }
        };

        var json = JsonSerializer.Serialize(order, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"participants\"", json);
        Assert.Contains("\"seen_participants\"", json);
        Assert.Contains("\"unclaimed_rewards\"", json);
        Assert.Contains("\"123\"", json);
    }

    [Fact]
    public void SpecialOrderProgressRefStateFieldsSerialize()
    {
        var order = new SpecialOrderProgressRef
        {
            QuestKey = "qiCrop",
            PreSelectedItems = new Dictionary<string, string> { ["item1"] = "tag1" },
            SelectedRandomElements = new Dictionary<string, int> { ["element1"] = 42 },
            GenerationSeed = 12345,
            ReadyForRemoval = false,
            ItemToRemoveOnEnd = "qiCrop",
            MailToRemoveOnEnd = "qiComplete"
        };

        var json = JsonSerializer.Serialize(order, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"pre_selected_items\"", json);
        Assert.Contains("\"selected_random_elements\"", json);
        Assert.Contains("\"generation_seed\"", json);
        Assert.Contains("\"ready_for_removal\"", json);
        Assert.Contains("\"item_to_remove_on_end\"", json);
        Assert.Contains("\"mail_to_remove_on_end\"", json);
    }

    [Fact]
    public void SpecialOrderDonatedItemRefFieldsSerialize()
    {
        var donated = new SpecialOrderDonatedItemRef
        {
            ItemId = "(O)70",
            Stack = 5,
            Quality = 1
        };

        var json = JsonSerializer.Serialize(donated, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"item_id\"", json);
        Assert.Contains("\"stack\"", json);
        Assert.Contains("\"quality\"", json);
    }

    [Fact]
    public void QuestProgressRefCanBeCancelledAndDayQuestAcceptedSerialize()
    {
        var quest = new QuestProgressRef
        {
            Id = "10",
            QuestType = 1,
            CanBeCancelled = true,
            DayQuestAccepted = 15
        };

        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"can_be_cancelled\"", json);
        Assert.Contains("\"day_quest_accepted\"", json);
    }

    [Fact]
    public void EventCandidateEstimatedTicksAndEnergyCostAreMinusOneForQuestCandidates()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest",
            Family = "ordinary_quest",
            QuestId = "10",
            RuntimeType = "ItemDeliveryQuest",
            NextActionCategory = "deliver_to_npc",
            RequiredTargetNpc = "Lewis",
            RequiredTargetCount = 1,
            TimeCostUnknown = true,
            EnergyCostUnknown = true
        };

        var json = JsonSerializer.Serialize(candidate, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"time_cost_unknown\":true", json);
        Assert.Contains("\"energy_cost_unknown\":true", json);
    }

    [Fact]
    public void QuestProgressRefTitleDescriptionAvailableSerialize()
    {
        var quest = new QuestProgressRef
        {
            Id = "10",
            Title = "Introductions",
            TitleAvailable = true,
            Description = "Meet everyone",
            DescriptionAvailable = false,
            CurrentObjective = ""
        };

        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"title_available\":true", json);
        Assert.Contains("\"description_available\":false", json);
    }

}
