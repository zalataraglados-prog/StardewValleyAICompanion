using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherControlQueueBuildResult> BuildCropTeacherSeedPlantingQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, string beforeSnapshotPath,
        JsonObject beforeSnapshot, string stateHash, CropTeacherSeedPlantingSupport support)
    {
        if (!options.UseRuntimeTestHarnessExecutor || !options.EmitQueueExecutionReceipt || options.UseTeacherPreferenceQueue ||
            options.MaxQueueItemAttempts is < 1 or > StardewAI.Contracts.Training.TeacherEvidenceRolloutLimits.MaxQueueItems ||
            !string.IsNullOrWhiteSpace(options.ExecutorOptionId) || options.TargetExecutionMode != ExecutionTargetProfiles.TrainingSingleplayer ||
            step.Stage != CropTeacherRunStageKinds.ExecuteCropBatch || step.ActionBatch?.OptionId != "executor.plant_seed" ||
            step.CommitmentId != support.CommitmentId || step.TotalDay != support.TotalDay ||
            step.ActionBatch.CommitmentId != support.CommitmentId || step.ActionBatch.SeedQualifiedItemId != step.SeedQualifiedItemId ||
            support.MarkerRequest.StateHash != stateHash || !string.Equals(Path.GetFullPath(beforeSnapshotPath), support.SnapshotPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Managed planting differs from the original native control batch.");
        var output = CropTeacherArtifactPrefix(options, iteration, "seed-planting");
        CropTeacherControlQueueBuildResult? compiled = null;
        var runtime = await CropTeacherSeedPlantingRuntime.PrepareAsync(http,
            new(options.BackendUrl, output, options.RunId, options.PolicyTrajectoryExecutorVersion), support,
            step.ActionBatch, step.SeedReservationId,
            (diagnostic, mutation) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, mutation),
            (diagnostic, mutation) => PostJournaledPortfolioSettlementAsync(http, options, diagnostic, mutation),
            WritePortfolioJournalFileAsync, async (marker, batch) =>
            {
                var model = new SmallModelActionEnvelope
                {
                    ModelOutputId = "model-output:" + support.MarkerRequest.PortfolioId, SourceModel = "crop_teacher_owned_planting.v1",
                    StateHash = stateHash, GoalId = "earn_money", ExecutionMode = options.TargetExecutionMode,
                    Actor = options.TargetActor, Actions = marker.BuildActions(batch, step.SeedReservationId)
                };
                var modelPath = Path.Combine(output, "model-action.json");
                await WritePortfolioJournalFileAsync(modelPath, JsonSerializer.Serialize(model, JsonOptions));
                var queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile", JsonSerializer.Serialize(model, JsonOptions));
                RequireCompiledQueue(queue, Enumerable.Repeat("executor.plant_seed", model.Actions.Length).ToArray(),
                    model.Actions.Length, "crop_teacher_owned_planting_compile_invalid");
                var queuePath = Path.Combine(output, "compiled-seed-queue.json");
                await WritePortfolioJournalFileAsync(queuePath, queue.ToJsonString(JsonOptions));
                compiled = StandardResult(queue, modelPath, string.Empty, beforeSnapshotPath, beforeSnapshot, stateHash, options);
                return queuePath;
            });
        return compiled! with { SeedPlantingSupport = runtime };
    }
}
