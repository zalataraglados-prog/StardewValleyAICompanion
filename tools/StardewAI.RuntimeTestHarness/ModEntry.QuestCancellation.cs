using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string QuestCancellationRuntimeNativeContract =
        "QuestLog_row_receiveLeftClick->cancelQuestButton_receiveLeftClick->accepted_false->questLog_remove->same_day_daily_acceptedDailyQuest_false";

    private void StartQuestCancellation(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!string.Equals(request.QuestFamily, "ordinary", StringComparison.Ordinal))
            reasons.Add("quest_cancellation_family_mismatch");
        if (string.IsNullOrWhiteSpace(request.QuestCancellationFingerprint))
            reasons.Add("quest_cancellation_fingerprint_required");
        if (string.IsNullOrWhiteSpace(request.QuestCancelReason) || request.ConfirmQuestCancel != true)
            reasons.Add("quest_cancellation_explicit_reason_and_confirmation_required");
        if (request.NativeContract != QuestCancellationRuntimeNativeContract)
            reasons.Add("quest_cancellation_native_contract_mismatch");
        if (Game1.activeClickableMenu is not null)
            reasons.Add("quest_cancellation_requires_clear_menu");

        var matches = Game1.player.questLog.Where(quest =>
                QuestRewardRuntimeTypeMatches(quest, request.QuestRuntimeType) &&
                string.Equals(QuestCancellationFingerprint(quest, request.QuestRuntimeType),
                    request.QuestCancellationFingerprint, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            reasons.Add(matches.Length == 0 ? "quest_cancellation_exact_quest_missing" : "quest_cancellation_identity_ambiguous");
        var quest = matches.Length == 1 ? matches[0] : null;
        if (quest is not null)
        {
            if (quest.IsHidden()) reasons.Add("quest_cancellation_quest_hidden");
            if (!quest.accepted.Value) reasons.Add("quest_cancellation_quest_not_accepted");
            if (quest.completed.Value) reasons.Add("quest_cancellation_quest_completed");
            if (!quest.CanBeCancelled()) reasons.Add("quest_cancellation_quest_not_cancellable");
            if (quest.destroy.Value) reasons.Add("quest_cancellation_quest_pending_removal");
            if (request.QuestId != quest.id.Value || request.QuestExpectedAcceptedBefore != quest.accepted.Value ||
                request.QuestExpectedCompletedBefore != quest.completed.Value ||
                request.QuestExpectedDailyQuest != quest.dailyQuest.Value ||
                request.QuestExpectedDayAccepted != quest.dayQuestAccepted.Value ||
                request.QuestExpectedDaysLeft != quest.daysLeft.Value)
                reasons.Add("quest_cancellation_quest_prestate_drifted");
        }
        var resetsDaily = quest is not null && quest.dailyQuest.Value && quest.dayQuestAccepted.Value == Game1.Date.TotalDays;
        var expectedDailyAfter = resetsDaily ? false : Game1.player.acceptedDailyQuest.Value;
        if (request.QuestLogCountBefore != Game1.player.questLog.Count ||
            request.QuestLogCountAfter != Game1.player.questLog.Count - 1 ||
            request.QuestAcceptedDailyBefore != Game1.player.acceptedDailyQuest.Value ||
            request.QuestAcceptedDailyAfter != expectedDailyAfter ||
            request.QuestResetsAcceptedDailyQuest != resetsDaily)
            reasons.Add("quest_cancellation_count_or_daily_flag_projection_drifted");

        if (reasons.Count > 0 || quest is null)
        {
            pending.Completion.SetResult(BlockedQuestCancellation(request, quest, reasons.ToArray()));
            return;
        }

        var menu = new QuestLog();
        Game1.activeClickableMenu = menu;
        activeQuestCancellation = new ActiveQuestCancellation(
            pending,
            menu,
            quest,
            Game1.player.questLog.Count,
            Game1.player.acceptedDailyQuest.Value,
            Game1.player.Money,
            Game1.stats.QuestsCompleted,
            Game1.player.team.specialOrders.Count,
            expectedDailyAfter);
    }

    private void TickQuestCancellationSafely()
    {
        var active = activeQuestCancellation;
        if (active is null) return;
        try
        {
            TickQuestCancellation(active);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Quest cancellation failed and was blocked: {ex}", LogLevel.Error);
            CompleteBlockedQuestCancellation(active, "quest_cancellation_executor_exception:" + ex.GetType().Name);
        }
    }

    private void TickQuestCancellation(ActiveQuestCancellation active)
    {
        active.ElapsedTicks++;
        if (active.ElapsedTicks > 300)
        {
            CompleteBlockedQuestCancellation(active, "quest_cancellation_native_menu_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.activeClickableMenu, active.Menu))
        {
            CompleteBlockedQuestCancellation(active, "quest_cancellation_native_menu_replaced");
            return;
        }

        var currentPage = (int)QuestLogCurrentPageField.GetValue(active.Menu)!;
        var questPage = (int)QuestLogQuestPageField.GetValue(active.Menu)!;
        var shown = QuestLogShownQuestField.GetValue(active.Menu) as IQuest;
        if (!active.CancelClicked)
        {
            var pages = (List<List<IQuest>>)QuestLogPagesField.GetValue(active.Menu)!;
            var target = FindQuestMenuPosition(pages, active.Quest);
            if (!target.HasValue)
            {
                CompleteBlockedQuestCancellation(active, "quest_cancellation_exact_menu_row_missing");
                return;
            }
            if (questPage == -1 && currentPage < target.Value.Page)
            {
                var bounds = active.Menu.forwardButton.bounds;
                active.Menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
                return;
            }
            if (questPage == -1 && currentPage > target.Value.Page)
            {
                var bounds = active.Menu.backButton.bounds;
                active.Menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
                return;
            }
            if (questPage == -1)
            {
                var bounds = active.Menu.questLogButtons[target.Value.Row].bounds;
                active.Menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
                return;
            }
            if (!ReferenceEquals(shown, active.Quest) || questPage != target.Value.Row || !active.Quest.CanBeCancelled())
            {
                CompleteBlockedQuestCancellation(active, "quest_cancellation_selected_menu_identity_drifted");
                return;
            }
            var cancelBounds = active.Menu.cancelQuestButton.bounds;
            active.Menu.receiveLeftClick(cancelBounds.Center.X, cancelBounds.Center.Y);
            active.CancelClicked = true;
            return;
        }

        if (Game1.player.questLog.Contains(active.Quest) || active.Quest.accepted.Value ||
            Game1.player.questLog.Count != active.QuestLogCountBefore - 1 ||
            Game1.player.acceptedDailyQuest.Value != active.ExpectedAcceptedDailyAfter ||
            Game1.player.Money != active.MoneyBefore || Game1.stats.QuestsCompleted != active.QuestsCompletedBefore ||
            Game1.player.team.specialOrders.Count != active.SpecialOrderCountBefore)
        {
            CompleteBlockedQuestCancellation(active, "quest_cancellation_native_receipt_mismatch");
            return;
        }

        var close = active.Menu.upperRightCloseButton?.bounds;
        if (close.HasValue) active.Menu.receiveLeftClick(close.Value.Center.X, close.Value.Center.Y);
        else active.Menu.exitThisMenu();
        if (Game1.activeClickableMenu is not null) return;
        CompleteQuestCancellation(active);
    }

    private void CompleteQuestCancellation(ActiveQuestCancellation active)
    {
        activeQuestCancellation = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "cancel_quest",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_QuestLog_exact_row_selected",
                "native_QuestLog_cancelQuestButton_receiveLeftClick_applied",
                "ordinary_quest_removed_and_accepted_false_verified",
                "same_day_daily_acceptedDailyQuest_side_effect_verified",
                "money_stats_and_special_orders_unchanged"
            },
            RequestedEffect = QuestCancellationRequestedEffect(request),
            ObservedEffect = QuestCancellationObservedEffect(request, active.Quest),
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestId = request.QuestId,
            QuestCancellationFingerprint = request.QuestCancellationFingerprint,
            QuestCancelReason = request.QuestCancelReason,
            QuestPresentBefore = true,
            QuestPresentAfter = false,
            QuestCompletedBefore = false,
            QuestCompletedAfter = false,
            QuestAcceptedBefore = true,
            QuestAcceptedAfter = false,
            QuestAcceptedDailyBefore = active.AcceptedDailyBefore,
            QuestAcceptedDailyAfter = Game1.player.acceptedDailyQuest.Value,
            QuestLogCountBefore = active.QuestLogCountBefore,
            QuestLogCountAfter = Game1.player.questLog.Count,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "quests.cancellation_candidates[" + request.QuestCancellationFingerprint + "].present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "quests.cancelled.accepted", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "player.accepted_daily_quest", Before = active.AcceptedDailyBefore.ToString().ToLowerInvariant(), After = Game1.player.acceptedDailyQuest.Value.ToString().ToLowerInvariant() }
            }
        });
    }

    private void CompleteBlockedQuestCancellation(ActiveQuestCancellation active, params string[] reasons)
    {
        activeQuestCancellation = null;
        if (ReferenceEquals(Game1.activeClickableMenu, active.Menu)) active.Menu.exitThisMenu();
        active.Pending.Completion.SetResult(BlockedQuestCancellation(active.Pending.Request, active.Quest, reasons));
    }

    private static TrainingExecutionResult BlockedQuestCancellation(
        TrainingExecutionRequest request,
        Quest? quest,
        params string[] reasons)
    {
        var result = BlockedWithPrimitive(
            request,
            "cancel_quest",
            QuestCancellationRequestedEffect(request),
            QuestCancellationObservedEffect(request, quest),
            reasons.Distinct(StringComparer.Ordinal).ToArray());
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestCancellationFingerprint = request.QuestCancellationFingerprint;
        result.QuestCancelReason = request.QuestCancelReason;
        result.QuestPresentBefore = quest is not null && Game1.player.questLog.Contains(quest);
        result.QuestPresentAfter = result.QuestPresentBefore;
        result.QuestAcceptedBefore = quest?.accepted.Value;
        result.QuestAcceptedAfter = quest?.accepted.Value;
        result.QuestAcceptedDailyBefore = Game1.player.acceptedDailyQuest.Value;
        result.QuestAcceptedDailyAfter = Game1.player.acceptedDailyQuest.Value;
        result.QuestLogCountBefore = Game1.player.questLog.Count;
        result.QuestLogCountAfter = Game1.player.questLog.Count;
        return result;
    }

    private static string QuestCancellationFingerprint(Quest quest, string runtimeType) =>
        QuestCancellationIdentity.Compute(
            quest.id.Value,
            runtimeType,
            quest._questTitle,
            quest._currentObjective,
            quest.questType.Value,
            quest.accepted.Value,
            quest.completed.Value,
            quest.IsHidden(),
            quest.dailyQuest.Value,
            quest.CanBeCancelled(),
            quest.dayQuestAccepted.Value,
            quest.daysLeft.Value,
            quest.moneyReward.Value,
            quest.destroy.Value);

    private static string QuestCancellationRequestedEffect(TrainingExecutionRequest request) =>
        "quest_cancellation_fingerprint=" + request.QuestCancellationFingerprint +
        ";quest_removed=true;accepted=false;accepted_daily_quest=" + request.QuestAcceptedDailyAfter;

    private static string QuestCancellationObservedEffect(TrainingExecutionRequest request, Quest? quest) =>
        "quest_cancellation_fingerprint=" + request.QuestCancellationFingerprint +
        ";quest_present=" + (quest is not null && Game1.player.questLog.Contains(quest)).ToString().ToLowerInvariant() +
        ";quest_accepted=" + (quest?.accepted.Value.ToString().ToLowerInvariant() ?? "missing") +
        ";accepted_daily_quest=" + Game1.player.acceptedDailyQuest.Value.ToString().ToLowerInvariant() +
        ";quest_log_count=" + Game1.player.questLog.Count +
        ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");

    private sealed class ActiveQuestCancellation
    {
        public ActiveQuestCancellation(
            PendingExecution pending,
            QuestLog menu,
            Quest quest,
            int questLogCountBefore,
            bool acceptedDailyBefore,
            int moneyBefore,
            uint questsCompletedBefore,
            int specialOrderCountBefore,
            bool expectedAcceptedDailyAfter)
        {
            Pending = pending;
            Menu = menu;
            Quest = quest;
            QuestLogCountBefore = questLogCountBefore;
            AcceptedDailyBefore = acceptedDailyBefore;
            MoneyBefore = moneyBefore;
            QuestsCompletedBefore = questsCompletedBefore;
            SpecialOrderCountBefore = specialOrderCountBefore;
            ExpectedAcceptedDailyAfter = expectedAcceptedDailyAfter;
        }

        public PendingExecution Pending { get; }
        public QuestLog Menu { get; }
        public Quest Quest { get; }
        public int QuestLogCountBefore { get; }
        public bool AcceptedDailyBefore { get; }
        public int MoneyBefore { get; }
        public uint QuestsCompletedBefore { get; }
        public int SpecialOrderCountBefore { get; }
        public bool ExpectedAcceptedDailyAfter { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public bool CancelClicked { get; set; }
    }
}
