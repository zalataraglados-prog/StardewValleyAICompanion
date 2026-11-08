using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherRefillObservedStep(string QueueItemId, string OptionId,
    NativeOutputObservationSummary Observation);

// Retains the original successful native queue. No dispatch, resource rebind or producer credit.
public sealed class CropTeacherRefillApproachNativeTrace
{
    private readonly CropTeacherRefillApproachCompilation compilation;
    private readonly CropTeacherRetainedInputs inputs;
    private readonly Dictionary<string, string> requestBlobs = new(CropTeacherSeedTransferNativeOutcome.PathComparer);

    private CropTeacherRefillApproachNativeTrace(CropTeacherRefillApproachCompilation compilation,
        string receiptPath, string afterPath, string runId)
    {
        this.compilation = compilation;
        compilation.ValidateInputsUnchanged();
        receiptPath = Path.GetFullPath(receiptPath); afterPath = Path.GetFullPath(afterPath);
        var initialPaths = compilation.InputHashes.Keys.Concat(new[] { receiptPath, afterPath })
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        using var reads = InvocationReadSession.Start(initialPaths);
        var initial = new CropTeacherRetainedInputs(initialPaths);
        var native = NativeExecutionActionSourceReader.ReadQueue(compilation.QueuePath, compilation.SourcePath,
            receiptPath, afterPath, runId, compilation.CandidateId);
        Require(native.Queue.QueueId == compilation.QueueId && native.Actions.Length == native.Queue.Items.Length,
            "native trace changed the original compiled queue");
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(receiptPath, "original refill native trace");
        var steps = raw.GetProperty("step_results").EnumerateArray().ToArray();
        var retained = new Dictionary<string, string>(initial.ReadHashes(), CropTeacherSeedTransferNativeOutcome.PathComparer);
        var summaries = new List<CropTeacherRefillObservedStep>();
        string? captureSession = null;
        for (var index = 0; index < native.Actions.Length; index++)
        {
            var frame = native.Actions[index];
            CropTeacherNativeStepBoundary.Verify(frame.Before, frame.After, native.Before,
                native.Receipt.StepResults[index], compilation.TotalDay, "Original refill trace");
            var resultPath = Path.GetFullPath(steps[index].GetProperty("execution_path").GetString()!);
            var result = CurrentTeacherFrontierSupport.Read<JsonElement>(resultPath, "original refill native result");
            var journalPath = Path.GetFullPath(result.GetProperty("execution_request_journal_path").GetString()!);
            foreach (var (label, path) in new[] { ("queue", compilation.QueuePath), ("result", resultPath),
                         ("before", frame.BeforeSnapshotPath), ("after", frame.AfterSnapshotPath),
                         ("journal", journalPath), ("request_gzip", journalPath + ".gz") })
            {
                var hash = frame.SourceSha256[label];
                Require(!retained.TryGetValue(path, out var previous) || previous == hash,
                    "native trace reused a retained path with another identity");
                retained[path] = hash;
                if (label == "request_gzip") requestBlobs[path] = hash;
            }
            Require(frame.Request.OptionId is "executor.move_to_tile" or "executor.clear_obstacle" &&
                    frame.Result.ActionOriginObservation is not null &&
                    NativeExecutionSourceSupport.TotalDay(frame.Before) == compilation.TotalDay &&
                    NativeExecutionSourceSupport.TotalDay(frame.After) == compilation.TotalDay &&
                    Location(frame.Before) == "Farm" && Location(frame.After) == "Farm",
                "native refill trace lacks its original same-day Farm scope/observation");
            var summary = NativeOutputObservationBoundary.Read(frame.Request, frame.Result.ActionOriginObservation!,
                frame.Before.GameTick, frame.After.GameTick, "Farm");
            captureSession ??= summary.CaptureSessionId;
            Require(summary.CaptureSessionId == captureSession, "native trace crossed observation-process reference namespaces");
            summaries.Add(new(frame.Item.QueueItemId, frame.Item.OptionId, summary));
        }
        Require(summaries.Where(step => step.OptionId == "executor.clear_obstacle").Select(step => step.QueueItemId)
                .SequenceEqual(compilation.ClearanceQueueItemIds, StringComparer.Ordinal),
            "native trace changed the original explicit repair identities/order");
        var final = native.After;
        Require(NativeExecutionSourceSupport.Value(final, "player", "tile_x").GetInt32() == compilation.Target.TileX &&
                NativeExecutionSourceSupport.Value(final, "player", "tile_y").GetInt32() == compilation.Target.TileY,
            "native refill trace did not reach the original approach tile");
        inputs = new(retained.Keys.Where(path => !requestBlobs.ContainsKey(path)).ToArray());
        Require(inputs.ReadHashes().All(pair => retained[pair.Key] == pair.Value),
            "native trace source changed while retaining original frame evidence");
        initial.Validate();
        Steps = summaries.AsReadOnly();
        AfterSnapshotPath = afterPath;
        ValidateInputsUnchanged();
    }

    public static CropTeacherRefillApproachNativeTrace Open(CropTeacherRefillApproachCompilation compilation,
        string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId);

    public IReadOnlyList<CropTeacherRefillObservedStep> Steps { get; }
    public string AfterSnapshotPath { get; }
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes().Concat(requestBlobs)
        .ToDictionary(pair => pair.Key, pair => pair.Value, CropTeacherSeedTransferNativeOutcome.PathComparer);
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public void ValidateInputsUnchanged()
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        compilation.ValidateInputsUnchanged(); inputs.Validate();
        foreach (var (path, hash) in requestBlobs)
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && CurrentTeacherFrontierSupport.HashFile(path) == hash,
                "native trace compressed original request changed");
    }

    private static string Location(SnapshotEnvelope snapshot) =>
        NativeExecutionSourceSupport.Value(snapshot, "player", "location_id").GetString() ?? string.Empty;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Crop refill native trace: " + reason + ".");
    }
}
