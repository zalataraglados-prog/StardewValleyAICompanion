using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.LiveTrainingLoop;

public static class NativeOutputObservationMode
{
    public static void Validate(LiveTrainingOptions options)
    {
        if (options.ObserveNativeInventoryTransients && !options.ObserveNativeInventoryPayload)
            throw new ArgumentException("--observe-native-inventory-transients requires --observe-native-inventory-payload.");
        if (options.ObserveNativeInventoryPayload && !options.ObserveNativeClearanceToolInputs)
            throw new ArgumentException("--observe-native-inventory-payload requires --observe-native-clearance-tool-inputs.");
        if (options.ObserveNativeClearanceToolInputs && !options.ObserveNativeOutputSnapshotAnchor)
            throw new ArgumentException("--observe-native-clearance-tool-inputs requires --observe-native-output-snapshot-anchor.");
        if (options.ObserveNativeOutputSnapshotAnchor &&
            (!options.ObserveNativeOutputs || !string.Equals(options.ExecutionSnapshotProfile, "full", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("--observe-native-output-snapshot-anchor requires --observe-native-outputs and full execution snapshots.");
        if (!options.ObserveNativeOutputs) return;
        if (!options.SkipTraining || !options.RequireExecutorFeedback ||
            !options.UseRuntimeTestHarnessExecutor || options.UseProductExecutor ||
            options.TargetExecutionMode != ExecutionTargetProfiles.TrainingSingleplayer)
            throw new ArgumentException("--observe-native-outputs requires --skip-training, " +
                "training_singleplayer, runtime-test-harness execution and executor feedback.");
    }

    public static string? CreateScopeId(LiveTrainingOptions options, TrainingExecutionRequest request)
    {
        Validate(options);
        if (!options.ObserveNativeOutputs || request.OptionId is not ("executor.move_to_tile" or "executor.clear_obstacle"))
            return null;
        var identity = new[] { request.RunId, request.QueueId, request.QueueItemId, request.RequestNonce,
            request.BeforeStateHash, request.SourcePlanningOptionId, request.SaveIsolationPath, request.OptionId };
        if (identity.Any(string.IsNullOrWhiteSpace) || request.RunId != options.RunId ||
            request.SaveIsolationPath != options.SaveIsolationPath || request.ExecutionMode != options.TargetExecutionMode)
            throw new InvalidOperationException("Native output observation requires the complete original request identity.");
        return "native-output.v1:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity))).ToLowerInvariant();
    }
}
