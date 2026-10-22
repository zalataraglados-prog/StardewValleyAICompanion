using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyToolUpgradeRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not (
            "executor.submit_tool_upgrade" or "executor.claim_tool_upgrade"))
        {
            return;
        }
        request.ToolUpgradeProjectionFingerprint = ReadQueueParameterString(item, "tool_upgrade_projection_fingerprint");
        request.ToolUpgradeLifecycleStage = ReadQueueParameterString(item, "tool_upgrade_lifecycle_stage");
        request.ToolUpgradeActionRaw = ReadQueueParameterString(item, "tool_upgrade_action_raw");
        request.ToolUpgradeActionToken = ReadQueueParameterString(item, "tool_upgrade_action_token");
        request.ToolUpgradeSourceSlotIndex = ReadQueueParameterInt(item, "tool_upgrade_source_slot_index");
        request.ToolUpgradeSourceQualifiedItemId = ReadQueueParameterString(item, "tool_upgrade_source_qualified_item_id");
        request.ToolUpgradeSourceItemId = ReadQueueParameterString(item, "tool_upgrade_source_item_id");
        request.ToolUpgradeSourceLevel = ReadQueueParameterInt(item, "tool_upgrade_source_level");
        request.ToolUpgradeTargetQualifiedItemId = ReadQueueParameterString(item, "tool_upgrade_target_qualified_item_id");
        request.ToolUpgradeTargetItemId = ReadQueueParameterString(item, "tool_upgrade_target_item_id");
        request.ToolUpgradeTargetLevel = ReadQueueParameterInt(item, "tool_upgrade_target_level");
        request.ToolUpgradeMoneyBefore = ReadQueueParameterInt(item, "tool_upgrade_money_before");
        request.ToolUpgradePriceGold = ReadQueueParameterInt(item, "tool_upgrade_price_gold");
        request.ToolUpgradeTradeQualifiedItemId = ReadQueueParameterString(item, "tool_upgrade_trade_qualified_item_id");
        request.ToolUpgradeTradeItemId = ReadQueueParameterString(item, "tool_upgrade_trade_item_id");
        request.ToolUpgradeTradeAmount = ReadQueueParameterInt(item, "tool_upgrade_trade_amount");
        request.ToolUpgradeTradeAvailableBefore = ReadQueueParameterInt(item, "tool_upgrade_trade_available_before");
        request.ToolUpgradeExpectedDaysLeftAfter = ReadQueueParameterInt(item, "tool_upgrade_expected_days_left_after");
        request.ToolUpgradePendingQualifiedItemId = ReadQueueParameterString(item, "tool_upgrade_pending_qualified_item_id");
        request.ToolUpgradePendingItemId = ReadQueueParameterString(item, "tool_upgrade_pending_item_id");
        request.ToolUpgradePendingLevel = ReadQueueParameterInt(item, "tool_upgrade_pending_level");
        request.ToolUpgradeDaysLeftBefore = ReadQueueParameterInt(item, "tool_upgrade_days_left_before");
        request.ToolUpgradeFreeSlotsBefore = ReadQueueParameterInt(item, "tool_upgrade_free_slots_before");
    }
}
