using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OwnerPreparationInvokesTheOriginalRuntimeOnceAndPinsBeforeReturningIt()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var admission = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-intent.json")))!;
        Assert.Equal(64, admission["original_intent_sha256"]!.GetValue<string>().Length);
        var calls = 0;
        var prepared = await owner.PrepareSeedAsync(operation, async () =>
        {
            calls++;
            Assert.True(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-started.json")));
            return await runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory);
        });
        Assert.True(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-prepared.json")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareSeedAsync(operation, () => { calls++; return Task.FromResult(prepared); }));
        Assert.Equal(1, calls);
        await owner.ExecuteSeedAsync(operation, async original =>
        {
            original.ValidateDispatchQueue(transition.QueuePath);
            runtime.ObserveAfter();
            return await prepared.CompleteAsync(transition);
        });
        await owner.AcceptLiveAsync(operation, prepared);
        Assert.Null(owner.Pending);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        await restarted.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerPreparationReconstructsAnUnpinnedRecordOnlyAfterFullOriginalNativeVerification(bool lostReply)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        // The original standalone path can have complete retained effects before the controller pins its record.
        var prepared = await runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory);
        runtime.ObserveAfter();
        if (lostReply) runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "acquisition-reply.json");
        if (lostReply) await Assert.ThrowsAsync<IOException>(() => prepared.CompleteAsync(transition));
        else await prepared.CompleteAsync(transition);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-prepared.json")));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        await resumed.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
        Assert.Null(resumed.Pending);
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-prepared.json")))!;
        Assert.True(record["reconstructed_after_original_native_verification"]!.GetValue<bool>());
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(lostReply ? 1 : 0, runtime.Handler.Lookups);
    }

    [Fact]
    public async Task OwnerPreparationDoesNotTurnAnUncertainPinOrMissingNativeProofIntoARepeat()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (Path.GetFileName(path) == "operation-000001-prepared.json") throw new IOException("Original preparation pin durable; acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory)));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        var pending = resumed.Pending!;
        var invoked = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.PrepareSeedAsync(pending, () =>
        {
            invoked = true;
            return Task.FromException<ICropTeacherSeedRuntime>(new InvalidOperationException());
        }));
        Assert.False(invoked);
        await Assert.ThrowsAnyAsync<IOException>(() => resumed.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post));
        Assert.Same(pending, resumed.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }
}
