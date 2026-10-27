using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyIslandGourmandRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId != "executor.advance_gourmand_request")
            return;

        request.GourmandAction = ReadQueueParameterString(item, "gourmand_action");
        request.GourmandSchemaVersion = ReadQueueParameterString(item, "gourmand_schema_version");
        request.GourmandProjectionStatus = ReadQueueParameterString(item, "gourmand_projection_status");
        request.GourmandInteractionStage = ReadQueueParameterString(item, "gourmand_interaction_stage");
        request.GourmandRequestIndexBefore = ReadQueueParameterInt(item, "request_index_before");
        request.GourmandRequestIndexAfter = ReadQueueParameterInt(item, "request_index_after");
        request.GourmandRequiredCropItemId = ReadQueueParameterString(item, "required_crop_item_id");
        request.GourmandRequiredCropQualifiedItemId = ReadQueueParameterString(item, "required_crop_qualified_item_id");
        request.GourmandReadyMatchingCropCount = ReadQueueParameterInt(item, "ready_matching_crop_count");
        request.GourmandTalkedBefore = ReadNullableBoolQueueParameter(item, "talked_to_gourmand_before");
        request.GourmandTalkedAfter = ReadNullableBoolQueueParameter(item, "talked_to_gourmand_after");
        request.GourmandExpectedCollectedNutKey = ReadQueueParameterString(item, "expected_collected_nut_key");
        request.GourmandWalnutDebrisCountBefore = ReadQueueParameterInt(item, "walnut_debris_count_before");
        request.GourmandWalnutDebrisCountAfter = ReadQueueParameterInt(item, "walnut_debris_count_after");
        request.GourmandExpectedWalnutDebrisSpawnCount = ReadQueueParameterInt(item, "expected_walnut_debris_spawn_count");
        request.GourmandGoldenWalnutsFoundBefore = ReadQueueParameterInt(item, "golden_walnuts_found_before");
        request.GourmandGoldenWalnutsFoundDelta = ReadQueueParameterInt(item, "golden_walnuts_found_delta");
        request.GourmandGoldenWalnutsFoundAfter = ReadQueueParameterInt(item, "golden_walnuts_found_after");
        request.GourmandQuestionKey = ReadQueueParameterString(item, "gourmand_question_key");
        request.GourmandResponseKey = ReadQueueParameterString(item, "gourmand_response_key");
        request.GourmandOutputDelivery = ReadQueueParameterString(item, "output_delivery");
    }
}
