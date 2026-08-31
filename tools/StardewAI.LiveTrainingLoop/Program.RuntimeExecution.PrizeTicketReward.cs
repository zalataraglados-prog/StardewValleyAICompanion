using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyPrizeTicketRewardRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("rewards.claim_prize_ticket" or "executor.claim_prize_ticket" or "debug.setup_prize_ticket_reward"))
            return;
        request.PrizeTicketStage = ReadQueueParameterString(item, "prize_ticket_stage");
        request.PrizeTicketProjectionFingerprint = ReadQueueParameterString(item, "prize_ticket_projection_fingerprint");
        request.PrizeTicketCurrentRewardFingerprint = ReadQueueParameterString(item, "prize_ticket_current_reward_fingerprint");
        request.PrizeTicketPreviewJson = ReadQueueParameterString(item, "prize_ticket_preview_json");
        request.PrizeTicketInventoryCountBefore = ReadQueueParameterInt(item, "prize_ticket_inventory_count_before");
        request.PrizeTicketPendingCountBefore = ReadQueueParameterInt(item, "prize_ticket_pending_count_before");
        request.PrizeTicketClaimedCountBefore = ReadQueueParameterInt(item, "prize_ticket_claimed_count_before");
        request.PrizeTicketPrizeLevel = ReadQueueParameterInt(item, "prize_ticket_prize_level");
        request.PrizeTicketRewardQualifiedItemId = ReadQueueParameterString(item, "prize_ticket_reward_qualified_item_id");
        request.PrizeTicketRewardItemId = ReadQueueParameterString(item, "prize_ticket_reward_item_id");
        request.PrizeTicketRewardStack = ReadQueueParameterInt(item, "prize_ticket_reward_stack");
        request.PrizeTicketRewardQuality = ReadQueueParameterInt(item, "prize_ticket_reward_quality");
        request.PrizeTicketRewardRuntimeType = ReadQueueParameterString(item, "prize_ticket_reward_runtime_type");
        request.PrizeTicketInventoryMaxItems = ReadQueueParameterInt(item, "prize_ticket_inventory_max_items");
        request.PrizeTicketInventoryOccupiedSlots = ReadQueueParameterInt(item, "prize_ticket_inventory_occupied_slots");
        request.PrizeTicketPendingCapacitySufficient = ReadNullableBoolQueueParameter(item, "prize_ticket_pending_capacity_sufficient");
        request.PrizeTicketActionRaw = ReadQueueParameterString(item, "prize_ticket_action_raw");
        request.PrizeTicketFixtureCase = ReadQueueParameterString(item, "prize_ticket_fixture_case");
    }
}
