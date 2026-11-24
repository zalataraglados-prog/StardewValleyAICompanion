using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherHarvest> SettleCropTeacherHarvestPickupAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, CropTeacherManagedSeedOperation operation, CropTeacherHarvest.NativeBatch batch)
    {
        if (!batch.NeedsPickup) return batch.Complete();
        var manifest = CropTeacherManagedSeedOwner.HarvestPickupSettlementPath(operation);
        var root = Path.GetDirectoryName(manifest)!;
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("Owned harvest pickup already has retained state; never repeat a claimed execution.");
        var files = new List<CropTeacherHarvestPickupFiles>();
        var fullUrl = SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true);
        var runtime = options.WithOwnedSupportingQueueBoundary();
        runtime.ManifestPath = Path.Combine(root, "runtime.json");
        Directory.CreateDirectory(runtime.SnapshotDir);
        while (batch.NeedsPickup)
        {
            var iteration = files.Count + 1;
            var suffix = iteration.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            var beforePath = Path.Combine(runtime.SnapshotDir, "before-snapshot-" + suffix + ".json");
            var frame = await ReadExecutionSnapshotAsync(http, runtime, forceRefresh: true,
                snapshotUrlOverride: fullUrl, snapshotProfileOverride: "full").ConfigureAwait(false);
            await ContentAddressedJsonArtifactStore.WriteAsync(beforePath, frame.Json, runtime.SnapshotArtifactMode).ConfigureAwait(false);
            var (command, observed, sourceHash) = batch.PreparePickupOrObserve(beforePath);
            var ingest = await PostJsonStringAsync(http, SnapshotIngestUrl(runtime, "full"), frame.Json).ConfigureAwait(false);
            if (ingest["accepted"]?.GetValue<bool>() != true || ReadString(ingest, "state_hash") != sourceHash)
                throw new InvalidDataException("Owned harvest pickup source was not accepted by the backend.");
            if (observed is not null) { files.Add(observed); continue; }
            var pickup = command ?? throw new InvalidDataException("Owned harvest pickup capture produced no action or observation.");
            var queuePath = Path.Combine(root, "pickup-" + suffix + "-queue.json");
            var modelPath = Path.Combine(root, "pickup-" + suffix + "-model.json");
            await WriteTypedJsonAsync(queuePath, pickup.Queue).ConfigureAwait(false);
            await WriteTypedJsonAsync(modelPath, pickup.Model).ConfigureAwait(false);
            var queue = JsonSerializer.SerializeToNode(pickup.Queue, JsonOptions)!.AsObject();
            var receiptPath = Path.Combine(runtime.SnapshotDir, "execution-" + suffix + ".json");
            var afterPath = Path.Combine(runtime.SnapshotDir, "after-snapshot-" + suffix + ".json");
            var bound = new CropTeacherHarvestPickupFiles(beforePath, queuePath, receiptPath, afterPath);
            await WriteTypedJsonAsync(Path.Combine(root, "pickup-" + suffix + "-claim.json"), bound).ConfigureAwait(false);
            var execution = await ExecuteRuntimeTestHarnessCoreAsync(http, executorHttp, runtime, iteration, beforePath,
                frame.Snapshot, queue, pickup.Queue.StateHash, pickup.Queue.QueueId, modelPath, string.Empty, queuePath, beforePath,
                pickup.Queue.StateHash, pickup.CandidateId, -1, queuePath, null, new List<JsonObject>(),
                useDailyPlanDecisionLease: false, executionSnapshotUrlOverride: fullUrl, executionSnapshotProfileOverride: "full").ConfigureAwait(false);
            if (!ManagedResumeSamePath(ReadString(execution, "execution_path"), receiptPath) ||
                !ManagedResumeSamePath(ReadString(execution, "after_snapshot_path"), afterPath))
                throw new InvalidDataException("Owned harvest pickup receipt differs from its claimed artifact paths.");
            batch.ObservePickup(pickup, bound);
            files.Add(bound);
        }
        await WriteTypedJsonAsync(manifest, files).ConfigureAwait(false);
        return batch.Complete(manifest);
    }
}
