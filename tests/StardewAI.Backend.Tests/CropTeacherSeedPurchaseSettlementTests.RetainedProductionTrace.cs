namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task RetainedTraceDoesNotRestoreOrAuthorizeTheExecutableHead()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var original = await owner.ReadProductionTraceAsync();
        Assert.False(original.RetainedReplayOnly);
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        Assert.True(reopened.HeadRecoveryRequired);
        var commits = f.Shared.Runtime.Handler.Commits;
        var reads = f.Shared.Runtime.Handler.LedgerReads;
        var retained = await reopened.ReadRetainedProductionTraceAsync();
        Assert.True(retained.RetainedReplayOnly);
        Assert.Equal(original.Transitions, retained.Transitions);
        Assert.Equal(original.AcceptedSnapshotPath, retained.AcceptedSnapshotPath);
        retained.ValidateInputsUnchanged();
        Assert.True(reopened.HeadRecoveryRequired);
        Assert.Equal(commits, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(reads, f.Shared.Runtime.Handler.LedgerReads);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ReadProductionTraceAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.BuildProductionEvidenceAsync(
            Path.Combine(f.Shared.Files.OwnerRoot, "retained-cannot-authorize")));
    }

    [Fact]
    public async Task RetainedTraceStillRejectsUnknownUnacceptedNativeClaims()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client,
            () => throw new IOException("unknown native outcome")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReadRetainedProductionTraceAsync());
        Assert.NotNull(owner.Pending);
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }
}
