using System;
using System.Linq;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupQuestRewardFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_reward",
                "claimable_quest_reward=ready",
                "claimable_quest_reward=unavailable",
                reasons.ToArray());
        }

        var player = Game1.player;
        var home = Utility.getHomeOfFarmer(player);
        if (home is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_reward",
                "claimable_quest_reward=ready",
                "claimable_quest_reward=unavailable",
                "quest_reward_fixture_home_missing");
        }

        const string fixtureId = "stardewai.runtime.reward";
        foreach (var existing in player.questLog
                     .Where(quest => string.Equals(quest.id.Value, fixtureId, StringComparison.Ordinal))
                     .ToArray())
        {
            player.questLog.Remove(existing);
        }

        var quest = new Quest
        {
            _questTitle = "StardewAI reward fixture",
            _questDescription = "A completed isolated reward fixture.",
            _currentObjective = "Claim the reward."
        };
        quest.id.Value = fixtureId;
        quest.accepted.Value = true;
        quest.dailyQuest.Value = false;
        quest.daysLeft.Value = 0;
        quest.dayQuestAccepted.Value = Game1.Date.TotalDays;
        quest.moneyReward.Value = 750;
        quest.destroy.Value = false;
        quest.showNew.Value = false;
        player.questLog.Add(quest);
        quest.questComplete();

        if (Game1.currentLocation is not null)
        {
            Game1.currentLocation.currentEvent = null;
        }
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.currentLocation = home;
        player.currentLocation = home;
        home.currentEvent = null;
        player.Position = Utility.PointToVector2(home.GetPlayerBedSpot()) * Game1.tileSize;
        player.UsingTool = false;
        player.canMove = true;

        var verified = quest.ShouldDisplayAsComplete() &&
            quest.HasMoneyReward() &&
            quest.GetMoneyReward() == 750 &&
            player.questLog.Contains(quest);
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
            PrimitiveKind = "debug_setup_quest_reward",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_completed_quest_money_reward_installed" }
                : new[] { "quest_reward_fixture_receipt_mismatch" },
            RequestedEffect = "claimable_quest_reward=ready",
            ObservedEffect = "quest_id=" + fixtureId +
                ";money_before=" + player.Money +
                ";reward=" + quest.GetMoneyReward() +
                ";claimable=" + verified.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "quest_reward_fixture_receipt_mismatch" }
        };
    }
}
