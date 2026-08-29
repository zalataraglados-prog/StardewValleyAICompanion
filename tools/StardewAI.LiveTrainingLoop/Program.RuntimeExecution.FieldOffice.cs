using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFieldOfficeRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not (
            "executor.donate_field_office_piece" or
            "executor.answer_field_office_survey" or
            "debug.setup_field_office_donation" or
            "debug.setup_field_office_survey" or
            "debug.answer_field_office_survey_wrong" or
            "debug.field_office_survey_day_update"))
            return;
        request.FieldOfficeDeskActionRaw = ReadQueueParameterString(item, "field_office_desk_action_raw");
        request.FieldOfficeTargetPieceIndex = ReadQueueParameterInt(item, "target_piece_index");
        request.FieldOfficeTargetPieceKind = ReadQueueParameterString(item, "target_piece_kind");
        request.FieldOfficeTargetSetKind = ReadQueueParameterString(item, "target_set_kind");
        request.FieldOfficeDonatedPieceCountBefore = ReadQueueParameterInt(item, "expected_donated_piece_count_before");
        request.FieldOfficeDonatedPieceCountAfter = ReadQueueParameterInt(item, "expected_donated_piece_count_after");
        request.FieldOfficeCompletesSet = ReadNullableBoolQueueParameter(item, "expected_completes_set");
        request.FieldOfficeNewRewardItemsJson = ReadQueueParameterString(item, "new_reward_items_json");
        request.FieldOfficeRewardsBeforeJson = ReadQueueParameterString(item, "uncollected_rewards_before_json");
        request.FieldOfficeRewardsAfterJson = ReadQueueParameterString(item, "uncollected_rewards_after_json");
        request.FieldOfficeCollectedNutKey = ReadQueueParameterString(item, "expected_collected_nut_key");
        request.FieldOfficeCollectedNutBefore = ReadNullableBoolQueueParameter(item, "collected_nut_before");
        request.FieldOfficeFinaleReadyAfter = ReadNullableBoolQueueParameter(item, "expected_finale_ready_after");
        request.FieldOfficePlantsRestoredLeftBefore = ReadNullableBoolQueueParameter(item, "plants_restored_left_before");
        request.FieldOfficePlantsRestoredRightBefore = ReadNullableBoolQueueParameter(item, "plants_restored_right_before");
        request.FieldOfficeFinaleReceivedBefore = ReadNullableBoolQueueParameter(item, "finale_received_or_pending_before");
        request.FieldOfficeGoldenWalnutsFoundBefore = ReadQueueParameterInt(item, "golden_walnuts_found_before");
        request.FieldOfficeProjectionStatus = ReadQueueParameterString(item, "field_office_projection_status");
        request.FieldOfficeFixtureCase = ReadQueueParameterString(item, "field_office_fixture_case");
        request.FieldOfficeSurveyActionRaw = ReadQueueParameterString(item, "field_office_survey_action_raw");
        request.FieldOfficeSurveyKind = ReadQueueParameterString(item, "survey_kind");
        request.FieldOfficeSurveyAnswer = ReadQueueParameterInt(item, "survey_answer");
        request.FieldOfficeSurveyAnswerMinimum = ReadQueueParameterInt(item, "survey_answer_minimum");
        request.FieldOfficeSurveyAnswerMaximum = ReadQueueParameterInt(item, "survey_answer_maximum");
        request.FieldOfficeSurveyPromptQuestionKey = ReadQueueParameterString(item, "survey_prompt_question_key");
        request.FieldOfficeSurveyPromptResponseKey = ReadQueueParameterString(item, "survey_prompt_response_key");
        request.FieldOfficeSurveyAnswerQuestionKey = ReadQueueParameterString(item, "survey_answer_question_key");
        request.FieldOfficeSurveyAnswerResponseKey = ReadQueueParameterString(item, "survey_answer_response_key");
        request.FieldOfficeSurveyPlantRestoredBefore = ReadNullableBoolQueueParameter(item, "survey_plant_restored_before");
        request.FieldOfficeSurveyPlantRestoredAfter = ReadNullableBoolQueueParameter(item, "survey_plant_restored_after");
        request.FieldOfficeSurveyFailedTodayBefore = ReadNullableBoolQueueParameter(item, "survey_failed_today_before");
        request.FieldOfficeSurveyFailedTodayAfter = ReadNullableBoolQueueParameter(item, "survey_failed_today_after");
        request.FieldOfficeSurveyWalnutDebrisCountBefore = ReadQueueParameterInt(item, "walnut_debris_count_before");
        request.FieldOfficeSurveyWalnutDebrisCountAfter = ReadQueueParameterInt(item, "walnut_debris_count_after");
        request.FieldOfficeSurveyWalnutDebrisSpawnCount = ReadQueueParameterInt(item, "walnut_debris_spawn_count");
        request.FieldOfficeSurveyGoldenWalnutsFoundAfter = ReadQueueParameterInt(item, "golden_walnuts_found_after");
        request.FieldOfficeSurveyGoldenWalnutsFoundDelta = ReadQueueParameterInt(item, "golden_walnuts_found_delta");
        request.FieldOfficeSurveyOutputDelivery = ReadQueueParameterString(item, "output_delivery");
        request.FieldOfficeSurveyExpectedFinaleTriggerAfter = ReadNullableBoolQueueParameter(item, "expected_finale_trigger_after");
        request.FieldOfficeSurveyDonatedPieceCountBefore = ReadQueueParameterInt(item, "donated_piece_count_before");
        request.FieldOfficeSurveyFixtureCase = ReadQueueParameterString(item, "field_office_survey_fixture_case");
        request.FieldOfficeSurveyAnswerMode = ReadQueueParameterString(item, "field_office_survey_answer_mode");
    }
}
