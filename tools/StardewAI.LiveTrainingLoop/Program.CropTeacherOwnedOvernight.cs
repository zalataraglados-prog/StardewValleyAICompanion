using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherManagedSeedOperation> PrepareCropTeacherOwnedOvernightAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, MoneyRevenueCropNativeProductionRunPlan runPlan,
        MoneyRevenueCropNativeProductionRunPlanRequest runRequest, CropTeacherManagedSeedOwner owner, string runtimeDirectory)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        owner.ValidateOriginalOvernightSaveBinding(options);
        if (step.Stage != CropTeacherRunStageKinds.NativeSleep || step.ActionBatch is not null)
            throw new InvalidDataException("Owned overnight cannot replace a non-sleep stage.");
        return await owner.CompileAndAdmitOriginalOvernightAsync(async () =>
        {
            var sourcePath = owner.CurrentSnapshotPath;
            var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
            var stateHash = ReadString(source, "state_hash");
            var prefix = CropTeacherArtifactPrefix(options, iteration, "sleep");
            var receiptPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { prefix + "-daily-plan.json", prefix + "-daily-plan-response.json", prefix + "-ranking.json", prefix + "-compiled-queue.json", receiptPath, afterPath }
                .Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned overnight needs new artifact slots; recover retained sleep instead of overwriting it.");
            var compiled = await BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, runPlan, runRequest,
                sourcePath, source, stateHash).ConfigureAwait(false);
            if (compiled.Queue is null || compiled.InterruptKind.Length != 0 || compiled.ExecutionStepOverride is not null || compiled.SitePreparationComplete ||
                compiled.StateHash != stateHash || !string.Equals(Path.GetFullPath(compiled.BeforeSnapshotPath), Path.GetFullPath(sourcePath),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Original crop control selected an interrupt, not the admitted overnight.");
            await WriteJsonAsync(prefix + "-compiled-queue.json", compiled.Queue).ConfigureAwait(false);
            return new(step, compiled.ModelPlanPath, prefix + "-compiled-queue.json", receiptPath, afterPath, runtimeDirectory);
        }).ConfigureAwait(false);
    }

    private static Task ExecuteCropTeacherOwnedOvernightAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
        ExecuteCropTeacherOwnedSupportingCoreAsync(http, executorHttp, options, iteration, owner, operation, CropTeacherSupportingOperationKind.Overnight);
}
