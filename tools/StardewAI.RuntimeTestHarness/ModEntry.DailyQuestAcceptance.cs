using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteAcceptDailyQuest(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var quest = Game1.questOfTheDay;
        var menu = Game1.activeClickableMenu as Billboard;
        var fingerprintBefore = quest is null ? string.Empty : DailyQuestFingerprint(quest);
        var presentBefore = quest is not null && Game1.player.questLog.Contains(quest);
        if (request.QuestInteractionKind != "accept_daily")
        {
            reasons.Add("daily_quest_interaction_kind_mismatch");
        }
        if (string.IsNullOrWhiteSpace(request.QuestOfferFingerprint) ||
            !string.Equals(request.QuestOfferFingerprint, fingerprintBefore, StringComparison.Ordinal))
        {
            reasons.Add("daily_quest_offer_fingerprint_drifted");
        }
        if (quest is null ||
            !string.Equals(quest.GetType().Name, request.QuestRuntimeType, StringComparison.Ordinal))
        {
            reasons.Add("daily_quest_runtime_type_drifted");
        }
        if (menu is null)
        {
            reasons.Add("daily_quest_billboard_menu_not_open");
        }
        else
        {
            var dailyBoard = Helper.Reflection
                .GetField<bool>(menu, "dailyQuestBoard")
                .GetValue();
            if (!dailyBoard)
            {
                reasons.Add("daily_quest_billboard_mode_required");
            }
            if (!menu.acceptQuestButton.visible)
            {
                reasons.Add("daily_quest_accept_button_not_visible");
            }
        }
        if (!Game1.CanAcceptDailyQuest())
        {
            reasons.Add("daily_quest_native_can_accept_false");
        }
        if (presentBefore)
        {
            reasons.Add("daily_quest_offer_already_in_quest_log");
        }

        if (reasons.Count > 0 || quest is null || menu is null)
        {
            return BlockedWithPrimitive(
                request,
                "accept_daily_quest",
                "native_daily_quest_accepted=true",
                DailyQuestAcceptanceObservedEffect(quest),
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var acceptedBefore = Game1.player.acceptedDailyQuest.Value;
        var bounds = menu.acceptQuestButton.bounds;
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);

        var fingerprintAfter = DailyQuestFingerprint(quest);
        var presentAfter = Game1.player.questLog.Contains(quest);
        var verified = presentAfter &&
            Game1.player.acceptedDailyQuest.Value &&
            quest.accepted.Value &&
            quest.dailyQuest.Value &&
            quest.canBeCancelled.Value &&
            quest.dayQuestAccepted.Value == Game1.Date.TotalDays &&
            quest.daysLeft.Value == 2 &&
            string.Equals(fingerprintBefore, fingerprintAfter, StringComparison.Ordinal);
        var verificationReasons = verified
            ? new[]
            {
                "native_Billboard_receiveLeftClick_applied",
                "exact_offer_reference_added_to_actor_quest_log",
                "acceptedDailyQuest_true",
                "daily_quest_native_two_day_deadline_verified"
            }
            : new[] { "daily_quest_native_acceptance_receipt_mismatch" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "accept_daily_quest",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "native_daily_quest_accepted=true",
            ObservedEffect = DailyQuestAcceptanceObservedEffect(quest),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestId = request.QuestId,
            QuestPresentBefore = presentBefore,
            QuestPresentAfter = presentAfter,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.accepted_daily_quest",
                    Before = acceptedBefore.ToString().ToLowerInvariant(),
                    After = Game1.player.acceptedDailyQuest.Value.ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "quests.daily_quest_offer.quest.accepted",
                    Before = "false",
                    After = quest.accepted.Value.ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "quests.daily_quest_offer.quest.days_left",
                    Before = "0",
                    After = quest.daysLeft.Value.ToString()
                }
            }
        };
    }

    private static string DailyQuestFingerprint(StardewValley.Quests.Quest quest) =>
        QuestOfferIdentity.Compute(
            quest.id.Value,
            quest.GetType().Name,
            quest._questTitle,
            quest._questDescription,
            quest._currentObjective);

    private static string DailyQuestAcceptanceObservedEffect(StardewValley.Quests.Quest? quest) =>
        "acceptedDailyQuest=" + Game1.player.acceptedDailyQuest.Value.ToString().ToLowerInvariant() +
        ";quest_present=" + (quest is not null && Game1.player.questLog.Contains(quest)).ToString().ToLowerInvariant() +
        ";quest_accepted=" + (quest?.accepted.Value ?? false).ToString().ToLowerInvariant() +
        ";days_left=" + (quest?.daysLeft.Value ?? -1);
}
