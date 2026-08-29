using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFieldOfficeRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.donate_field_office_piece" or "debug.setup_field_office_donation"))
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
    }
}
