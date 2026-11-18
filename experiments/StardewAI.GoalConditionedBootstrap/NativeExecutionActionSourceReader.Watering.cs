using StardewAI.Contracts.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    internal static NativeExecutionActionFrame ReadSelectedWateringFrame(string queuePath, string resultPath,
        string afterPath, string compiledStateHash, string executorVersion)
    {
        Require(PolicyTrajectoryVersionPins.IsKnownExecutor(executorVersion), "Unknown selected watering executor.");
        var frame = ReadFrame(queuePath, resultPath, afterPath, compiledStateHash, NativeProductBoundary.WateringResources);
        var expectedPath = executorVersion == PolicyTrajectoryVersionPins.ProductExecutor
            ? "/api/v1/product/execute" : "/api/v1/training/execute";
        Require(Uri.TryCreate(frame.Endpoint, UriKind.Absolute, out var endpoint) && endpoint.AbsolutePath == expectedPath &&
            frame.Item.OptionId == "executor.water_crop", "Selected watering changed its admitted executor or action.");
        return frame;
    }
}
