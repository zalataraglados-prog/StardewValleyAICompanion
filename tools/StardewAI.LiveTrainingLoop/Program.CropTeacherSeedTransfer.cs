using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherControlQueueBuildResult> BuildCropTeacherSeedTransferQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, string beforeSnapshotPath,
        JsonObject beforeSnapshot, string stateHash, CropTeacherSeedTransferSupport support, string? runtimeDirectory = null)
    {
        var request = support.PreparationRequest;
        if (!options.UseRuntimeTestHarnessExecutor || !options.EmitQueueExecutionReceipt ||
            step.Stage != CropTeacherRunStageKinds.TransferSeedSupply || step.CommitmentId != support.CommitmentId ||
            step.SeedReservationId != support.ReservationId || step.TotalDay != support.TotalDay ||
            step.SeedQualifiedItemId != request.MaterialTransferPreparation!.Intent.QualifiedItemId ||
            request.MaterialTransferPreparation.Intent.Quantity > step.RemainingQuantity ||
            !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(step.SeedTransferIntent, JsonOptions),
                JsonSerializer.SerializeToNode(request.MaterialTransferPreparation.Intent, JsonOptions)) ||
            request.StateHash != stateHash || !string.Equals(Path.GetFullPath(beforeSnapshotPath), support.SnapshotPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Managed crop seed transfer is not bound to the original native control step.");
        var output = runtimeDirectory ?? CropTeacherArtifactPrefix(options, iteration, "seed-transfer");
        CropTeacherControlQueueBuildResult? compiled = null;
        var prepared = await CropTeacherSeedTransferRuntime.PrepareAsync(http,
            new(options.BackendUrl, output, options.RunId, options.PolicyTrajectoryExecutorVersion), support,
            (diagnostic, mutation) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, mutation),
            (diagnostic, mutation) => PostJournaledPortfolioSettlementAsync(http, options, diagnostic, mutation),
            WritePortfolioJournalFileAsync, async (marker, plan) =>
            {
                var model = new SmallModelActionEnvelope
                {
                    ModelOutputId = "model-output:" + plan.CandidateId, SourceModel = "crop_teacher_owned_seed_transfer.v1",
                    StateHash = stateHash, GoalId = "earn_money", ExecutionMode = options.TargetExecutionMode,
                    Actor = options.TargetActor, Actions = new[] { marker.BuildAction(plan.StandTileX, plan.StandTileY) }
                };
                var modelPath = Path.Combine(output, "model-action.json");
                await WritePortfolioJournalFileAsync(modelPath, JsonSerializer.Serialize(model, JsonOptions));
                var queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile",
                    JsonSerializer.Serialize(model, JsonOptions));
                RequireCompiledQueue(queue, new[] { "executor.transfer_material" }, 1, "crop_teacher_seed_transfer_compile_invalid");
                var queuePath = Path.Combine(output, "compiled-seed-queue.json");
                await WritePortfolioJournalFileAsync(queuePath, queue.ToJsonString(JsonOptions));
                compiled = StandardResult(queue, modelPath, string.Empty, beforeSnapshotPath, beforeSnapshot, stateHash, options);
                return queuePath;
            }, expectedLocationId: step.TargetLocationId);
        return compiled! with { SeedTransferSupport = prepared };
    }
}
