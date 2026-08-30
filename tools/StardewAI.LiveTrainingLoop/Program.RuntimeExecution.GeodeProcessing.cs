using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyGeodeProcessingRequestFields(TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.crack_geode" or "processing.crack_geode" or "debug.setup_geode_processing")) return;
        request.GeodePurpose = ReadQueueParameterString(item, "geode_purpose");
        request.GeodeQualifiedItemId = ReadQueueParameterString(item, "geode_qualified_item_id");
        request.GeodeSlotIndex = ReadQueueParameterInt(item, "geode_slot_index");
        request.GeodeInputQuality = ReadQueueParameterInt(item, "geode_input_quality");
        request.GeodeStackBefore = ReadQueueParameterInt(item, "geode_stack_before");
        request.GeodeFreeSlotsBefore = ReadQueueParameterInt(item, "geode_free_slots_before");
        request.GeodeMoneyBefore = ReadQueueParameterInt(item, "geode_money_before");
        request.GeodePriceGold = ReadQueueParameterInt(item, "geode_price_gold");
        request.GeodesCrackedBefore = ReadQueueParameterInt(item, "geodes_cracked_before");
        request.MysteryBoxesOpenedBefore = ReadQueueParameterInt(item, "mystery_boxes_opened_before");
        request.GoldenCoconutCrackedBefore = ReadQueueParameterBool(item, "golden_coconut_cracked_before");
        request.GoldenWalnutsBefore = ReadQueueParameterInt(item, "golden_walnuts_before");
        request.GoldenWalnutsFoundBefore = ReadQueueParameterInt(item, "golden_walnuts_found_before");
        request.GeodeArchaeologyFoundCount = ReadQueueParameterInt(item, "geode_archaeology_found_count");
        request.GeodeSaveIdHalf = ReadQueueParameterLong(item, "geode_save_id_half");
        request.GeodePlayerIdHalf = ReadQueueParameterLong(item, "geode_player_id_half");
        request.GeodeSeason = ReadQueueParameterString(item, "geode_season");
        request.GeodeDeepestMineLevel = ReadQueueParameterInt(item, "geode_deepest_mine_level");
        request.GeodeSkill1Level = ReadQueueParameterInt(item, "geode_skill_1_level");
        request.GeodeFarmingMasteryUnlocked = ReadQueueParameterBool(item, "geode_farming_mastery_unlocked");
        request.GeodeQiBeansRuleActive = ReadQueueParameterBool(item, "geode_qi_beans_rule_active");
        request.GeodeGotMysteryBookMailBefore = ReadQueueParameterBool(item, "geode_got_mystery_book_mail_before");
        request.GeodeArtifactFoundMailBefore = ReadQueueParameterBool(item, "geode_artifact_found_mail_before");
        request.GeodePredictionKind = ReadQueueParameterString(item, "geode_prediction_kind");
        request.GeodeExpectedOutputQid = ReadQueueParameterString(item, "geode_expected_output_qid");
        request.GeodeExpectedOutputStack = ReadQueueParameterInt(item, "geode_expected_output_stack");
        request.GeodeExpectedOutputQuality = ReadQueueParameterInt(item, "geode_expected_output_quality");
        request.GeodeAcceptedOutputsJson = ReadQueueParameterString(item, "geode_accepted_outputs_json");
        request.GeodeExpectedMailAdditionsJson = ReadQueueParameterString(item, "geode_expected_mail_additions_json");
        request.GeodeProjectionFingerprint = ReadQueueParameterString(item, "geode_projection_fingerprint");
        request.GeodeActionRaw = ReadQueueParameterString(item, "geode_action_raw");
        request.GeodeActionToken = ReadQueueParameterString(item, "geode_action_token");
    }
}
