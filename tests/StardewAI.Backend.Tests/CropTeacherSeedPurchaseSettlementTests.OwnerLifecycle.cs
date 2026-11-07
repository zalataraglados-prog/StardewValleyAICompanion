using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("live")]
    [InlineData("lost_release_reply")]
    [InlineData("lost_acceptance")]
    public async Task OwnerLifecycleUsesTheSameSequenceAndOriginalRuntimeAcrossRestart(string mode)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var loseWrite = true;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (mode == "lost_release_reply" && loseWrite && Path.GetFileName(path) == "release-reply.json")
            { loseWrite = false; throw new IOException("Original release committed before reply file."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "lost_acceptance" && loseWrite && Path.GetFileName(path) == "operation-000001-accepted.json")
            { loseWrite = false; throw new IOException("Original acceptance durable; acknowledgement lost."); }
        });
        var operation = await owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "owner_test_cancel",
            f.SourcePath, f.LedgerPath, files.Native);
        Assert.True(operation.IsLifecycle);
        Assert.Throws<InvalidOperationException>(() => operation.ReadStep());
        if (mode == "live") await Run();
        else await Assert.ThrowsAsync<IOException>(Run);
        Assert.Equal(mode == "live", owner.Pending is null);
        Assert.Equal(1, runtime.Handler.Cancellations);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(mode == "lost_release_reply" ? 0 : 1, runtime.Handler.LedgerReads);
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        if (mode == "lost_release_reply")
        {
            Assert.True(restarted.Pending!.IsLifecycle);
            await restarted.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
        }
        else
        {
            Assert.True(restarted.HeadRecoveryRequired);
            await restarted.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        }
        Assert.Null(restarted.Pending);
        Assert.False(restarted.HeadRecoveryRequired);
        var final = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(restarted.CurrentLedgerPath), JsonOptions)!;
        Assert.Equal("cancelled", final.CropPlantingCommitments.Single().Status);
        AssertOtherRootsUnchanged(f.Ledger, final);
        Assert.Equal(1, runtime.Handler.Cancellations);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(mode == "lost_release_reply" ? 1 : 0, runtime.Handler.Lookups);
        var acceptance = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal("lifecycle", acceptance["operation_kind"]!.GetValue<string>());
        Assert.False(acceptance["native_outcome_verified"]!.GetValue<bool>());
        Assert.Null(acceptance["original_proof_sha256"]);

        Task Run() => owner.RunLifecycleAsync(operation, runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post);
    }

    [Fact]
    public async Task OwnerLifecycleContinuesAfterTheOriginalPurchaseOnTheSameAcceptedChain()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        runtime.LiveOptions.RunId = transition.RunId;
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var seed = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await runtime.Prepare(transition, selectedScope: seed.Scopes.Purchase, runtimeDirectory: seed.RuntimeDirectory);
        await owner.PinPreparedAsync(seed, prepared);
        runtime.ObserveAfter();
        await prepared.CompleteAsync(transition);
        await owner.AcceptLiveAsync(seed, prepared);
        var operation = await owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "owner_after_purchase",
            owner.CurrentSnapshotPath, owner.CurrentLedgerPath, files.Native + "-lifecycle");
        Assert.Equal(2, operation.Sequence);
        var reads = runtime.Handler.LedgerReads;
        await owner.RunLifecycleAsync(operation, runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post);
        Assert.Equal(reads + 1, runtime.Handler.LedgerReads);
        Assert.Equal(3, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(1, runtime.Handler.Cancellations);
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        Assert.True(restarted.HeadRecoveryRequired);
        await restarted.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        Assert.Equal(3, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(1, runtime.Handler.Cancellations);
        Assert.Equal(2, Directory.GetFiles(files.OwnerRoot, "operation-*-accepted.json").Length);
        var first = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json")))!;
        var second = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000002-accepted.json")))!;
        Assert.NotNull(first["original_proof_sha256"]);
        Assert.NotNull(second["original_closure_sha256"]);
    }
}
