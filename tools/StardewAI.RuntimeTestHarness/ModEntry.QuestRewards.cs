using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo QuestLogPagesField =
        typeof(QuestLog).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(QuestLog).FullName, "pages");
    private static readonly FieldInfo QuestLogCurrentPageField =
        typeof(QuestLog).GetField("currentPage", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(QuestLog).FullName, "currentPage");
    private static readonly FieldInfo QuestLogQuestPageField =
        typeof(QuestLog).GetField("questPage", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(QuestLog).FullName, "questPage");
    private static readonly FieldInfo QuestLogShownQuestField =
        typeof(QuestLog).GetField("_shownQuest", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(QuestLog).FullName, "_shownQuest");

    private void StartQuestRewardClaim(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!string.Equals(request.QuestFamily, "ordinary", StringComparison.Ordinal))
            reasons.Add("quest_reward_family_mismatch");
        if (string.IsNullOrWhiteSpace(request.QuestRewardFingerprint))
            reasons.Add("quest_reward_fingerprint_required");
        if (!request.QuestMoneyRewardExpected.HasValue || request.QuestMoneyRewardExpected.Value <= 0)
            reasons.Add("quest_reward_amount_invalid");
        if (!request.QuestExpectedMoneyBefore.HasValue)
            reasons.Add("quest_reward_expected_money_before_required");
        if (Game1.activeClickableMenu is not null)
            reasons.Add("quest_reward_requires_clear_menu");
        if (request.QuestExpectedMoneyBefore.HasValue && Game1.player.Money != request.QuestExpectedMoneyBefore.Value)
            reasons.Add("quest_reward_money_before_drifted");

        var matches = Game1.player.questLog
            .Where(quest => QuestRewardRuntimeTypeMatches(quest, request.QuestRuntimeType))
            .Where(quest => string.Equals(
                QuestRewardFingerprint(quest, request.QuestRuntimeType),
                request.QuestRewardFingerprint,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            reasons.Add(matches.Length == 0 ? "quest_reward_exact_quest_missing" : "quest_reward_identity_ambiguous");
        var quest = matches.Length == 1 ? matches[0] : null;
        if (quest is not null)
        {
            if (quest.IsHidden()) reasons.Add("quest_reward_quest_hidden");
            if (!quest.ShouldDisplayAsComplete()) reasons.Add("quest_reward_quest_not_complete");
            if (!quest.HasMoneyReward()) reasons.Add("quest_reward_not_claimable");
            if (quest.GetMoneyReward() != request.QuestMoneyRewardExpected)
                reasons.Add("quest_reward_amount_drifted");
        }

        if (reasons.Count > 0 || quest is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "claim_quest_reward",
                QuestRewardRequestedEffect(request),
                QuestRewardObservedEffect(request, quest),
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            return;
        }

        var menu = new QuestLog();
        Game1.activeClickableMenu = menu;
        activeQuestRewardClaim = new ActiveQuestRewardClaim(
            pending,
            menu,
            quest,
            Game1.player.Money,
            Game1.stats.QuestsCompleted,
            request.QuestMoneyRewardExpected!.Value);
    }

    private void TickQuestRewardClaimSafely()
    {
        var active = activeQuestRewardClaim;
        if (active is null) return;
        try
        {
            TickQuestRewardClaim(active);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Quest reward claim failed and was blocked: {ex}", LogLevel.Error);
            CompleteBlockedQuestRewardClaim(active, "quest_reward_executor_exception:" + ex.GetType().Name);
        }
    }

    private void TickQuestRewardClaim(ActiveQuestRewardClaim active)
    {
        active.ElapsedTicks++;
        if (active.ElapsedTicks > 300)
        {
            CompleteBlockedQuestRewardClaim(active, "quest_reward_native_menu_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.activeClickableMenu, active.Menu))
        {
            CompleteBlockedQuestRewardClaim(active, "quest_reward_native_menu_replaced");
            return;
        }

        var currentPage = (int)QuestLogCurrentPageField.GetValue(active.Menu)!;
        var questPage = (int)QuestLogQuestPageField.GetValue(active.Menu)!;
        var shown = QuestLogShownQuestField.GetValue(active.Menu) as IQuest;

        if (!active.RewardClicked)
        {
            var pages = (List<List<IQuest>>)QuestLogPagesField.GetValue(active.Menu)!;
            var target = FindQuestRewardMenuPosition(pages, active.Quest);
            if (!target.HasValue)
            {
                CompleteBlockedQuestRewardClaim(active, "quest_reward_exact_menu_row_missing");
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
            if (!ReferenceEquals(shown, active.Quest) || questPage != target.Value.Row)
            {
                CompleteBlockedQuestRewardClaim(active, "quest_reward_selected_menu_identity_drifted");
                return;
            }

            var rewardBounds = active.Menu.rewardBox.bounds;
            var timedTitleOffset = active.Quest.IsTimedQuest() && active.Quest.GetDaysLeft() > 0 &&
                SpriteText.getWidthOfString(active.Quest.GetName()) > active.Menu.width / 2
                    ? 48
                    : 0;
            active.Menu.receiveLeftClick(
                rewardBounds.Center.X,
                rewardBounds.Center.Y + timedTitleOffset);
            active.RewardClicked = true;
            return;
        }

        if (!active.ReturnClicked)
        {
            if (Game1.player.Money != active.MoneyBefore + active.RewardAmount ||
                active.Quest.moneyReward.Value != 0 ||
                !active.Quest.destroy.Value)
            {
                CompleteBlockedQuestRewardClaim(active, "quest_reward_native_receipt_mismatch");
                return;
            }
            if (Game1.player.questLog.Contains(active.Quest))
            {
                if (questPage == -1 || !ReferenceEquals(shown, active.Quest))
                {
                    CompleteBlockedQuestRewardClaim(active, "quest_reward_native_leave_page_identity_drifted");
                    return;
                }
                var bounds = active.Menu.backButton.bounds;
                active.Menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
            }
            active.ReturnClicked = true;
            return;
        }

        if (Game1.player.questLog.Contains(active.Quest))
        {
            CompleteBlockedQuestRewardClaim(active, "quest_reward_native_leave_page_did_not_remove_quest");
            return;
        }
        var close = active.Menu.upperRightCloseButton?.bounds;
        if (close.HasValue)
        {
            active.Menu.receiveLeftClick(close.Value.Center.X, close.Value.Center.Y);
        }
        else
        {
            active.Menu.exitThisMenu();
        }
        if (Game1.activeClickableMenu is not null) return;
        CompleteQuestRewardClaim(active);
    }

    private void CompleteQuestRewardClaim(ActiveQuestRewardClaim active)
    {
        activeQuestRewardClaim = null;
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
            PrimitiveKind = "claim_quest_reward",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_QuestLog_exact_row_selected",
                "native_QuestLog_rewardBox_receiveLeftClick_applied",
                "native_Quest_OnMoneyRewardClaimed_effects_observed",
                "native_Quest_OnLeaveQuestPage_removal_observed",
                "exact_money_delta_verified"
            },
            RequestedEffect = QuestRewardRequestedEffect(request),
            ObservedEffect = QuestRewardObservedEffect(request, active.Quest),
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestId = request.QuestId,
            QuestCompletedBefore = true,
            QuestCompletedAfter = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.money", Before = active.MoneyBefore.ToString(), After = Game1.player.Money.ToString() },
                new SimulatedFactChange { Path = "quests.claimable_rewards[" + request.QuestRewardFingerprint + "].present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "stats.quests_completed", Before = active.QuestsCompletedBefore.ToString(), After = Game1.stats.QuestsCompleted.ToString() }
            }
        });
    }

    private void CompleteBlockedQuestRewardClaim(ActiveQuestRewardClaim active, params string[] reasons)
    {
        activeQuestRewardClaim = null;
        if (ReferenceEquals(Game1.activeClickableMenu, active.Menu)) active.Menu.exitThisMenu();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "claim_quest_reward",
            QuestRewardRequestedEffect(active.Pending.Request),
            QuestRewardObservedEffect(active.Pending.Request, active.Quest),
            reasons));
    }

    private static (int Page, int Row)? FindQuestRewardMenuPosition(List<List<IQuest>> pages, Quest quest)
    {
        for (var page = 0; page < pages.Count; page++)
        for (var row = 0; row < pages[page].Count; row++)
            if (ReferenceEquals(pages[page][row], quest)) return (page, row);
        return null;
    }

    private static bool QuestRewardRuntimeTypeMatches(Quest quest, string runtimeType) =>
        string.Equals(quest.GetType().Name, runtimeType, StringComparison.Ordinal) ||
        string.Equals(quest.GetType().FullName, runtimeType, StringComparison.Ordinal);

    private static string QuestRewardFingerprint(Quest quest, string runtimeType) =>
        QuestRewardClaimIdentity.Compute(quest.id.Value, runtimeType, quest._questTitle, quest.moneyReward.Value, quest.dayQuestAccepted.Value, quest.dailyQuest.Value);

    private static string QuestRewardRequestedEffect(TrainingExecutionRequest request) =>
        "quest_reward_fingerprint=" + request.QuestRewardFingerprint + ";money_delta=" + request.QuestMoneyRewardExpected;

    private static string QuestRewardObservedEffect(TrainingExecutionRequest request, Quest? quest) =>
        "quest_reward_fingerprint=" + request.QuestRewardFingerprint +
        ";money=" + Game1.player.Money +
        ";quest_present=" + (quest is not null && Game1.player.questLog.Contains(quest)).ToString().ToLowerInvariant() +
        ";quest_money_reward=" + (quest?.moneyReward.Value.ToString() ?? "missing") +
        ";quest_destroy=" + (quest?.destroy.Value.ToString().ToLowerInvariant() ?? "missing") +
        ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");

    private sealed class ActiveQuestRewardClaim
    {
        public ActiveQuestRewardClaim(PendingExecution pending, QuestLog menu, Quest quest, int moneyBefore, uint questsCompletedBefore, int rewardAmount)
        {
            Pending = pending;
            Menu = menu;
            Quest = quest;
            MoneyBefore = moneyBefore;
            QuestsCompletedBefore = questsCompletedBefore;
            RewardAmount = rewardAmount;
        }

        public PendingExecution Pending { get; }
        public QuestLog Menu { get; }
        public Quest Quest { get; }
        public int MoneyBefore { get; }
        public uint QuestsCompletedBefore { get; }
        public int RewardAmount { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public bool RewardClicked { get; set; }
        public bool ReturnClicked { get; set; }
    }
}
