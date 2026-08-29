using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyHomeRenovationRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.renovate_home", StringComparison.Ordinal))
            return;
        request.RenovationId = ReadQueueParameterString(item, "renovation_id");
        request.RenovationSelectedIndex = ReadQueueParameterInt(item, "selected_index");
        request.RenovationReason = ReadQueueParameterString(item, "renovation_reason");
        request.ConfirmRenovation = ReadNullableBoolQueueParameter(item, "confirm_renovation");
        request.ConfirmDestructiveRenovation = ReadNullableBoolQueueParameter(item, "confirm_destructive");
        request.RenovationIsDestructive = ReadNullableBoolQueueParameter(item, "is_destructive");
        request.HomeLocationId = ReadQueueParameterString(item, "home_location_id");
        request.HomeRuntimeType = ReadQueueParameterString(item, "home_runtime_type");
        request.ExpectedHomeHouseUpgradeLevel = ReadQueueParameterInt(item, "expected_house_upgrade_level");
        request.HomeRenovationDataPayloadSha256 = ReadQueueParameterString(item, "data_payload_sha256");
        request.HomeRenovationDataContractStatus = ReadQueueParameterString(item, "data_contract_status");
        request.NativeAvailableRenovationIdsJson = ReadQueueParameterString(item, "native_available_renovation_ids_json");
        request.NativeRenovationShopIndex = ReadQueueParameterInt(item, "native_shop_index");
        request.RenovationRoomId = ReadQueueParameterString(item, "room_id");
        request.RenovationAnimationType = ReadQueueParameterString(item, "animation_type");
        request.RenovationCheckForObstructions = ReadNullableBoolQueueParameter(item, "check_for_obstructions");
        request.RenovationFirstPurchaseMailId = ReadQueueParameterString(item, "first_purchase_mail_id");
        request.RenovationFirstPurchaseMailBefore = ReadNullableBoolQueueParameter(item, "first_purchase_mail_before");
        request.ExpectedRenovationFirstPurchaseMailAfter = ReadNullableBoolQueueParameter(item, "expected_first_purchase_mail_after");
        request.RenovationRefundEligible = ReadNullableBoolQueueParameter(item, "refund_eligible");
        request.RenovationRequirementsJson = ReadQueueParameterString(item, "requirements_json");
        request.RenovateActionsJson = ReadQueueParameterString(item, "renovate_actions_json");
        request.SelectedRegionRectanglesJson = ReadQueueParameterString(item, "selected_region_rectangles_json");
        request.SelectedRegionObstructionStatus = ReadQueueParameterString(item, "selected_region_obstruction_status");
        request.RenovationProjectionFingerprint = ReadQueueParameterString(item, "projection_fingerprint");
    }
}
