using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherManagedSeedOperation> PrepareCropTeacherOwnedWateringAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, MoneyRevenueCropNativeProductionRunPlan runPlan,
        MoneyRevenueCropNativeProductionRunPlanRequest runRequest, CropTeacherManagedSeedOwner owner, string runtimeDirectory)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: true);
        if (step.Stage != CropTeacherRunStageKinds.ExecuteCropBatch || step.ActionBatch?.OptionId != "executor.water_crop")
            throw new InvalidDataException("Owned watering cannot replace a non-watering stage.");
        return await owner.CompileAndAdmitOriginalWateringAsync(async () =>
        {
            var sourcePath = owner.CurrentSnapshotPath;
            var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
            var stateHash = ReadString(source, "state_hash");
            var prefix = CropTeacherArtifactPrefix(options, iteration, "batch");
            var refillPrefix = CropTeacherArtifactPrefix(options, iteration, "watering-refill-approach");
            var receiptPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { prefix + "-model-action.json", prefix + "-compiled-queue.json", receiptPath, afterPath,
                    refillPrefix + "-model-plan.json", refillPrefix + "-compiled-queue.json" }
                .Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned watering needs new artifact slots; retain/recover existing compilation instead of overwriting it.");
            var compiled = await BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, runPlan, runRequest,
                sourcePath, source, stateHash).ConfigureAwait(false);
            var refill = compiled.InterruptKind == "crop_teacher_watering_refill_approach";
            if (compiled.Queue is null || (compiled.InterruptKind.Length != 0 && !refill) || compiled.ExecutionStepOverride is not null || compiled.SitePreparationComplete ||
                compiled.StateHash != stateHash || !string.Equals(Path.GetFullPath(compiled.BeforeSnapshotPath), Path.GetFullPath(sourcePath),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Original crop control selected an interrupt, not the admitted watering batch.");
            await WriteJsonAsync(prefix + "-compiled-queue.json", compiled.Queue).ConfigureAwait(false);
            return new(step, compiled.ModelPlanPath, prefix + "-compiled-queue.json", receiptPath, afterPath, runtimeDirectory)
                { IsRefillApproach = refill };
        }).ConfigureAwait(false);
    }

    private static Task ExecuteCropTeacherOwnedWateringAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
        ExecuteCropTeacherOwnedSupportingAsync(http, executorHttp, options, iteration, owner, operation, watering: true);
}
