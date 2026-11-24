using System.Reflection;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task BoundedNativeCheckpointBindsTheAcceptedOvernightWithoutChangingItsLedger()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        var retain = typeof(CropTeacherManagedSeedOwner).GetMethod("RetainAcceptedOriginalOvernightSave",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => retain.Invoke(owner, [night])).InnerException);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        var snapshot = owner.CurrentSnapshotPath;
        var ledger = owner.CurrentLedgerPath;
        var commits = f.Shared.Runtime.Handler.Commits;
        var options = f.Shared.Runtime.LiveOptions;
        var expected = NativeSaveBoundaryVerifier.Capture(options.SaveIsolationPath, options.SaveSlot);

        var archive = Assert.IsType<string>(retain.Invoke(owner, [night]));
        Assert.Equal(Path.Combine(night.RuntimeDirectory, "accepted-native-save.zip"), archive);
        NativeSaveCheckpointArchive.Verify(expected, archive);
        Assert.Equal(snapshot, owner.CurrentSnapshotPath);
        Assert.Equal(ledger, owner.CurrentLedgerPath);
        Assert.Equal(commits, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.NativeNightCalls);
    }
}
