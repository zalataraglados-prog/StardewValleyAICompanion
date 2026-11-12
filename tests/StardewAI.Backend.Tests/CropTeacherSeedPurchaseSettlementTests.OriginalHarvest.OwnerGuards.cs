using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalHarvestOwnerDoesNotAdoptAFutureGrownSnapshotAsItsCurrentHead()
    {
        using var f = new HarvestFixture();
        await using var owner = await f.Shared.Files.Open();
        var initial = owner.CurrentSnapshotPath;
        var calls = 0;
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.ExecuteCropBatch, f.Day, Id,
            "Farm", "FarmHouse", string.Empty, string.Empty, 0, 0, f.Batch, "declared_future_harvest");
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalHarvestAsync(() =>
        {
            calls++;
            return Task.FromResult(new CropTeacherOriginalHarvestCompilation(step, f.ModelPath, f.QueuePath,
                f.Shared.ReceiptPath, f.Shared.AfterPath, f.Shared.Files.Native));
        }));
        Assert.Equal(1, calls); Assert.Null(owner.Pending); Assert.Equal(initial, owner.CurrentSnapshotPath);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-intent.json")));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task OriginalHarvestOwnerCannotDispatchOrCommitAnExistingWateringPhase()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        var nativeCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteOriginalHarvestAsync(operation, f.Shared.Runtime.Client, () =>
        { nativeCalls++; return Task.FromResult(new JsonObject()); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.RunHarvestAsync(operation, f.Shared.Runtime.Client,
            (diagnostic, request) => throw new InvalidOperationException("No marker may be invoked."), f.Shared.Files.Post));
        Assert.False(operation.IsHarvest); Assert.Same(operation, owner.Pending);
        Assert.Equal(0, nativeCalls); Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-started.json")));
    }

    [Fact]
    public async Task OriginalHarvestOwnerDoesNotCompileAnotherPhaseWhileOneIsPending()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        var compiled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CompileAndAdmitOriginalHarvestAsync(() =>
        { compiled = true; throw new InvalidOperationException("Must keep the admitted phase."); }));
        Assert.False(compiled); Assert.Same(operation, owner.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }
}
