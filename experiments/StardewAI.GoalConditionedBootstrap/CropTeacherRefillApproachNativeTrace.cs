using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherRefillObservedStep(string QueueItemId, string OptionId,
    NativeOutputObservationSummary Observation);

public sealed record CropTeacherRefillResourceEvidenceStep(string QueueItemId, string OptionId,
    NativeOutputResourceEvidence Evidence);

public sealed record CropTeacherRefillPickupInventoryStep(string QueueItemId, string OptionId,
    NativePickupInventorySummary Inventory);

public sealed record CropTeacherRefillResourceEffectStep(string QueueItemId, string OptionId,
    NativeRefillFrameResourceEffects Effects);

// Retains the original successful native queue. No dispatch or producer credit.
public sealed partial class CropTeacherRefillApproachNativeTrace
{
    private readonly CropTeacherRefillApproachCompilation compilation;
    private readonly CropTeacherRetainedInputs inputs;
    private readonly Dictionary<string, string> requestBlobs = new(CropTeacherSeedTransferNativeOutcome.PathComparer);

    private CropTeacherRefillApproachNativeTrace(CropTeacherRefillApproachCompilation compilation,
        string receiptPath, string afterPath, string runId, bool verifySnapshotInventoryContinuity,
        bool classifyResourceEvidence = false, bool requireResourceNeutrality = false, bool verifyPickupInventory = false,
        bool verifySelectedResourceEffects = false, string? nativeExecutorVersion = null, bool nativeObservedEffects = false)
    {
        this.compilation = compilation;
        Require(!nativeObservedEffects || nativeExecutorVersion is not null && !verifySelectedResourceEffects && !requireResourceNeutrality,
            "native observed refill requires its explicit native protocol, not a legacy resource assertion");
        compilation.ValidateInputsUnchanged();
        Require(!requireResourceNeutrality || compilation.ClearanceQueueItemIds.Count == 0,
            "resource-neutral approach cannot replace a compiled repair/resource settlement");
        receiptPath = Path.GetFullPath(receiptPath); afterPath = Path.GetFullPath(afterPath);
        var initialPaths = compilation.InputHashes.Keys.Concat(new[] { receiptPath, afterPath })
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        using var reads = InvocationReadSession.Start(initialPaths);
        var initial = new CropTeacherRetainedInputs(initialPaths);
        var native = nativeExecutorVersion is null
            ? NativeExecutionActionSourceReader.ReadQueue(compilation.QueuePath, compilation.SourcePath,
                receiptPath, afterPath, runId, compilation.CandidateId)
            : NativeExecutionActionSourceReader.ReadSelectedNativeActionQueue(compilation.QueuePath, compilation.SourcePath,
                receiptPath, afterPath, runId, compilation.CandidateId);
        Require(native.Queue.QueueId == compilation.QueueId && native.Actions.Length == native.Queue.Items.Length,
            "native trace changed the original compiled queue");
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(receiptPath, "original refill native trace");
        var steps = raw.GetProperty("step_results").EnumerateArray().ToArray();
        var retained = new Dictionary<string, string>(initial.ReadHashes(), CropTeacherSeedTransferNativeOutcome.PathComparer);
        var summaries = new List<CropTeacherRefillObservedStep>();
        var resourceEvidence = new List<CropTeacherRefillResourceEvidenceStep>();
        var pickupInventory = new List<CropTeacherRefillPickupInventoryStep>();
        var resourceEffects = new List<CropTeacherRefillResourceEffectStep>();
        var nativeOutcomes = new List<CropTeacherRefillNativeOutcomeStep>();
        string? captureSession = null;
        for (var index = 0; index < native.Actions.Length; index++)
        {
            var frame = native.Actions[index];
            if (nativeExecutorVersion is not null)
                Require(new Uri(frame.Endpoint, UriKind.Absolute).AbsolutePath ==
                    (nativeExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor
                        ? "/api/v1/product/execute" : "/api/v1/training/execute"),
                    "native refill declared executor differs from its verified request endpoint");
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
                    (nativeObservedEffects || requireResourceNeutrality || verifySelectedResourceEffects && frame.Request.OptionId == "executor.move_to_tile" ||
                        frame.Result.ActionOriginObservation is not null) &&
                    NativeExecutionSourceSupport.TotalDay(frame.Before) == compilation.TotalDay &&
                    NativeExecutionSourceSupport.TotalDay(frame.After) == compilation.TotalDay &&
                    Location(frame.Before) == "Farm" && Location(frame.After) == "Farm",
                "native refill trace lacks its original same-day Farm scope/observation");
            if (nativeObservedEffects)
            {
                VerifyNativeObservedTarget(frame);
                nativeOutcomes.Add(new(frame.Item.QueueItemId, frame.Item.OptionId, frame.Before.StateHash, frame.After.StateHash));
                continue;
            }
            if (requireResourceNeutrality)
            {
                Require(frame.Request.OptionId == "executor.move_to_tile" &&
                        FullShipmentRecoverySettlementBoundaryProjector.TryProject(frame.Before, out var first) &&
                        FullShipmentRecoverySettlementBoundaryProjector.TryProject(frame.After, out var next) && first == next,
                    "resource-neutral approach changed persistent resources/state or lacks its original projection");
                if (frame.Request.ActionOriginObservationScopeId is null)
                {
                    Require(frame.Result.ActionOriginObservation is null,
                        "unrequested native observation cannot be attached to a resource-neutral approach");
                    continue;
                }
                Require(frame.Result.ActionOriginObservation is not null,
                    "requested native observation is missing from the resource-neutral approach");
            }
            var effects = verifySelectedResourceEffects ? CropTeacherRefillResourceEffects.Read(frame.Request,
                frame.Result.ActionOriginObservation, frame.Before, frame.After, "Farm") : null;
            if (effects is not null)
            {
                resourceEffects.Add(new(frame.Item.QueueItemId, frame.Item.OptionId, effects));
                if (effects.Evidence is null) continue; // Original unobserved, strictly resource-neutral move.
            }
            var pickup = effects?.Pickup?.Inventory ?? (verifyPickupInventory
                ? NativeOutputObservationBoundary.ReadPickupInventory(frame.Request, frame.Result.ActionOriginObservation!,
                    frame.Before, frame.After, "Farm") : null);
            var classified = effects?.Evidence ?? pickup?.Evidence ?? (classifyResourceEvidence
                ? NativeOutputObservationBoundary.ReadResourceEvidence(frame.Request, frame.Result.ActionOriginObservation!,
                    frame.Before, frame.After, "Farm") : null);
            var summary = classified?.Observation ?? (verifySnapshotInventoryContinuity || requireResourceNeutrality
                ? NativeOutputObservationSnapshotBoundary.Read(frame.Request, frame.Result.ActionOriginObservation!,
                    frame.Before, frame.After, "Farm")
                : NativeOutputObservationBoundary.Read(frame.Request, frame.Result.ActionOriginObservation!,
                    frame.Before.GameTick, frame.After.GameTick, "Farm"));
            captureSession ??= summary.CaptureSessionId;
            Require(summary.CaptureSessionId == captureSession, "native trace crossed observation-process reference namespaces");
            summaries.Add(new(frame.Item.QueueItemId, frame.Item.OptionId, summary));
            if (classified is not null) resourceEvidence.Add(new(frame.Item.QueueItemId, frame.Item.OptionId, classified));
            if (pickup is not null) pickupInventory.Add(new(frame.Item.QueueItemId, frame.Item.OptionId, pickup));
        }
        Require((nativeObservedEffects ? nativeOutcomes.Where(step => step.OptionId == "executor.clear_obstacle").Select(step => step.QueueItemId) :
                summaries.Where(step => step.OptionId == "executor.clear_obstacle").Select(step => step.QueueItemId))
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
        ResourceEvidence = resourceEvidence.AsReadOnly();
        PickupInventory = pickupInventory.AsReadOnly();
        ResourceEffects = resourceEffects.AsReadOnly();
        NativeOutcomes = nativeOutcomes.AsReadOnly();
        NativeEngineEffectsObserved = nativeObservedEffects;
        AfterSnapshotPath = afterPath;
        VerifiedAfterSnapshot = native.After;
        ValidateInputsUnchanged();
        SnapshotInventoryContinuityVerified = verifySnapshotInventoryContinuity;
        ResourceNeutralBoundaryVerified = requireResourceNeutrality;
        PickupInventoryCaptureVerified = verifyPickupInventory;
        SelectedResourceEffectsVerified = verifySelectedResourceEffects;
    }

    // Calibration-only: validates captured observations, not their inventory snapshot denominator.
    public static CropTeacherRefillApproachNativeTrace Open(CropTeacherRefillApproachCompilation compilation,
        string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: false);

    // Stronger quantity-only gate on every original frame; not full metadata/world/producer proof.
    public static CropTeacherRefillApproachNativeTrace OpenWithSnapshotInventoryContinuity(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: true);

    public static CropTeacherRefillApproachNativeTrace OpenWithResourceEvidence(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: true,
            classifyResourceEvidence: true);

    public static CropTeacherRefillApproachNativeTrace OpenWithPickupInventory(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: true,
            verifyPickupInventory: true);

    public static CropTeacherRefillApproachNativeTrace OpenWithSelectedResourceEffects(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: false,
            verifySelectedResourceEffects: true);

    // No repair or pickup exemption: every frame must preserve the original full resource boundary.
    public static CropTeacherRefillApproachNativeTrace OpenResourceNeutral(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath, string runId) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: false,
            requireResourceNeutrality: true);

    public static CropTeacherRefillApproachNativeTrace OpenNativeResourceNeutral(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath,
        string runId, string executorVersion) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: false,
            requireResourceNeutrality: true, nativeExecutorVersion: RequireNativeExecutorVersion(executorVersion));

    public static CropTeacherRefillApproachNativeTrace OpenNativeWithSelectedResourceEffects(
        CropTeacherRefillApproachCompilation compilation, string executionReceiptPath, string afterSnapshotPath,
        string runId, string executorVersion) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: false,
            verifySelectedResourceEffects: true, nativeExecutorVersion: RequireNativeExecutorVersion(executorVersion));

    public IReadOnlyList<CropTeacherRefillObservedStep> Steps { get; }
    public IReadOnlyList<CropTeacherRefillResourceEvidenceStep> ResourceEvidence { get; }
    public IReadOnlyList<CropTeacherRefillPickupInventoryStep> PickupInventory { get; }
    public IReadOnlyList<CropTeacherRefillResourceEffectStep> ResourceEffects { get; }
    public string AfterSnapshotPath { get; }
    internal SnapshotEnvelope VerifiedAfterSnapshot { get; }
    public bool SnapshotInventoryContinuityVerified { get; }
    public bool ResourceNeutralBoundaryVerified { get; }
    public bool PickupInventoryCaptureVerified { get; }
    public bool SelectedResourceEffectsVerified { get; }
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes().Concat(requestBlobs)
        .ToDictionary(pair => pair.Key, pair => pair.Value, CropTeacherSeedTransferNativeOutcome.PathComparer);
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => NativeEngineEffectsObserved;
    public bool FormalProductTrainingAuthorized => false;
    public void ValidateInputsUnchanged()
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        compilation.ValidateInputsUnchanged(); inputs.Validate();
        foreach (var (path, hash) in requestBlobs)
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && CurrentTeacherFrontierSupport.HashFile(path) == hash,
                "native trace compressed original request changed");
    }

    private static string RequireNativeExecutorVersion(string value)
    {
        Require(value is PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor or PolicyTrajectoryVersionPins.ProductExecutor,
            "explicit native refill requires a known native executor version");
        return value;
    }

    private static string Location(SnapshotEnvelope snapshot) =>
        NativeExecutionSourceSupport.Value(snapshot, "player", "location_id").GetString() ?? string.Empty;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Crop refill native trace: " + reason + ".");
    }
}
