using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalRoutePendingPreventsCompilationOverwriteAndWrongSeedCallbacks()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        var queue = File.ReadAllBytes(f.QueuePath);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Prepare(owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CompileAndAdmitOriginalRouteAsync(() =>
        { calls++; throw new InvalidOperationException("Compiler callback must not run."); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareSeedAsync<StardewAI.LiveTrainingLoop.CropTeacherSeedPurchaseRuntime>(operation, () =>
        { calls++; throw new InvalidOperationException("Seed callback must not run."); }));
        Assert.Equal(0, calls);
        Assert.Equal(queue, File.ReadAllBytes(f.QueuePath));
        Assert.Same(operation, owner.Pending);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("queue")]
    [InlineData("plan")]
    public async Task OriginalRouteChangedPreparedInputPreventsNativeInvocation(string changed)
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        var path = changed switch { "source" => f.Source.SourcePath, "queue" => f.QueuePath,
            _ => Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "crop-teacher-route-0001-model-plan.json") };
        File.AppendAllText(path, " ");
        var calls = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, () =>
        { calls++; return f.EmitNative(); }));
        Assert.Equal(0, calls);
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task OriginalRouteUnclaimedNativeFilesCannotAuthorizeRebind()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        await f.EmitNative();
        await Assert.ThrowsAnyAsync<IOException>(() => f.Rebind(owner, operation));
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.Same(operation, owner.Pending);
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("money")]
    [InlineData("returned_paths")]
    public async Task OriginalRouteInvalidNativeResultNeverRebindsLedger(string change)
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, async () =>
        {
            var result = await f.EmitNative();
            var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(f.ReceiptPath), JsonOptions)!;
            if (change == "receipt") { receipt.Success = false; Fixture.Save(f.ReceiptPath, receipt); }
            if (change == "money")
            {
                var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
                ChangeState(after, "player", node => node["money"]!["value"] = 0);
                after.StateHash = SnapshotHash.ComputeStateHash(after.State);
                Fixture.Save(f.AfterPath, after);
                receipt.AfterStateHash = after.StateHash;
                receipt.StepResults[^1].AfterStateHash = after.StateHash;
                Fixture.Save(f.ReceiptPath, receipt);
            }
            if (change == "returned_paths") result["after_snapshot_path"] = f.Source.SourcePath;
            return result;
        }));
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.Same(operation, owner.Pending);
    }
}
