using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherWateringMaintenance
{
    private readonly Dictionary<string, string> requestBlobHashes = new(CropTeacherSeedTransferNativeOutcome.PathComparer);

    private void VerifySelectedWateringStep(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        ActionQueueEnvelope queue, QueueExecutionStepReceipt step, JsonElement rawStep, int index,
        SnapshotEnvelope before, SnapshotEnvelope after, string location, MoneyRevenueCropPlantingTile tile,
        HashSet<string> nonces, Dictionary<string, string> retained)
    {
        var resultPath = CurrentTeacherFrontierSupport.RequiredString(rawStep, "execution_path");
        var frame = NativeExecutionActionSourceReader.ReadSelectedWateringFrame(transition.QueuePath, resultPath,
            step.AfterSnapshotPath, queue.StateHash, transition.ExecutorVersion);
        Require(frame.Result.RunId == transition.RunId && frame.Result.QueueId == step.QueueId &&
            frame.Item.QueueItemId == queue.Items[index].QueueItemId && frame.Result.Status == step.Status &&
            frame.Result.PrimitiveKind == step.PrimitiveKind && frame.Result.PrimitiveVerificationStatus == step.PrimitiveVerificationStatus &&
            EqualJson(frame.Before, before) && EqualJson(frame.After, after) &&
            CurrentTeacherFrontierSupport.RequiredString(rawStep, "execution_request_nonce") == frame.Request.RequestNonce &&
            nonces.Add(frame.Request.RequestNonce) &&
            frame.Result.BlockReasons.SequenceEqual(step.BlockReasons, StringComparer.Ordinal) &&
            EqualJson(JsonSerializer.SerializeToElement(frame.Result.ChangedFacts, JsonDefaults.Options), step.ChangedFacts),
            "Crop watering native action differs from its original aggregate step/source/nonce.");
        foreach (var snapshot in new[] { frame.Before, frame.After })
            Require(NativeExecutionSourceSupport.Value(snapshot, "environment", "training_run_id").GetString() == transition.RunId &&
                NativeExecutionSourceSupport.Value(snapshot, "environment", "save_isolation_path").GetString() == frame.Request.SaveIsolationPath &&
                NativeExecutionSourceSupport.Value(snapshot, "environment", "training_mode").GetString() == "1",
                "Crop watering native frame changed its isolated environment.");
        _ = NativeOutputObservationBoundary.ReadWateringResources(frame.Request, frame.Result, before, after, location, tile);
        foreach (var (label, path) in frame.SourcePaths)
        {
            var hash = frame.SourceSha256[label];
            var target = label == "request_gzip" ? requestBlobHashes : retained;
            Require(!(label == "request_gzip" ? retained : requestBlobHashes).ContainsKey(path) &&
                (!target.TryGetValue(path, out var prior) || prior == hash),
                "Crop watering native source aliases another role or changed its retained hash.");
            target[path] = hash;
        }
    }

    private void ValidateWateringRequestBlobs()
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        foreach (var (path, hash) in requestBlobHashes)
        {
            CropTeacherRetainedInputs.RequireRegularInput(path);
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Crop watering original compressed request changed.");
        }
    }
}
