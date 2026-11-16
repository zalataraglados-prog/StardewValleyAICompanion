using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSharedWorkCanContinueOnlyItsKnownClaimedOutcomeWithoutAnotherExecutionCallback(bool emitOutcome)
    {
        var f = new SharedYieldFixture(); using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await owner.AdmitNativeSharedSourceYieldAsync(f.Selection, f.Native.ReceiptPath,
            f.Native.AfterPath, f.Native.Files.F.Files.Native);
        await owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client);
        if (emitOutcome)
        {
            await f.Native.Build();
            f.Native.Files.F.Runtime.Store.Snapshots[f.Native.After.StateHash] = f.Native.After;
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        var pending = reopened.Pending!;
        Task Continue() => InvokeLive<Task>("CompleteCropTeacherSharedSourceReturnAsync", f.Native.Files.F.Runtime.Client,
            f.Native.Files.F.Runtime.LiveOptions, reopened, pending);
        if (emitOutcome)
        {
            await Continue();
            Assert.Null(reopened.Pending);
            Assert.Equal(f.Native.AfterPath, reopened.CurrentSnapshotPath);
            Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
            var reportPath = await InvokeLive<Task<string>>("WriteCropTeacherNativeSourceReturnReportAsync",
                f.Native.Files.F.Runtime.LiveOptions, reopened, pending);
            var report = JsonNode.Parse(File.ReadAllText(reportPath))!;
            Assert.True(Directory.Exists(Path.GetDirectoryName(reportPath)));
            Assert.False(report["native_execution_performed"]!.GetValue<bool>());
            Assert.False(report["owner_pending"]!.GetValue<bool>());
        }
        else
        {
            await Assert.ThrowsAnyAsync<IOException>(Continue);
            Assert.Same(pending, reopened.Pending);
            Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Native.Files.F.Runtime.Client));
    }
}
