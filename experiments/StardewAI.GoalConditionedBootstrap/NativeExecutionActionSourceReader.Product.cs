using StardewAI.Contracts.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    internal static VerifiedNativeExecutionQueue ReadSelectedProductActionQueue(string queuePath, string beforePath, string receiptPath,
        string afterPath, string runId, string candidateId)
    {
        var native = ReadQueueCore(queuePath, beforePath, receiptPath, afterPath, runId, candidateId,
            NativeProductBoundary.NativeAction, PolicyTrajectoryVersionPins.ProductExecutor);
        RequireOriginalProductWire(native);
        return native;
    }

    private static void RequireOriginalProductWire(VerifiedNativeExecutionQueue native)
    {
        Require(native.Actions.All(frame => Uri.TryCreate(frame.Endpoint, UriKind.Absolute, out var endpoint) &&
                endpoint.AbsolutePath == "/api/v1/product/execute"),
            "Product native queue requires the original Product wire for every action.");
    }
}
