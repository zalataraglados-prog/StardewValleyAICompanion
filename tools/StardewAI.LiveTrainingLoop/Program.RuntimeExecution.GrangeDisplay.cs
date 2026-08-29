using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyGrangeDisplayRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.manage_grange_display", StringComparison.Ordinal))
            return;
        request.GrangeProjectionFingerprint = ReadQueueParameterString(item, "grange_projection_fingerprint");
        request.GrangeInteractionTileX = ReadQueueParameterInt(item, "interaction_tile_x");
        request.GrangeInteractionTileY = ReadQueueParameterInt(item, "interaction_tile_y");
        request.GrangeStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
        request.GrangeStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
        request.GrangeJudged = ReadQueueParameterBool(item, "grange_judged");
        request.GrangeObjective = ReadQueueParameterString(item, "objective");
        request.GrangeOperation = ReadQueueParameterString(item, "operation");
        request.GrangeDisplaySlotIndex = ReadQueueParameterInt(item, "display_slot_index");
        request.GrangeInventorySlotIndex = ReadQueueParameterInt(item, "inventory_slot_index");
        request.GrangeInventoryStackBefore = ReadQueueParameterInt(item, "inventory_stack_before");
        request.GrangeInventoryStackAfter = ReadQueueParameterInt(item, "inventory_stack_after");
        request.GrangeSinkInventorySlotIndex = ReadQueueParameterInt(item, "sink_inventory_slot_index");
        request.QualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
        request.ItemId = ReadQueueParameterString(item, "item_id");
        request.GrangeItemRuntimeType = ReadQueueParameterString(item, "runtime_type");
        request.GrangeItemQuality = ReadQueueParameterInt(item, "quality");
        request.GrangeActualSellPrice = ReadQueueParameterInt(item, "actual_sell_price");
        request.GrangeItemPoints = ReadQueueParameterInt(item, "item_points");
        request.GrangeScoringGroup = ReadQueueParameterString(item, "scoring_group");
        request.GrangeScoreBefore = ReadQueueParameterInt(item, "score_before");
        request.GrangeScoreAfter = ReadQueueParameterInt(item, "score_after");
        request.GrangeOccupiedSlotsBefore = ReadQueueParameterInt(item, "occupied_slots_before");
        request.GrangeOccupiedSlotsAfter = ReadQueueParameterInt(item, "occupied_slots_after");
        request.GrangeBestAvailableScore = ReadQueueParameterInt(item, "best_available_score");
        request.GrangeFirstPlaceScore = ReadQueueParameterInt(item, "first_place_score");
        request.NativeContract = ReadQueueParameterString(item, "native_contract");
    }
}
