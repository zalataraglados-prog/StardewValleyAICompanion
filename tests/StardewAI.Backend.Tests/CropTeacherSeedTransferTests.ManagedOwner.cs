using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task ManagedOwnerUsesTheSameOriginalTransferAndLiveCertificate()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f.SourcePath, f.LedgerPath, f.RequestPath);
        using var files = new OwnerFiles(runtime.Options, runtime.Client, f.SourcePath, f.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.TransferSeedSupply, step.Stage);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await runtime.Prepare(operation.Scopes.Transfer, runtimeDirectory: operation.RuntimeDirectory);
        await owner.PinPreparedAsync(operation, prepared);
        runtime.ObserveAfter();
        await prepared.CompleteAsync(runtime.Transition);
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
