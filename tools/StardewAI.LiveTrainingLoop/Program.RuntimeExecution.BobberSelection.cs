using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyBobberSelectionRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.choose_bobber_style" or "player.choose_bobber" or
            "debug.setup_bobber_selection"))
            return;
        request.BobberStyleId = ReadQueueParameterInt(item, "bobber_style_id");
        request.BobberReason = ReadQueueParameterString(item, "bobber_reason");
        request.ConfirmBobberStyle = ReadQueueParameterBool(item, "confirm_bobber_style");
        request.BobberProjectionFingerprint = ReadQueueParameterString(item, "bobber_projection_fingerprint");
        request.BobberStyleBefore = ReadQueueParameterInt(item, "bobber_style_before");
        request.BobberRandomBefore = ReadQueueParameterBool(item, "bobber_random_before");
        request.BobberRandomAfter = ReadQueueParameterBool(item, "bobber_random_after");
        request.BobberFishCaughtSpeciesCount = ReadQueueParameterInt(item, "bobber_fish_caught_species_count");
        request.BobberNativeUnlockQuotient = ReadQueueParameterInt(item, "bobber_native_unlock_quotient");
        request.BobberActionRaw = ReadQueueParameterString(item, "bobber_action_raw");
        request.ExpectedMenuKind = ReadQueueParameterString(item, "expected_menu_kind");
    }
}
