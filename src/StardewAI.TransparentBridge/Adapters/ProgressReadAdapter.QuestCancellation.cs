using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ProgressQuestReadAdapter
{
    private const string QuestCancellationNativeContract =
        "QuestLog_row_receiveLeftClick->cancelQuestButton_receiveLeftClick->accepted_false->questLog_remove->same_day_daily_acceptedDailyQuest_false";

    private QuestCancellationProjectionRef? ReadQuestCancellationProjection(Farmer? player)
    {
        if (player is null) return null;
        var questLogCount = player.questLog.Count;
        var acceptedDailyQuest = player.acceptedDailyQuest.Value;
        var today = Game1.Date.TotalDays;
        var rows = player.questLog.Select(quest =>
        {
            var mapped = mapper.MapQuest(quest);
            var diagnostics = new System.Collections.Generic.List<string>();
            if (mapped.Hidden) diagnostics.Add("quest_hidden_from_native_quest_log");
            if (!mapped.Accepted) diagnostics.Add("quest_not_accepted");
            if (mapped.Completed) diagnostics.Add("quest_already_completed");
            if (!quest.CanBeCancelled()) diagnostics.Add("quest_native_cancellation_disabled");
            if (mapped.Destroy) diagnostics.Add("quest_pending_native_removal");
            var eligible = diagnostics.Count == 0;
            var resetsDailyFlag = mapped.DailyQuest && mapped.DayQuestAccepted == today;
            return new QuestCancellationCandidateRef
            {
                CancellationFingerprint = QuestCancellationIdentity.Compute(mapped),
                Quest = mapped,
                Eligible = eligible,
                NativeButtonVisible = !mapped.Completed && quest.CanBeCancelled(),
                ResetsAcceptedDailyQuest = resetsDailyFlag,
                ExpectedAcceptedDailyQuestAfter = resetsDailyFlag ? false : acceptedDailyQuest,
                ExpectedQuestLogCountAfter = questLogCount - 1,
                Status = eligible ? "ready" : "blocked",
                BlockedDiagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToArray()
            };
        }).OrderBy(row => row.Quest.Id, StringComparer.Ordinal)
            .ThenBy(row => row.CancellationFingerprint, StringComparer.Ordinal)
            .ToArray();

        return new QuestCancellationProjectionRef
        {
            SchemaVersion = "quest_cancellation.v1",
            ProjectionStatus = "complete_locked_base_1.6.15",
            InvocationPolicy = "player_command_only",
            NativeContract = QuestCancellationNativeContract,
            CurrentTotalDays = today,
            QuestLogCountBefore = questLogCount,
            AcceptedDailyQuestBefore = acceptedDailyQuest,
            Candidates = rows
        };
    }
}
