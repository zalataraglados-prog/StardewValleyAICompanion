using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFishPondManagementRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "fishing.manage_fish_pond", StringComparison.Ordinal))
            return;
        request.ManagementOperation = ReadQueueParameterString(item, "management_operation");
        request.FishPondManagementReason = ReadQueueParameterString(item, "management_reason");
        request.ConfirmEmptyPond = ReadNullableBoolQueueParameter(item, "confirm_empty_pond");
        request.ExpectedFishCountAfter = ReadQueueParameterInt(item, "expected_fish_count_after");
        request.ExpectedNeededItemQualifiedItemIdBefore = ReadQueueParameterString(item, "expected_needed_item_qualified_item_id_before");
        request.ExpectedNeededItemCountBefore = ReadQueueParameterInt(item, "expected_needed_item_count_before");
        request.ExpectedHasCompletedRequestBefore = ReadQueueParameterInt(item, "expected_has_completed_request_before");
        request.ExpectedGoldenAnimalCrackerBefore = ReadQueueParameterInt(item, "expected_golden_animal_cracker_before");
        request.ExpectedGoldenAnimalCrackerAfter = ReadQueueParameterInt(item, "expected_golden_animal_cracker_after");
        request.ExpectedHasSpawnedFishBefore = ReadQueueParameterInt(item, "expected_has_spawned_fish_before");
        request.ExpectedHasSpawnedFishAfter = ReadQueueParameterInt(item, "expected_has_spawned_fish_after");
        request.ExpectedNettingStyleBefore = ReadQueueParameterInt(item, "expected_netting_style_before");
        request.ExpectedNettingStyleAfter = ReadQueueParameterInt(item, "expected_netting_style_after");
        request.ExpectedFishDebrisQualifiedItemId = ReadQueueParameterString(item, "expected_fish_debris_qualified_item_id");
        request.ExpectedFishDebrisCount = ReadQueueParameterInt(item, "expected_fish_debris_count");
        request.ExpectedSignQualifiedItemIdBefore = ReadQueueParameterString(item, "expected_sign_qualified_item_id_before");
        request.ExpectedOutputQualifiedItemIdBefore = ReadQueueParameterString(item, "expected_output_qualified_item_id_before");
        request.ExpectedOverrideWaterColorPackedBefore = ReadQueueParameterLong(item, "expected_override_water_color_packed_before");
    }
}
