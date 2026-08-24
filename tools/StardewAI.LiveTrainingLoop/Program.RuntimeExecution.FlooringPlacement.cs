using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFlooringPlacementRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.place_flooring", StringComparison.Ordinal))
        {
            return;
        }

        request.FloorDataKey = ReadQueueParameterString(item, "floor_data_key");
        request.FlooringConnectType = ReadQueueParameterString(item, "connect_type");
        request.ExpectedFlooringNeighborMask = ReadQueueParameterInt(item, "expected_neighbor_mask_after");
        request.ExpectedFlooringViewMin = ReadQueueParameterInt(item, "expected_which_view_min");
        request.ExpectedFlooringViewMax = ReadQueueParameterInt(item, "expected_which_view_max");
    }
}
