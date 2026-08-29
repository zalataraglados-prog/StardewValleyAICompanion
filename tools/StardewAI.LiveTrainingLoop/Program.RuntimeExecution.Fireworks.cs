using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFireworkRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_firework", StringComparison.Ordinal))
            return;

        request.ExpectedFireworkType = ReadQueueParameterInt(item, "firework_type");
        request.ExpectedFireworkSourceRectX = ReadQueueParameterInt(item, "firework_source_rect_x");
        request.ExpectedFireworkSourceRectY = ReadQueueParameterInt(item, "firework_source_rect_y");
        request.ExpectedFireworkFuseDurationMs = ReadQueueParameterInt(item, "firework_fuse_duration_ms");
        request.ExpectedFireworkRocketDelayMs = ReadQueueParameterInt(item, "firework_rocket_delay_ms");
        request.ExpectedFireworkRocketIdMin = ReadQueueParameterInt(item, "firework_rocket_id_min");
        request.ExpectedFireworkRocketIdMax = ReadQueueParameterInt(item, "firework_rocket_id_max");
        request.FireworkAccelerationYMin = ReadQueueParameterString(item, "firework_acceleration_y_min");
        request.FireworkAccelerationYMax = ReadQueueParameterString(item, "firework_acceleration_y_max");
        request.FireworkAccelerationYStep = ReadQueueParameterString(item, "firework_acceleration_y_step");
        request.FireworkRandomContract = ReadQueueParameterString(item, "firework_random_contract");
    }
}
