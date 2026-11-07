using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedRecoveryRebuildsOriginalTransferIncludingLostSettlementFile(bool missingReply)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        var prepared = await runtime.Prepare();
        runtime.ObserveAfter();
        if (missingReply) runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "settlement-reply.json");
        if (missingReply) await Assert.ThrowsAsync<IOException>(() => prepared.CompleteAsync(runtime.Transition));
        else await prepared.CompleteAsync(runtime.Transition);
        var original = f.Open();
        var step = runtime.StepFor(original);
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, original.PreparationRequest.PortfolioId);
        var expected = runtime.Repository.Get(f.After);
        var recovered = await recovery.Recover(runtime.Client, runtime.Options, f.RuntimePath, scopes, step);
        CropTeacherSeedRecoveryTestSupport.AssertSameLedger(recovered, expected);
        Assert.Null(recovered.ReadLedger().PreparedMaterialTransfer);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(missingReply ? 1 : 0, runtime.Handler.Lookups);
        var reads = runtime.Handler.LedgerReads;
        await Assert.ThrowsAsync<IOException>(() => recovery.Recover(runtime.Client, runtime.Options, f.RuntimePath, scopes, step));
        Assert.Equal(reads, runtime.Handler.LedgerReads);
        File.AppendAllText(runtime.Transition.AfterSnapshotPath, "\n");
        Assert.Throws<InvalidDataException>(() => recovered.ReadLedger());
    }

    [Theory]
    [InlineData("missing_recovery_lookup")]
    [InlineData("final_readback")]
    public async Task ManagedRecoveryRejectsMissingOriginalLookupOrForeignFinalLedger(string fault)
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f, fault: fault);
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        var prepared = await runtime.Prepare();
        runtime.ObserveAfter();
        runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "settlement-reply.json");
        await Assert.ThrowsAsync<IOException>(() => prepared.CompleteAsync(runtime.Transition));
        var original = f.Open();
        var step = runtime.StepFor(original);
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, original.PreparationRequest.PortfolioId);
        var reads = runtime.Handler.LedgerReads;
        await Assert.ThrowsAsync<InvalidDataException>(() => recovery.Recover(runtime.Client, runtime.Options, f.RuntimePath, scopes, step));
        Assert.Equal(reads + (fault == "final_readback" ? 1 : 0), runtime.Handler.LedgerReads);
        Assert.Equal(1, runtime.Handler.Lookups);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }
}
