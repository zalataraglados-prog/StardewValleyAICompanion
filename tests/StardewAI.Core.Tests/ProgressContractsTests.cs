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

public sealed class ProgressContractsTests
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

    [Fact]
    public void QuestProgressRefLifecycleFieldsSerialize()
    {
        var quest = new QuestProgressRef
        {
            Id = "10",
            RewardDescription = "500g",
            ShowNew = true,
            Destroy = false,
            NextQuests = new[] { "11", "12" }
        };

        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"reward_description\"", json);
        Assert.Contains("\"show_new\"", json);
        Assert.Contains("\"destroy\"", json);
        Assert.Contains("\"next_quests\"", json);
        Assert.Contains("\"11\"", json);
    }

    [Fact]
    public void PerTypeObjectiveFieldsKnownSubtypesAreAvailable()
    {
        var known = new PerTypeObjectiveFields { Available = true };
        var unknown = new PerTypeObjectiveFields
        {
            Available = false,
            UnavailableReason = "unsupported_subtype"
        };

        var knownJson = JsonSerializer.Serialize(known, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var unknownJson = JsonSerializer.Serialize(unknown, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"available\":true", knownJson);
        Assert.Contains("\"available\":false", unknownJson);
        Assert.Contains("\"unavailable_reason\"", unknownJson);
    }

    [Fact]
    public void Type9DisambiguationProducesCorrectNextActionCategories()
    {
        var harvest = new QuestProgressRef
        {
            Id = "30", QuestType = 9, RuntimeType = "ItemHarvestQuest", Accepted = true,
            PerTypeFields = new PerTypeQuestFields { ItemId = "24", TargetCount = 10 }
        };
        var lostItem = new QuestProgressRef
        {
            Id = "31", QuestType = 9, RuntimeType = "LostItemQuest", Accepted = true,
            PerTypeFields = new PerTypeQuestFields { NpcName = "Robin", LocationOfItem = "ScienceHouse", TileX = 3, TileY = 5 }
        };
        var secretLost = new QuestProgressRef
        {
            Id = "32", QuestType = 9, RuntimeType = "SecretLostItemQuest", Accepted = true,
            PerTypeFields = new PerTypeQuestFields { NpcName = "Wizard", ExclusiveQuestId = "secret", FriendshipReward = 250 }
        };

        var candidates = QuestCandidateBuilder.BuildOrdinaryCandidates(new[] { harvest, lostItem, secretLost });

        Assert.Equal(3, candidates.Length);
        var catHarvest = candidates.Single(c => c.QuestId == "30").NextActionCategory;
        var catLostItem = candidates.Single(c => c.QuestId == "31").NextActionCategory;
        var catSecretLost = candidates.Single(c => c.QuestId == "32").NextActionCategory;
        Assert.Equal("harvest_items", catHarvest);
        Assert.Equal("find_lost_item", catLostItem);
        Assert.Equal("find_secret_lost_item", catSecretLost);
    }

    [Fact]
    public void EmptySpecialRuleIsValidNotUnavailable()
    {
        var order = new SpecialOrderProgressRef
        {
            QuestKey = "qiCrop", QuestState = "InProgress",
            SpecialRule = string.Empty,
            Objectives = new[] { new SpecialOrderObjectiveProgressRef { Description = "obj1", CurrentCount = 0, MaxCount = 5, RuntimeType = "CollectObjective", PerTypeFields = new PerTypeObjectiveFields { Available = true } } },
            Rewards = new[] { new SpecialOrderRewardProgressRef { RuntimeType = "MoneyReward", Available = true, Amount = 5000 } }
        };

        var candidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(new[] { order });

        Assert.Single(candidates);
        Assert.DoesNotContain(candidates[0].BlockedDiagnostics, d => d.Contains("special_rule_unavailable"));
    }

    [Fact]
    public void TwoOrdinaryQuestsCannotBeConfusedByCandidateId()
    {
        var quest1 = new QuestProgressRef { Id = "10", Title = "Introductions", QuestType = 1, RuntimeType = "Quest", Accepted = true };
        var quest2 = new QuestProgressRef { Id = "11", Title = "Kill Slimes", QuestType = 4, RuntimeType = "SlayMonsterQuest", Accepted = true };

        var candidates = QuestCandidateBuilder.BuildOrdinaryCandidates(new[] { quest1, quest2 });

        Assert.Equal(2, candidates.Length);
        Assert.NotEqual(candidates[0].CandidateId, candidates[1].CandidateId);
        Assert.Equal("Quest", candidates[0].RuntimeType);
        Assert.Equal("SlayMonsterQuest", candidates[1].RuntimeType);
        Assert.Equal("quest:10:Quest", candidates[0].CandidateId);
        Assert.Equal("quest:11:SlayMonsterQuest", candidates[1].CandidateId);

        var match1 = candidates.FirstOrDefault(c => c.QuestId == "10");
        var match2 = candidates.FirstOrDefault(c => c.QuestId == "11");
        Assert.NotNull(match1);
        Assert.NotNull(match2);
        Assert.Equal("Introductions", match1!.Title);
        Assert.Equal("Kill Slimes", match2!.Title);
    }

    [Fact]
    public void TwoSpecialOrdersCannotBeConfusedByQuestKey()
    {
        var order1 = new SpecialOrderProgressRef { QuestKey = "qiCrop", QuestName = "Qi Crop", QuestState = "InProgress" };
        var order2 = new SpecialOrderProgressRef { QuestKey = "qiBeans", QuestName = "Qi Beans", QuestState = "InProgress" };

        var candidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(new[] { order1, order2 });

        Assert.Equal(2, candidates.Length);
        Assert.NotEqual(candidates[0].CandidateId, candidates[1].CandidateId);
        Assert.Equal("special_order:qiCrop", candidates[0].CandidateId);
        Assert.Equal("special_order:qiBeans", candidates[1].CandidateId);

        var match1 = candidates.FirstOrDefault(c => c.QuestKey == "qiCrop");
        var match2 = candidates.FirstOrDefault(c => c.QuestKey == "qiBeans");
        Assert.NotNull(match1);
        Assert.NotNull(match2);
        Assert.Equal("Qi Crop", match1!.Title);
        Assert.Equal("Qi Beans", match2!.Title);
    }

    [Fact]
    public void BuildCompilerEnvelopeBindsSingleCandidateByIdentity()
    {
        var ordinary = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest", QuestId = "10", Family = "ordinary_quest",
            RuntimeType = "ItemDeliveryQuest", NextActionCategory = "deliver_to_npc"
        };
        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { ordinary }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedCandidateId: "quest:10:ItemDeliveryQuest");

        Assert.Equal("quest:10:ItemDeliveryQuest", envelope.SelectedCandidateId);
        Assert.Equal("10", envelope.SelectedQuestId);
        Assert.Equal("ItemDeliveryQuest", envelope.SelectedRuntimeType);
        Assert.NotNull(envelope.LiveEvidence);
        Assert.Equal("quest:10:ItemDeliveryQuest", envelope.LiveEvidence!.Candidate.CandidateId);

        var envelopeByQuestId = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { ordinary }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedQuestId: "10");

        Assert.Equal("quest:10:ItemDeliveryQuest", envelopeByQuestId.SelectedCandidateId);
    }

    [Fact]
    public void QuestCandidateBuilderCompilerEnvelopeRejectsAbsentIdentity()
    {
        var ordinary = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest", QuestId = "10"
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { ordinary }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedCandidateId: "quest:99:DoesNotExist");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Empty(envelope.SelectedQuestId);
        Assert.Contains("quest_candidate_not_found", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void QuestCompilerEnvelopeRejectsNoIdentity()
    {
        var ordinary = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest", QuestId = "10"
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { ordinary }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>());

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_identity_not_specified", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void QuestProgressRefSerializesModDataAndObsoleteCompletionString()
    {
        var quest = new QuestProgressRef
        {
            Id = "10",
            ModData = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" },
            ObsoleteCompletionString = "legacyMarker"
        };

        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"mod_data\"", json);
        Assert.Contains("\"obsolete_completion_string\"", json);
        Assert.Contains("\"legacyMarker\"", json);
        Assert.Contains("\"key1\"", json);
        Assert.Contains("\"value1\"", json);
    }

    [Fact]
    public void QuestProgressRefSerializesCurrentObjectiveAvailable()
    {
        var withObj = new QuestProgressRef { Id = "10", CurrentObjective = "obj", CurrentObjectiveAvailable = true };
        var withoutObj = new QuestProgressRef { Id = "11", CurrentObjective = "", CurrentObjectiveAvailable = false };

        var jsonWith = JsonSerializer.Serialize(withObj, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var jsonWithout = JsonSerializer.Serialize(withoutObj, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"current_objective_available\":true", jsonWith);
        Assert.Contains("\"current_objective_available\":false", jsonWithout);
    }

    [Fact]
    public void TitleAvailableFailsClosedForEmptyString()
    {
        var quest = new QuestProgressRef { Id = "10", Title = "", TitleAvailable = false };
        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"title_available\":false", json);
    }

    [Fact]
    public void SpecialOrderDonatedItemRefSerializesQualifiedItemIdAndModData()
    {
        var donated = new SpecialOrderDonatedItemRef
        {
            ItemId = "70",
            QualifiedItemId = "(O)70",
            Stack = 5,
            Quality = 1,
            ModData = new Dictionary<string, string> { ["donor"] = "player1" }
        };

        var json = JsonSerializer.Serialize(donated, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"qualified_item_id\"", json);
        Assert.Contains("\"(O)70\"", json);
        Assert.Contains("\"mod_data\"", json);
        Assert.Contains("\"donor\"", json);
    }

    [Fact]
    public void QuestCandidateRefSerializesSelectedObjectiveIndex()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "special_order:qiCrop",
            Family = "special_order",
            SelectedObjectiveIndex = 2
        };

        var json = JsonSerializer.Serialize(candidate, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"selected_objective_index\"", json);
        Assert.Contains("2", json);
        Assert.DoesNotContain("\"selected_objective_index\":-1", json);
    }

    [Fact]
    public void QuestCandidateBuilderBuildSpecialOrderPopulatesSelectedObjectiveIndex()
    {
        var order = new SpecialOrderProgressRef
        {
            QuestKey = "qiCrop", QuestState = "InProgress",
            Objectives = new[]
            {
                new SpecialOrderObjectiveProgressRef { Description = "obj0", CurrentCount = 5, MaxCount = 5, RuntimeType = "CollectObjective", PerTypeFields = new PerTypeObjectiveFields { Available = true } },
                new SpecialOrderObjectiveProgressRef { Description = "obj1", CurrentCount = 0, MaxCount = 10, RuntimeType = "CollectObjective", PerTypeFields = new PerTypeObjectiveFields { Available = true } }
            },
            Rewards = new[] { new SpecialOrderRewardProgressRef { RuntimeType = "MoneyReward", Available = true, Amount = 5000 } }
        };

        var candidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(new[] { order });

        Assert.Single(candidates);
        Assert.Equal(1, candidates[0].SelectedObjectiveIndex);
        Assert.Equal("collect_items", candidates[0].NextActionCategory);
    }

    [Fact]
    public void QuestCompilerEnvelopeSerializesSelectedObjectiveIndex()
    {
        var envelope = new QuestCompilerEnvelope
        {
            SelectedCandidateId = "special_order:qiCrop",
            SelectedObjectiveIndex = 2
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"selected_objective_index\"", json);
        Assert.Contains("2", json);
    }

    [Fact]
    public void BuildCompilerEnvelopeRejectsMalformedTargetCount()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest", QuestId = "10",
            RequiredTargetCount = 1
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { candidate }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedCandidateId: "quest:10:ItemDeliveryQuest",
            requestedTargetCount: "not_a_number");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_target_count_malformed", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void BuildCompilerEnvelopeRejectsMalformedCurrentCount()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest", QuestId = "10",
            CurrentProgressCount = 0
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { candidate }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedCandidateId: "quest:10:ItemDeliveryQuest",
            requestedCurrentCount: "NaN");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_current_count_malformed", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void BuildCompilerEnvelopeRejectsMalformedObjectiveIndex()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "special_order:qiCrop", QuestKey = "qiCrop",
            SelectedObjectiveIndex = 1
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            Array.Empty<QuestCandidateRef>(), new[] { candidate },
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedQuestKey: "qiCrop",
            requestedObjectiveIndex: "bad");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_selected_objective_index_malformed", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void BuildCompilerEnvelopeRejectsMismatchedCounts()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "quest:10:ItemDeliveryQuest", QuestId = "10",
            RequiredTargetCount = 5, CurrentProgressCount = 0
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { candidate }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedCandidateId: "quest:10:ItemDeliveryQuest",
            requestedTargetCount: "3",
            requestedCurrentCount: "1");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_target_count_mismatch", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void BuildCompilerEnvelopeRejectsMismatchedObjectiveIndex()
    {
        var candidate = new QuestCandidateRef
        {
            CandidateId = "special_order:qiCrop", QuestKey = "qiCrop",
            SelectedObjectiveIndex = 0
        };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            Array.Empty<QuestCandidateRef>(), new[] { candidate },
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedQuestKey: "qiCrop",
            requestedObjectiveIndex: "2");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_selected_objective_index_mismatch", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void BuildCompilerEnvelopeRejectsAmbiguousIdentity()
    {
        var candidate1 = new QuestCandidateRef { CandidateId = "quest:10:Quest", QuestId = "10" };
        var candidate2 = new QuestCandidateRef { CandidateId = "quest:11:Quest", QuestId = "10" };

        var envelope = QuestCandidateBuilder.BuildCompilerEnvelope(
            new[] { candidate1, candidate2 }, Array.Empty<QuestCandidateRef>(),
            Array.Empty<QuestProgressRef>(), Array.Empty<SpecialOrderProgressRef>(),
            requestedQuestId: "10");

        Assert.Empty(envelope.SelectedCandidateId);
        Assert.Contains("quest_candidate_ambiguous", envelope.ExecutorBlockReason);
    }

    [Fact]
    public void DonatedItemRefNoDonorIdField()
    {
        var json = JsonSerializer.Serialize(new SpecialOrderDonatedItemRef(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("donor_id", json);
        Assert.DoesNotContain("donor", json);
    }

    [Fact]
    public void QuestPlanEnvelopeSerializesSelectedObjectiveIndex()
    {
        var envelope = new StardewAI.Contracts.Execution.QuestPlanEnvelope
        {
            SelectedCandidateId = "special_order:qiCrop",
            SelectedObjectiveIndex = 1
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"selected_objective_index\"", json);
        Assert.Contains("1", json);
    }

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

    [Fact]
    public void QuestProgressRefSerializesRuntimeTypeAndPerTypeFields()
    {
        var quest = new QuestProgressRef
        {
            Id = "20",
            Title = "Kill Slimes",
            QuestType = 4,
            RuntimeType = "SlayMonsterQuest",
            Accepted = true,
            Completed = false,
            PerTypeFields = new PerTypeQuestFields
            {
                Available = true,
                MonsterName = "Slime",
                TargetCount = 10,
                CurrentCount = 3,
                IgnoreFarmMonsters = true
            }
        };

        var json = JsonSerializer.Serialize(quest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"runtime_type\"", json);
        Assert.Contains("\"SlayMonsterQuest\"", json);
        Assert.Contains("\"monster_name\"", json);
        Assert.Contains("\"Slime\"", json);
        Assert.Contains("\"target_count\"", json);
        Assert.Contains("\"current_count\"", json);
        Assert.Contains("\"ignore_farm_monsters\"", json);
    }

    [Fact]
    public void SpecialOrderObjectiveRewardProgressRefsSerializeRuntimeType()
    {
        var objective = new SpecialOrderObjectiveProgressRef
        {
            Description = "Ship 10 items",
            CurrentCount = 4,
            MaxCount = 10,
            RuntimeType = "ShipObjective",
            FailOnCompletion = false,
            PerTypeFields = new PerTypeObjectiveFields
            {
                Available = true,
                UseShipmentValue = true,
                AcceptableContextTagSets = new[] { "item_spring" }
            }
        };

        var reward = new SpecialOrderRewardProgressRef
        {
            RuntimeType = "MoneyReward",
            Available = true,
            Amount = 5000,
            Multiplier = 1.0f
        };

        var objJson = JsonSerializer.Serialize(objective, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var rewardJson = JsonSerializer.Serialize(reward, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"runtime_type\"", objJson);
        Assert.Contains("\"ShipObjective\"", objJson);
        Assert.Contains("\"fail_on_completion\"", objJson);
        Assert.Contains("\"use_shipment_value\"", objJson);

        Assert.Contains("\"runtime_type\"", rewardJson);
        Assert.Contains("\"MoneyReward\"", rewardJson);
        Assert.Contains("\"amount\"", rewardJson);
        Assert.Contains("\"multiplier\"", rewardJson);
    }

    [Fact]
    public void QuestCandidateRefSerializesBlockedDiagnosticsAndUnknownCost()
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
    public void ClassifyIslandByTagsIdentifiesIsland()
    {
        var method = typeof(ReadQuestProgressMapper).GetMethod(
            "ClassifyIslandByTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var classify = (Func<string?, int>)(arg => (int)method.Invoke(null, new object?[] { arg })!);

        Assert.Equal(1, classify("island"));
        Assert.Equal(1, classify("item_spring, island, item_berry"));
        Assert.Equal(0, classify("item_spring"));
        Assert.Equal(0, classify(null));
        Assert.Equal(0, classify(string.Empty));
    }

    private sealed class UnknownTestObjective : OrderObjective
    {
    }

    private sealed class UnknownTestReward : OrderReward
    {
    }
}
