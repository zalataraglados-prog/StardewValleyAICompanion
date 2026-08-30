using System;
using System.Linq;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupQuestCancellationFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var fixtureCase = request.QuestCancellationFixtureCase;
        if (fixtureCase is not ("same_day_daily" or "ordinary_preserve_daily_flag" or "non_cancellable"))
            reasons.Add("quest_cancellation_fixture_case_invalid");
        if (reasons.Count > 0)
            return BlockedWithPrimitive(request, "debug_setup_quest_cancellation",
                "quest_cancellation_fixture=ready", "quest_cancellation_fixture=blocked", reasons.ToArray());

        const string prefix = "stardewai.runtime.cancel.";
        foreach (var existing in Game1.player.questLog.Where(quest => quest.id.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            Game1.player.questLog.Remove(existing);

        var isDaily = fixtureCase == "same_day_daily";
        var cancellable = fixtureCase != "non_cancellable";
        var quest = new Quest
        {
            _questTitle = "StardewAI cancellation fixture " + fixtureCase,
            _questDescription = "An isolated ordinary quest cancellation fixture.",
            _currentObjective = "Cancel this fixture through the native QuestLog."
        };
        quest.id.Value = prefix + fixtureCase;
        quest.questType.Value = 0;
        quest.accepted.Value = true;
        quest.completed.Value = false;
        quest.dailyQuest.Value = isDaily;
        quest.canBeCancelled.Value = cancellable;
        quest.dayQuestAccepted.Value = isDaily ? Game1.Date.TotalDays : Game1.Date.TotalDays - 1;
        quest.daysLeft.Value = isDaily ? 2 : 0;
        quest.moneyReward.Value = 0;
        quest.destroy.Value = false;
        quest.showNew.Value = true;
        Game1.player.questLog.Add(quest);
        Game1.player.acceptedDailyQuest.Set(newValue: true);

        var home = Utility.getHomeOfFarmer(Game1.player);
        if (Game1.currentLocation is not null) Game1.currentLocation.currentEvent = null;
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        if (home is not null)
        {
            Game1.currentLocation = home;
            Game1.player.currentLocation = home;
            home.currentEvent = null;
            Game1.player.Position = Utility.PointToVector2(home.GetPlayerBedSpot()) * Game1.tileSize;
        }
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;

        var verified = Game1.player.questLog.Contains(quest) && quest.accepted.Value &&
            quest.CanBeCancelled() == cancellable && Game1.player.acceptedDailyQuest.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_quest_cancellation",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_ordinary_quest_cancellation_fixture_installed" }
                : new[] { "quest_cancellation_fixture_receipt_mismatch" },
            RequestedEffect = "quest_cancellation_fixture=" + fixtureCase,
            ObservedEffect = "quest_id=" + quest.id.Value + ";daily=" + isDaily.ToString().ToLowerInvariant() +
                ";cancellable=" + quest.CanBeCancelled().ToString().ToLowerInvariant() +
                ";accepted_daily_quest=" + Game1.player.acceptedDailyQuest.Value.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "quest_cancellation_fixture_receipt_mismatch" }
        };
    }
}
