using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyDartsGameRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.play_darts" or "debug.setup_darts_game"))
            return;
        request.DartsProjectionFingerprint = ReadQueueParameterString(item, "darts_projection_fingerprint");
        request.DartsActionRaw = ReadQueueParameterString(item, "darts_action_raw");
        request.DartsActionToken = ReadQueueParameterString(item, "darts_action_token");
        request.DartsYesResponseKey = ReadQueueParameterString(item, "darts_yes_response_key");
        request.DartsLimitedNutKey = ReadQueueParameterString(item, "darts_limited_nut_key");
        request.DartsLimitedNutLimit = ReadQueueParameterInt(item, "darts_limited_nut_limit");
        request.DartsLimitedNutDroppedBefore = ReadQueueParameterInt(item, "darts_limited_nut_dropped_before");
        request.DartsLimitedNutDroppedAfter = ReadQueueParameterInt(item, "darts_limited_nut_dropped_after");
        request.DartsStartingDartCount = ReadQueueParameterInt(item, "darts_starting_dart_count");
        request.DartsStartingPoints = ReadQueueParameterInt(item, "darts_starting_points");
        request.DartsPerfectVictoryMaxThrows = ReadQueueParameterInt(item, "darts_perfect_victory_max_throws");
        request.DartsPerfectScorePlan = ReadQueueParameterString(item, "darts_perfect_score_plan");
        request.DartsChargeReleaseThreshold = ReadQueueParameterDouble(item, "darts_charge_release_threshold");
    }
}
