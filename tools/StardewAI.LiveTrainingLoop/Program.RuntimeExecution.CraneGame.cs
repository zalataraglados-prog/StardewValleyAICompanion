using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyCraneGameRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.play_crane_game" or "debug.setup_crane_game"))
            return;
        request.CraneProjectionFingerprint = ReadQueueParameterString(item, "crane_projection_fingerprint");
        request.CraneActionRaw = ReadQueueParameterString(item, "crane_action_raw");
        request.CraneActionToken = ReadQueueParameterString(item, "crane_action_token");
        request.CraneYesResponseKey = ReadQueueParameterString(item, "crane_yes_response_key");
        request.CraneFeeGold = ReadQueueParameterInt(item, "crane_fee_gold");
        request.CraneMoneyBefore = ReadQueueParameterInt(item, "crane_money_before");
        request.CraneEmptySlotsBefore = ReadQueueParameterInt(item, "crane_empty_slots_before");
        request.CraneAttempts = ReadQueueParameterInt(item, "crane_attempts");
        request.CraneTimerTicksPerAttempt = ReadQueueParameterInt(item, "crane_timer_ticks_per_attempt");
        request.CraneSelectionPolicy = ReadQueueParameterString(item, "crane_selection_policy");
        request.CraneExitPolicy = ReadQueueParameterString(item, "crane_exit_policy");
    }
}
