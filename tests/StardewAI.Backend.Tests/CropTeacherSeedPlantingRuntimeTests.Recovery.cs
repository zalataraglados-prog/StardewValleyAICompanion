using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData(8, false)]
    [InlineData(3, true)]
    public async Task ManagedRecoveryRebuildsOriginalObservedPlantingPrefixWithoutAnotherNativeRequest(int consumed, bool missingReply)
    {
        using var f = new RuntimeFixture();
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        var runtime = await f.Prepare(consumed, failedSuffix: missingReply);
        await f.Observe(runtime);
        if (missingReply) f.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "settlement-reply.json");
        if (missingReply) await Assert.ThrowsAsync<IOException>(() => runtime.CompleteAsync(f.Transition));
        else await runtime.CompleteAsync(f.Transition);
        var original = f.Files.Open();
        var step = RecoveryStep(f);
        var scopes = CropTeacherManagedSeedScopes.Open(f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath,
            step, original.MarkerRequest.PortfolioId);
        var expected = f.Files.Repository.Get(f.Files.After);
        var recovered = await recovery.Recover(f.Client, f.Options, f.Output, scopes, step);
        CropTeacherSeedRecoveryTestSupport.AssertSameLedger(recovered, expected);
        Assert.Equal(9 - consumed, recovered.ReadLedger().MaterialReservations.Single(row => row.ReservationId == f.Files.OwnedSlotOne).Quantity);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
        Assert.Equal(missingReply ? 1 : 0, f.Handler.Lookups);
        Assert.Equal(0, f.NativeRequests);
    }

    [Fact]
    public async Task ManagedRecoveryDoesNotTurnMissingNativeTransitionIntoZeroExecutionOrResend()
    {
        using var f = new RuntimeFixture();
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        await f.Prepare();
        var original = f.Files.Open();
        var step = RecoveryStep(f);
        var scopes = CropTeacherManagedSeedScopes.Open(f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath,
            step, original.MarkerRequest.PortfolioId);
        var reads = f.Handler.LedgerReads;
        await Assert.ThrowsAsync<FileNotFoundException>(() => recovery.Recover(f.Client, f.Options, f.Output, scopes, step));
        Assert.Equal(reads, f.Handler.LedgerReads);
        Assert.Equal(0, f.Handler.Lookups);
        Assert.Equal(0, f.NativeRequests);
        Assert.Equal(0, f.Handler.Settlements);
    }

    private static CropTeacherRunStep RecoveryStep(RuntimeFixture f)
    {
        var scope = f.Files.Open();
        return new(CropTeacherRunStageKinds.ExecuteCropBatch, scope.TotalDay, scope.CommitmentId,
            "Farm", "FarmHouse", "(O)745", "", 0, 8, f.Files.Batch(), "owned_supply", SeedReservationId: f.Files.OwnedSlotOne);
    }
}
