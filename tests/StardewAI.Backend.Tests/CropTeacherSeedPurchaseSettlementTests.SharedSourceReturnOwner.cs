using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedSourceReturnAdvancesTheSameOwnerAndRestoresOnlyBookkeeping(bool product)
    {
        var f = new SiteTillNativeFixture(productTransport: product);
        using var host = f.Files.F.Runtime;
        await using var owner = await f.Files.F.Files.Open();
        var operation = await AdmitObservedSharedReturn(f, owner);
        Assert.True(operation.IsSharedSourceReturn);
        Assert.True(operation.IsSupportingTransition);
        Assert.False(operation.IsSupportingRoute);
        Assert.False(operation.IsSiteTilling);
        Assert.Throws<InvalidOperationException>(operation.ReadStep);
        var guard = Assert.Throws<TargetInvocationException>(() => InvokeLive<object>("RequireManagedCropDispatch", owner, operation));
        Assert.IsType<InvalidOperationException>(guard.InnerException);
        await RunObservedSharedReturn(f, owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
        Assert.Equal(1, f.Files.F.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReadProductionTraceAsync());
        var trace = await owner.ReadJointProductionTraceAsync();
        Assert.Empty(trace.CropTrace.Transitions);
        Assert.Equal(operation.Sequence, Assert.Single(trace.SharedSourceReturns).OwnerSequence);
        Assert.Equal(f.AfterPath, trace.CropTrace.AcceptedSnapshotPath);
        Assert.False(trace.NativeCropLifecycleVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
        Assert.All(f.NativePaths, path => Assert.Equal(SiteTillNativeFixture.FileHash(path), trace.CropTrace.InputHashes[path]));
        await owner.DisposeAsync();
        await using var reopened = await f.Files.F.Files.Open();
        Assert.True(reopened.HeadRecoveryRequired);
        await reopened.RestoreHeadAsync(f.Files.F.Runtime.Client, f.Files.F.Files.Recovery, f.Files.F.Files.Post);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Null(reopened.Pending);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(1, f.Files.F.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(operation.RuntimeDirectory, "shared-source-native-claim.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedSourceReturnRecoversTheOriginalMarkerReplyWithoutRepeatingNativeOrMutation(bool replyRetained)
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        var failed = false;
        await using var owner = await f.Files.F.Files.Open(writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == "marker-reply.json")
            {
                failed = true;
                if (replyRetained) await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
                throw new IOException("Synthetic shared-return reply acknowledgement lost.");
            }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var operation = await AdmitObservedSharedReturn(f, owner);
        var nativeHashes = f.NativePaths.ToDictionary(path => path, SiteTillNativeFixture.FileHash);
        await Assert.ThrowsAsync<IOException>(() => RunObservedSharedReturn(f, owner, operation));
        Assert.True(failed);
        Assert.Equal(1, f.Files.F.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await using var reopened = await f.Files.F.Files.Open();
        Assert.True(reopened.Pending!.IsSharedSourceReturn);
        await reopened.RecoverPendingAsync(f.Files.F.Runtime.Client, f.Files.F.Files.Recovery, f.Files.F.Files.Post);
        Assert.Null(reopened.Pending);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, f.Files.F.Runtime.Handler.Commits);
        Assert.Equal(replyRetained ? 0 : 1, f.Files.F.Runtime.Handler.Lookups);
        Assert.All(nativeHashes, pair => Assert.Equal(pair.Value, SiteTillNativeFixture.FileHash(pair.Key)));
    }

    [Fact]
    public async Task SharedSourceReturnChangedNativeInputCannotStartItsMarkerOrNativeDispatch()
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        await using var owner = await f.Files.F.Files.Open();
        var operation = await AdmitObservedSharedReturn(f, owner);
        var calls = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeLive<Task<JsonObject>>(
            "ExecuteCropTeacherManagedOperationAsync", f.Files.F.Runtime.Client, f.Files.F.Runtime.Client,
            f.Files.F.Runtime.LiveOptions, 1, owner, operation, f.Files.Queue,
            (Func<Task<JsonObject>>)(() => { calls++; return Task.FromResult(new JsonObject()); })));
        Assert.Contains("bookkeeping", error.Message);
        Assert.Equal(0, calls);
        File.AppendAllText(f.ResultPath, "\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => RunObservedSharedReturn(f, owner, operation));
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
        Assert.Same(operation, owner.Pending);
        Assert.False(Directory.Exists(operation.RuntimeDirectory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedSourceReturnCannotAdmitAnOutcomeFromAnotherOwnerSourceOrRun(bool otherRun)
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        await using var owner = await f.Files.F.Files.Open();
        await f.Build();
        var transition = otherRun ? f.Transition with { RunId = "foreign-owner-run" } :
            f.Transition with { BeforeSnapshotPath = f.AfterPath };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSharedSourceReturnAsync(transition, f.Files.F.Files.Native));
        Assert.Null(owner.Pending);
        Assert.Equal(f.Files.F.Source.SourcePath, owner.CurrentSnapshotPath);
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    private static async Task<CropTeacherManagedSeedOperation> AdmitObservedSharedReturn(SiteTillNativeFixture f,
        CropTeacherManagedSeedOwner owner)
    {
        await f.Build();
        f.Files.F.Runtime.Store.Snapshots[f.After.StateHash] = f.After;
        return await owner.AdmitSharedSourceReturnAsync(f.Transition, f.Files.F.Files.Native);
    }

    private static Task RunObservedSharedReturn(SiteTillNativeFixture f, CropTeacherManagedSeedOwner owner,
        CropTeacherManagedSeedOperation operation) => owner.RunSharedSourceReturnAsync(operation, f.Files.F.Runtime.Client,
        (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Files.F.Runtime.Client,
            f.Files.F.Runtime.LiveOptions, diagnostic, request), f.Files.F.Files.Post);
}
