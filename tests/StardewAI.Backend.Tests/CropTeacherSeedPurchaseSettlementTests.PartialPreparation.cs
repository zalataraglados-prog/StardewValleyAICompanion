using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("lost_marker_reply")]
    [InlineData("raw_queue_retained")]
    [InlineData("no_summary")]
    [InlineData("pin_ack_lost")]
    [InlineData("pin_not_written")]
    [InlineData("queue_ack_lost")]
    [InlineData("retry_pure_compile")]
    [InlineData("continuation_ack_lost")]
    [InlineData("view_ack_lost")]
    public async Task OwnedPartialPreparationRetainsOriginalFailuresAndRecoversDurableBoundaries(string mode)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        var failPin = true;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (failPin && mode == "pin_not_written") CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "operation-000001-prepared.json");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (failPin && mode == "continuation_ack_lost" && Path.GetFileName(path) == "operation-000001-continuation.json")
                throw new IOException("Durable continuation ownership acknowledgement lost.");
            if (failPin && mode == "view_ack_lost" && Path.GetFileName(path) == "continued-preparation.json")
                throw new IOException("Durable completed preparation view acknowledgement lost.");
            if (failPin && mode == "pin_ack_lost" && Path.GetFileName(path) == "operation-000001-prepared.json")
                throw new IOException("Durable continuation pin acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        if (mode is "lost_marker_reply" or "no_summary") runtime.BeforeWrite = path =>
            CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, mode == "lost_marker_reply" ? "marker-reply.json" : "preparation-summary.json");
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            compile: () =>
            {
                if (mode == "raw_queue_retained") File.Copy(transition.QueuePath, Path.Combine(files.Native, "compiled-seed-queue.json"));
                throw new IOException("Original preparation interrupted before a completed summary.");
            }, selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native)));
        var originalFiles = Directory.EnumerateFiles(files.Native).ToDictionary(path => path, CropTeacherSeedRecoveryTestSupport.Hash);
        var calls = 0;
        async Task<string> Compile(CropTeacherSeedCompilationContext context)
        {
            calls++;
            var path = Path.Combine(context.OutputDirectory, "compiled-seed-queue.json");
            if (mode == "retry_pure_compile" && calls == 1) throw new IOException("Pure compiler failed without native dispatch.");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, File.ReadAllText(transition.QueuePath));
            if (mode == "queue_ack_lost" && calls == 1) throw new IOException("Original compiled queue durable; callback acknowledgement lost.");
            return path;
        }
        var firstFails = mode is "pin_ack_lost" or "pin_not_written" or "queue_ack_lost" or "retry_pure_compile" or "continuation_ack_lost" or "view_ack_lost";
        ICropTeacherSeedRuntime? prepared = null;
        if (firstFails) await Assert.ThrowsAsync<IOException>(() => ContinuePartial(files, owner, runtime, Compile));
        else prepared = await ContinuePartial(files, owner, runtime, Compile);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        if (firstFails)
        {
            await owner.DisposeAsync();
            await using var resumed = await files.Open();
            failPin = false;
            if (mode == "pin_ack_lost") prepared = await RestorePrepared(files, resumed, runtime, files.Recovery + "-restart");
            else prepared = await ContinuePartial(files, resumed, runtime, Compile, files.Recovery + "-restart");
            var pending = resumed.Pending!;
            await resumed.ExecuteSeedAsync(pending, async original =>
            {
                original.ValidateDispatchQueue(transition.QueuePath);
                runtime.ObserveAfter();
                return await original.CompleteAsync(transition);
            });
            await resumed.AcceptLiveAsync(pending, prepared);
            Assert.Null(resumed.Pending);
        }
        else
        {
            await owner.ExecuteSeedAsync(operation, async original =>
            {
                original.ValidateDispatchQueue(transition.QueuePath);
                runtime.ObserveAfter();
                return await original.CompleteAsync(transition);
            });
            await owner.AcceptLiveAsync(operation, prepared!);
        }
        foreach (var row in originalFiles) Assert.Equal(row.Value, CropTeacherSeedRecoveryTestSupport.Hash(row.Key));
        Assert.Equal(mode == "raw_queue_retained" ? 0 : mode == "retry_pure_compile" ? 2 : 1, calls);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(mode == "lost_marker_reply" ? 1 : 0, runtime.Handler.Lookups);
    }

    private static Task<ICropTeacherSeedRuntime> ContinuePartial(OwnerFiles files, CropTeacherManagedSeedOwner owner,
        RuntimeFixture runtime, Func<CropTeacherSeedCompilationContext, Task<string>> compile, string? evidence = null) =>
        owner.ContinueSeedPreparationAsync(runtime.Client, evidence ?? files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), compile);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedPartialPreparationContinuesTheOriginalCompilerWithoutRecommitAndSurvivesNativeRecovery(bool restartAfterNative)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            compile: () => throw new IOException("Original compiler interrupted after marker readback."),
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native)));
        var failedPath = Path.Combine(files.Native, "preparation-summary.json");
        var failedHash = CropTeacherSeedRecoveryTestSupport.Hash(failedPath);
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        var compiles = 0;
        var prepared = await resumed.ContinueSeedPreparationAsync(runtime.Client, files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", runtime.Client, runtime.LiveOptions, diagnostic, request),
            async context =>
            {
                compiles++;
                Assert.Equal(operation.SupportStepId, resumed.Pending!.SupportStepId);
                var queuePath = Path.Combine(context.OutputDirectory, "compiled-seed-queue.json");
                await InvokeLive<Task>("WritePortfolioJournalFileAsync", queuePath, File.ReadAllText(transition.QueuePath));
                return queuePath;
            });
        Assert.Equal(1, compiles);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(failedHash, CropTeacherSeedRecoveryTestSupport.Hash(failedPath));
        var pending = resumed.Pending!;
        await resumed.ExecuteSeedAsync(pending, async original =>
        {
            original.ValidateDispatchQueue(transition.QueuePath);
            runtime.ObserveAfter();
            return await original.CompleteAsync(transition);
        });
        if (!restartAfterNative) await resumed.AcceptLiveAsync(pending, prepared);
        await resumed.DisposeAsync();
        await using var accepted = await files.Open();
        if (restartAfterNative) await accepted.RecoverPendingAsync(runtime.Client, files.Recovery + "-native", files.Post);
        else await accepted.RestoreHeadAsync(runtime.Client, files.Recovery + "-native", files.Post);
        Assert.Null(accepted.Pending);
        Assert.Equal(failedHash, CropTeacherSeedRecoveryTestSupport.Hash(failedPath));
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }
}
