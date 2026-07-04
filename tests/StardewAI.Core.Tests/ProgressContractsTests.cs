using System.Text.Json;
using StardewAI.Contracts.State;

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
    public void UnavailableCompletedQuestsEnvelopeCarriesNoDefaultValue()
    {
        var field = new FieldEnvelope<object?>
        {
            Value = null,
            Status = FieldStatus.Unavailable,
            Source = new SourceRef { Kind = "unavailable", Path = "StardewValley.Farmer questLog contains current Quest.completed only" },
            Adapter = "not_implemented",
            ReadAtTick = 42,
            Confidence = 0,
            Reason = "no_verified_global_completed_quest_collection_found"
        };

        Assert.Null(field.Value);
        Assert.False(FieldEnvelopeValidator.IsReadableStatus(field.Status));
        Assert.Equal(0, field.Confidence);
        Assert.Equal("no_verified_global_completed_quest_collection_found", field.Reason);
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
}
