using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("reply")]
    [InlineData("compile")]
    [InlineData("prepared_record")]
    public async Task SharedCookingRecoversOriginalPreparationWithoutResendingOrRedispatch(string failure)
    {
        using var f = new SharedCookingFixture(); f.PrepareSource(false);
        var decision = CookingDecision(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        var failed = false;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (!failed && failure == "prepared_record" && Path.GetFileName(path).EndsWith("-prepared.json", StringComparison.Ordinal))
            { failed = true; throw new IOException("Declared cooking prepared-record interruption."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var operation = await owner.AdmitSharedCookingPreparationAsync(decision, f.PreparationRequest(),
            files.Native + "-receipt.json", files.Native + "-after.json", files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSharedCookingAsync(operation, f.Runtime.Client,
            async (diagnostic, request) =>
            {
                var reply = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request);
                if (failure == "reply") throw new IOException("Declared cooking accepted-reply loss.");
                return reply;
            }, (ledger, output) => failure == "compile" ? throw new IOException("Declared cooking compiler interruption.") :
                Task.FromResult(CompileOwnedCookingSelection(f, ledger, output, decision))));
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Same(operation, owner.Pending);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        var pending = reopened.Pending!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.PrepareSharedCookingAsync(pending, f.Runtime.Client,
            (_, _) => throw new Exception("Must not resend preparation"), (_, _) => throw new Exception("Use explicit recovery")));
        var selection = await reopened.RecoverSharedCookingPreparationAsync(pending, f.Runtime.Client, files.Post,
            (ledger, output) => Task.FromResult(CompileOwnedCookingSelection(f, ledger, output, decision)));
        Assert.NotNull(selection.InventoryConsumptionPreparation); Assert.Null(selection.MaterialTransferPreparation);
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(failure == "reply" ? 1 : 0, f.Runtime.Handler.Lookups);
        Assert.Equal(selection.QueuePath, (await reopened.ClaimSharedSourceExecutionAsync(pending, f.Runtime.Client)).Transition.QueuePath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Runtime.Client));
        await reopened.DisposeAsync();
        await using var claimed = await files.Open();
        Assert.NotNull(claimed.Pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => claimed.ClaimSharedSourceExecutionAsync(claimed.Pending!, f.Runtime.Client));
        Assert.Equal(1, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCookingRecoversOriginalSettlementWithoutAnotherNativeOrMutation(bool loseReply)
    {
        using var f = new SharedCookingFixture(); f.PrepareSource(false);
        var decision = CookingDecision(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        var failed = false;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == (loseReply ? "shared-consumption-settlement-reply.json" : "shared-consumption-settlement.json"))
            { failed = true; throw new IOException("Declared cooking settlement publication interruption."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var receipt = files.Native + "-receipt.json"; var after = files.Native + "-after.json";
        var operation = await owner.AdmitSharedCookingPreparationAsync(decision, f.PreparationRequest(), receipt, after, files.Native);
        var selection = await owner.PrepareSharedCookingAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request),
            (ledger, output) => Task.FromResult(CompileOwnedCookingSelection(f, ledger, output, decision)));
        await owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client);
        await f.WriteOwnedNative(selection, receipt, after, false);
        f.Runtime.Store.StoreSnapshot(f.After);
        await Assert.ThrowsAsync<IOException>(() => owner.SettleSharedCookingExecutionAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request)));
        Assert.True(failed); Assert.Equal(1, f.Runtime.Handler.Settlements);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SettleSharedCookingExecutionAsync(operation, f.Runtime.Client,
            (_, _) => throw new Exception("Must not retry settlement")));
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        var pending = reopened.Pending!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Runtime.Client));
        await reopened.RecoverSharedCookingSettlementAsync(pending, f.Runtime.Client, files.Recovery, files.Post);
        await reopened.VerifySharedSourceExecutionAsync(pending);
        await reopened.RunSharedSourceReturnAsync(pending, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request), files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(after, reopened.CurrentSnapshotPath);
        Assert.Equal(loseReply ? 1 : 0, f.Runtime.Handler.Lookups);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }

    [Fact]
    public async Task SharedCookingRejectsFreshSelectionFromAnotherOriginalDecision()
    {
        using var f = new SharedCookingFixture(); f.PrepareSource(false);
        var decision = CookingDecision(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        await using var owner = await files.Open();
        var operation = await owner.AdmitSharedCookingPreparationAsync(decision, f.PreparationRequest(),
            files.Native + "-receipt.json", files.Native + "-after.json", files.Native);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.PrepareSharedCookingAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request),
            (ledger, output) => Task.FromResult(CompileOwnedCookingSelection(f, ledger, output, decision)
                with { SelectedQueueIndex = decision.SelectedQueueIndex + 1 })));
        Assert.Equal(1, f.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
        Assert.False(Directory.Exists(files.Native));
    }
}
