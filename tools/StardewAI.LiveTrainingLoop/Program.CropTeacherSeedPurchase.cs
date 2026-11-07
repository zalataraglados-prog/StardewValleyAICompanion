using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherControlQueueBuildResult> BuildCropTeacherSeedQueueAsync(
        HttpClient http, LiveTrainingOptions options, int iteration, CropTeacherRunStep step,
        string beforeSnapshotPath, JsonObject beforeSnapshot, string stateHash,
        CropTeacherSeedPurchaseSupport? resourceSupport = null)
    {
        if (resourceSupport is null)
            return await CompileCropTeacherSeedQueueAsync(http, options, iteration, step,
                beforeSnapshotPath, beforeSnapshot, stateHash, "earn_money");
        if (!options.UseRuntimeTestHarnessExecutor || !options.EmitQueueExecutionReceipt ||
            resourceSupport.CommitmentId != step.CommitmentId ||
            resourceSupport.MarkerRequest.StateHash != stateHash ||
            Path.GetFullPath(beforeSnapshotPath) != resourceSupport.SnapshotPath)
            throw new InvalidDataException("Managed crop seed support is not bound to the original native control step.");
        CropTeacherControlQueueBuildResult? compiled = null;
        var output = CropTeacherArtifactPrefix(options, iteration, "seed-support");
        var runtimeOptions = new CropTeacherSeedPurchaseRuntimeOptions(options.BackendUrl, output,
            options.RunId, options.PolicyTrajectoryExecutorVersion);
        var prepared = await CropTeacherSeedPurchaseRuntime.PrepareAsync(http, runtimeOptions, resourceSupport,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (diagnostic, request) => PostJournaledPortfolioSettlementAsync(http, options, diagnostic, request),
            WritePortfolioJournalFileAsync,
            async () =>
            {
                compiled = await CompileCropTeacherSeedQueueAsync(http, options, iteration, step,
                    beforeSnapshotPath, beforeSnapshot, stateHash, "daily.closed_loop");
                var queuePath = Path.Combine(output, "compiled-seed-queue.json");
                await WritePortfolioJournalFileAsync(queuePath, (compiled.Queue ??
                    throw new InvalidDataException("Managed crop seed compilation returned no queue.")).ToJsonString(JsonOptions));
                return queuePath;
            });
        return compiled! with { SeedPurchaseSupport = prepared };
    }
}
