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

}
