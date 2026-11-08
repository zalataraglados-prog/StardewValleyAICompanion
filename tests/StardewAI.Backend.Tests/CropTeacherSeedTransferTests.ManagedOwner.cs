using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task ManagedOwnerRestoresOriginalPreparedTransferWithoutAnotherMarker()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f.SourcePath, f.LedgerPath, f.RequestPath);
        using var files = new OwnerFiles(runtime.Options, runtime.Client, f.SourcePath, f.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await owner.PrepareSeedAsync(operation, () => runtime.Prepare(operation.Scopes.Transfer, runtimeDirectory: operation.RuntimeDirectory));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        var prepared = await resumed.RestorePreparedSeedAsync(runtime.Client, files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.Options, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", runtime.Client, runtime.Options, diagnostic, request));
        Assert.IsType<CropTeacherSeedTransferRuntime>(prepared);
        Assert.Equal(1, runtime.Handler.Commits);
        var pending = resumed.Pending!;
        await resumed.ExecuteSeedAsync(pending, async original =>
        {
            original.ValidateDispatchQueue(runtime.Transition.QueuePath);
            runtime.ObserveAfter();
            return await original.CompleteAsync(runtime.Transition);
        });
        await resumed.AcceptLiveAsync(pending, prepared);
        Assert.Null(resumed.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedOwnerUsesTheSameOriginalTransferAndLiveCertificate(bool managedPreparation)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f.SourcePath, f.LedgerPath, f.RequestPath);
        using var files = new OwnerFiles(runtime.Options, runtime.Client, f.SourcePath, f.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.TransferSeedSupply, step.Stage);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = managedPreparation
            ? await owner.PrepareSeedAsync(operation, () => runtime.Prepare(operation.Scopes.Transfer, runtimeDirectory: operation.RuntimeDirectory))
            : await runtime.Prepare(operation.Scopes.Transfer, runtimeDirectory: operation.RuntimeDirectory);
        if (!managedPreparation) await owner.PinPreparedAsync(operation, prepared);
        async Task<bool> Execute(ICropTeacherSeedRuntime original)
        {
            original.ValidateDispatchQueue(runtime.Transition.QueuePath);
            runtime.ObserveAfter();
            await prepared.CompleteAsync(runtime.Transition);
            return true;
        }
        if (managedPreparation) await owner.ExecuteSeedAsync(operation, Execute);
        else await Execute(prepared);
        var reads = runtime.Handler.LedgerReads;
        await owner.AcceptLiveAsync(operation, prepared);
        Assert.Null(owner.Pending);
        Assert.Equal(reads, runtime.Handler.LedgerReads);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        Assert.Null(ledger.PreparedMaterialTransfer);
    }
}
