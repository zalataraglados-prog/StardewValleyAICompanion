using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalWateringCannotEnterRouteOrSeedCallbacks()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Shared.Runtime.Client, () =>
        { calls++; throw new InvalidOperationException("Wrong callback."); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.RunSupportingRouteAsync(operation, f.Shared.Runtime.Client,
            (_, _) => { calls++; throw new InvalidOperationException("Wrong marker."); }, f.Shared.Files.Post));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareSeedAsync<CropTeacherSeedPurchaseRuntime>(operation, () =>
        { calls++; throw new InvalidOperationException("Wrong seed."); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CompileAndAdmitOriginalWateringAsync(() =>
        { calls++; throw new InvalidOperationException("Pending compilation."); }));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "maintenance-native-claim.json")));
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "route-native-claim.json")));
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-started.json")));
    }

    [Theory]
    [InlineData("target")]
    [InlineData("budget")]
    [InlineData("opaque_id")]
    public async Task OriginalWateringReplaysCompilerAndPinsOpaqueIdsAfterAdmission(string changed)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        await f.Prepare(owner);
        await owner.DisposeAsync();
        var queue = JsonNode.Parse(File.ReadAllText(f.QueuePath))!;
        var command = queue["items"]![0]!["normalized_command"]!;
        if (changed == "target") command["parameters"]!.AsArray().Single(row => row!["name"]!.GetValue<string>() == "target_tile_x")!["value"] = "9";
        if (changed == "budget") command["steps"]![0]!["estimated_ticks"] = command["steps"]![0]!["estimated_ticks"]!.GetValue<int>() + 1;
        if (changed == "opaque_id") command["steps"]![0]!["step_id"] = "different-pinned-id";
        Fixture.Save(f.QueuePath, queue);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Shared.Files.Open());
        Assert.Contains(changed == "opaque_id" ? "admission inputs changed" : "does not replay its original compiler", error.Message);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "maintenance-native-claim.json")));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("batch_index")]
    [InlineData("duplicate")]
    [InlineData("outside_layout")]
    public async Task OriginalWateringRequiresOriginalDueBatchBeforeAdmission(string changed)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        await f.Basis.Compile();
        var batch = f.Basis.Step.ActionBatch!;
        batch = changed == "batch_index" ? batch with { BatchIndex = batch.BatchIndex + 100 } :
            batch with { Tiles = changed == "duplicate" ? new[] { batch.Tiles[0], batch.Tiles[0] } : new[] { new MoneyRevenueCropPlantingTile(9, 9) } };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalWateringAsync(() => Task.FromResult(
            new CropTeacherOriginalWateringCompilation(f.Basis.Step with { ActionBatch = batch }, f.ModelPath, f.Basis.QueuePath,
                f.ReceiptPath, f.AfterPath, f.Shared.Files.Native))));
        Assert.Null(owner.Pending);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "maintenance-native-claim.json")));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalWateringRequiresReceiptEmissionBeforeCompileAndInvoke(bool afterAdmission)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = afterAdmission ? await f.Prepare(owner) : null;
        f.Shared.Runtime.LiveOptions.EmitQueueExecutionReceipt = false;
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            if (operation is null) await f.Prepare(owner);
            else await InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync", f.Shared.Runtime.Client, f.Shared.Runtime.Client,
                f.Shared.Runtime.LiveOptions, 1, owner, operation);
        });
        Assert.Same(operation, owner.Pending);
        Assert.Equal(afterAdmission, File.Exists(f.QueuePath));
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "maintenance-native-claim.json")));
        Assert.False(File.Exists(f.ReceiptPath));
    }

    [Fact]
    public async Task OriginalWateringRejectsDifferentOutputIterationBeforeNativeClaim()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 2, owner, operation));
        Assert.Contains("output slot changed", error.Message);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "maintenance-native-claim.json")));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }
}
