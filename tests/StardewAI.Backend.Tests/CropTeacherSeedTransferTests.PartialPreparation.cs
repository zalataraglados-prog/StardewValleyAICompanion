using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task OwnedPartialTransferUsesOriginalMarkerPlanCompilerAndFullRecoveryWithoutAnotherCommit()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f.SourcePath, f.LedgerPath, f.RequestPath);
        using var files = new OwnerFiles(runtime.Options, runtime.Client, f.SourcePath, f.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation,
            () => runtime.Prepare(operation.Scopes.Transfer, compile: (_, _) => throw new IOException("Original transfer compilation interrupted."), runtimeDirectory: files.Native)));
        MoneyRevenueCropNativeLifecycleTransitionRequest? transition = null;
        var prepared = await owner.ContinueSeedPreparationAsync(runtime.Client, files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.Options, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", runtime.Client, runtime.Options, diagnostic, request),
            async context =>
            {
                var marker = Assert.IsType<CropTeacherSeedTransferMarker>(context.Transfer);
                var plan = Assert.IsType<CropTeacherSeedTransferExecutionPlan>(context.TransferPlan);
                var model = new SmallModelActionEnvelope
                {
                    ModelOutputId = "model-output:" + plan.CandidateId, SourceModel = "crop_teacher_owned_seed_transfer.v1",
                    StateHash = f.Before.StateHash, GoalId = "earn_money", Actor = runtime.Options.TargetActor,
                    ExecutionMode = runtime.Options.TargetExecutionMode, Actions = new[] { marker.BuildAction(plan.StandTileX, plan.StandTileY) }
                };
                using var reply = await runtime.Client.PostAsJsonAsync(runtime.Options.BackendUrl + "/api/v1/small-model/action-queue/compile", model);
                Assert.True(reply.IsSuccessStatusCode, await reply.Content.ReadAsStringAsync());
                var queue = (await reply.Content.ReadFromJsonAsync<ActionQueueEnvelope>())!;
                transition = f.NativeTransition(marker, compiledQueue: queue);
                var path = Path.Combine(context.OutputDirectory, "compiled-seed-queue.json");
                await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, JsonSerializer.Serialize(queue, JsonOptions));
                return path;
            });
        Assert.IsType<CropTeacherSeedTransferRuntime>(prepared);
        Assert.Equal(1, runtime.Handler.Commits);
        await owner.ExecuteSeedAsync(operation, async original =>
        {
            original.ValidateDispatchQueue(transition!.QueuePath);
            runtime.ObserveAfter();
            return await original.CompleteAsync(transition);
        });
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        await resumed.RecoverPendingAsync(runtime.Client, files.Recovery + "-native", files.Post);
        Assert.Null(resumed.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }
}
