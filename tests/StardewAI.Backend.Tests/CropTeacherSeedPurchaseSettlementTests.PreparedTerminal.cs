using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PreparedTerminalProductionReusesPinnedPublishedInputsWithoutAdoptingOrDispatching(int prefix)
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(prefix);
        var source = f.Owner.CurrentSnapshotPath; var ledger = f.Owner.CurrentLedgerPath;
        var commits = f.F.Shared.Runtime.Handler.Commits;
        var production = await f.Owner.ReadPreparedTerminalProductionAsync(new(f.Evidence.ManifestPath, f.Evidence.ManifestSha256));
        Assert.Equal(f.Evidence.ManifestPath, production.ManifestPath);
        Assert.Equal(f.Evidence.ManifestSha256, production.ManifestSha256);
        Assert.Equal(f.Evidence.OutputRetentionProofPaths, production.OutputRetentionProofPaths);
        Assert.Null(f.Owner.Pending);
        Assert.Equal(0, f.Owner.AcceptedTerminalDepositCount);
        Assert.Equal(source, f.Owner.CurrentSnapshotPath); Assert.Equal(ledger, f.Owner.CurrentLedgerPath);
        Assert.Equal(commits, f.F.Shared.Runtime.Handler.Commits);
        if (prefix > 0)
        {
            await f.Select();
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Owner.ReadPreparedTerminalProductionAsync(
                new(f.Evidence.ManifestPath, f.Evidence.ManifestSha256)));
        }
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("outside_owner")]
    public async Task PreparedTerminalProductionRejectsUnpinnedOrForeignCertificates(string fault)
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(0);
        var reference = new CropTeacherTerminalProductionReference(f.Evidence.ManifestPath, f.Evidence.ManifestSha256);
        if (fault == "hash") reference = reference with { ManifestSha256 = new string('0', 64) };
        else reference = reference with { ManifestPath = Path.Combine(Path.GetTempPath(), "foreign-production.json") };
        if (fault == "hash") await Assert.ThrowsAsync<InvalidDataException>(() => f.Owner.ReadPreparedTerminalProductionAsync(reference));
        else await Assert.ThrowsAsync<IOException>(() => f.Owner.ReadPreparedTerminalProductionAsync(reference));
        Assert.Null(f.Owner.Pending);
        Assert.Equal(0, f.Owner.AcceptedTerminalDepositCount);
        Assert.Equal(0, f.F.Shared.Runtime.Handler.Commits);
    }
}
