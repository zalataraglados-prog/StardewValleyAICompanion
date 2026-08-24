using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFencePlacementRequestFields(
        TrainingExecutionRequest request,
        JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.place_fence", StringComparison.Ordinal))
        {
            return;
        }

        request.FenceDataKey = ReadQueueParameterString(item, "fence_data_key");
        request.ExpectedFenceIsGate = ReadQueueParameterBool(item, "expected_is_gate");
        request.ExpectedFenceDrawSum = ReadQueueParameterInt(item, "expected_draw_sum_after");
        request.ExpectedFenceGateFunctional = ReadQueueParameterBool(item, "expected_gate_functional");
        request.ExpectedFenceHealthMin = ReadQueueParameterDouble(item, "expected_health_min");
        request.ExpectedFenceHealthMax = ReadQueueParameterDouble(item, "expected_health_max");
        request.ExpectedFenceMaxHealthMin = ReadQueueParameterDouble(item, "expected_max_health_min");
        request.ExpectedFenceMaxHealthMax = ReadQueueParameterDouble(item, "expected_max_health_max");
    }
}
