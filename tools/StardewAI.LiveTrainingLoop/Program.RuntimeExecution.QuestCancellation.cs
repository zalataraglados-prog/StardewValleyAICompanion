using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyQuestCancellationRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("quest.cancel" or "executor.cancel_quest" or "debug.setup_quest_cancellation"))
            return;
        request.QuestCancellationFingerprint = ReadQueueParameterString(item, "quest_cancellation_fingerprint");
        request.QuestCancelReason = ReadQueueParameterString(item, "quest_cancel_reason");
        request.ConfirmQuestCancel = ReadQueueParameterBool(item, "confirm_quest_cancel");
        request.QuestExpectedAcceptedBefore = ReadNullableBoolQueueParameter(item, "quest_expected_accepted_before");
        request.QuestExpectedCompletedBefore = ReadNullableBoolQueueParameter(item, "quest_expected_completed_before");
        request.QuestExpectedDailyQuest = ReadNullableBoolQueueParameter(item, "quest_expected_daily_quest");
        request.QuestExpectedDayAccepted = ReadQueueParameterInt(item, "quest_expected_day_accepted");
        request.QuestExpectedDaysLeft = ReadQueueParameterInt(item, "quest_expected_days_left");
        request.QuestLogCountBefore = ReadQueueParameterInt(item, "quest_log_count_before");
        request.QuestLogCountAfter = ReadQueueParameterInt(item, "quest_log_count_after");
        request.QuestAcceptedDailyBefore = ReadNullableBoolQueueParameter(item, "quest_accepted_daily_before");
        request.QuestAcceptedDailyAfter = ReadNullableBoolQueueParameter(item, "quest_accepted_daily_after");
        request.QuestResetsAcceptedDailyQuest = ReadNullableBoolQueueParameter(item, "quest_resets_accepted_daily_quest");
        request.QuestCancellationFixtureCase = ReadQueueParameterString(item, "quest_cancellation_fixture_case");
    }
}
