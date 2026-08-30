using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyPrairieKingRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.play_prairie_king" or "debug.setup_prairie_king"))
            return;
        request.PrairieKingProjectionFingerprint = ReadQueueParameterString(item, "prairie_king_projection_fingerprint");
        request.PrairieKingActionRaw = ReadQueueParameterString(item, "prairie_king_action_raw");
        request.PrairieKingActionToken = ReadQueueParameterString(item, "prairie_king_action_token");
        request.PrairieKingDialogueKey = ReadQueueParameterString(item, "prairie_king_dialogue_key");
        request.PrairieKingDialogueResponseKey = ReadQueueParameterString(item, "prairie_king_dialogue_response_key");
        request.PrairieKingCompletedBefore = ReadQueueParameterLong(item, "prairie_king_completed_before");
        request.PrairieKingCompletedWithoutDyingBefore = ReadQueueParameterLong(item, "prairie_king_completed_without_dying_before");
        request.PrairieKingCompletionGoal = ReadQueueParameterString(item, "prairie_king_completion_goal");
        request.PrairieKingEquivalentDurationTicks = ReadQueueParameterInt(item, "prairie_king_equivalent_duration_ticks");
        request.PrairieKingEquivalentAcceleration = ReadQueueParameterInt(item, "prairie_king_equivalent_acceleration");
        request.PrairieKingEquivalentContract = ReadQueueParameterString(item, "prairie_king_equivalent_contract");
    }
}
