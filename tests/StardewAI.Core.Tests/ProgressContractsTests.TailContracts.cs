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
    }}
