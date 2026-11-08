using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("pinned")]
    [InlineData("pin_lost_ack")]
    [InlineData("pin_not_written")]
    [InlineData("marker_reply_missing")]
    public async Task OwnedPreparedRestoreUsesOriginalPreparationWithoutRecommitAndExecutesOnce(string mode)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (mode == "pin_not_written") CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "operation-000001-prepared.json");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "pin_lost_ack" && Path.GetFileName(path) == "operation-000001-prepared.json")
                throw new IOException("Durable preparation pin acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        Task<CropTeacherSeedPurchaseRuntime> Prepare() => owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory));
        if (mode.StartsWith("pin_", StringComparison.Ordinal)) await Assert.ThrowsAsync<IOException>(Prepare);
        else
        {
            var original = await Prepare();
            Assert.Throws<InvalidOperationException>(() => original.ValidateDispatchQueue(transition.QueuePath));
        }
        if (mode == "marker_reply_missing") File.Delete(Path.Combine(files.Native, "marker-reply.json"));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        var pending = resumed.Pending!;
        var restored = await RestorePrepared(files, resumed, runtime);
        Assert.IsType<CropTeacherSeedPurchaseRuntime>(restored);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(mode == "marker_reply_missing" ? 1 : 0, runtime.Handler.Lookups);
        Assert.Throws<InvalidOperationException>(() => restored.ValidateDispatchQueue(transition.QueuePath));
        await resumed.ExecuteSeedAsync(pending, async original =>
        {
            Assert.Same(restored, original);
            var record = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")))!;
            Assert.True(record["execution_loop_invocation_claimed"]!.GetValue<bool>());
            Assert.Null(record["native_request_performed"]);
            original.ValidateDispatchQueue(transition.QueuePath);
            runtime.ObserveAfter();
            return await original.CompleteAsync(transition);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.ExecuteSeedAsync(pending, _ => Task.FromResult(true)));
        await resumed.AcceptLiveAsync(pending, restored);
        Assert.Null(resumed.Pending);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        await resumed.DisposeAsync();
        await using var accepted = await files.Open();
        await accepted.RestoreHeadAsync(runtime.Client, files.Recovery + "-head", files.Post);
        Assert.False(accepted.HeadRecoveryRequired);
    }

    [Theory]
    [InlineData("dispatch_ack_lost")]
    [InlineData("callback_failed")]
    public async Task OwnedExecutionClaimPrecedesCallbackAndUnknownInvocationCannotRepeat(string mode)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "dispatch_ack_lost" && Path.GetFileName(path) == "operation-000001-dispatch.json")
                throw new IOException("Durable dispatch claim acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory));
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteSeedAsync<bool>(operation, _ =>
        {
            calls++;
            throw new IOException("Original execution loop failed with unknown dispatch state.");
        }));
        Assert.Equal(mode == "dispatch_ack_lost" ? 0 : 1, calls);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateDispatchQueue(transition.QueuePath));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => RestorePrepared(files, resumed, runtime));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.ExecuteSeedAsync(resumed.Pending!, _ => Task.FromResult(true)));
        Assert.NotNull(resumed.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }

    private static Task<ICropTeacherSeedRuntime> RestorePrepared(OwnerFiles files, CropTeacherManagedSeedOwner owner, RuntimeFixture runtime) =>
        owner.RestorePreparedSeedAsync(runtime.Client, files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", runtime.Client, runtime.LiveOptions, diagnostic, request));
}
