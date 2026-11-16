using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRefillOwnerUsesPinnedExecutorThroughOriginalRebindAndReopen(bool product)
    {
        using var f = new NativeRefillProductFixture(product);
        var approach = f.Trace.Approach;
        var shared = approach.Watering.Shared;
        shared.Runtime.LiveOptions.UseProductExecutor = product;
        var options = shared.Files.Options with
        {
            ExecutorVersion = product ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            UseSelectedRefillResourceEffects = true
        };
        await approach.Compile();
        await using var owner = await shared.Files.Open(options);
        var operation = await owner.CompileAndAdmitOriginalWateringAsync(() => Task.FromResult(
            new CropTeacherOriginalWateringCompilation(approach.Watering.Step, approach.ModelPath, approach.QueuePath,
                f.ReceiptPath, f.AfterPath, shared.Files.Native)
                { IsRefillApproach = true, UseSelectedRefillResourceEffects = true }));
        Assert.Equal(product, NativeRefillStart(operation).UsesNativeRefillExecution);
        var nativeCalls = 0;
        await owner.ExecuteOriginalWateringAsync(operation, shared.Runtime.Client, async () =>
        {
            nativeCalls++;
            await f.Build(retainCompilation: true);
            var after = ReadSiteSnapshot(f.AfterPath);
            shared.Runtime.Store.Snapshots[after.StateHash] = after;
            return new JsonObject { ["execution_path"] = f.ReceiptPath, ["after_snapshot_path"] = f.AfterPath };
        });
        await owner.RunWateringMaintenanceAsync(operation, shared.Runtime.Client, (diagnostic, request) =>
            InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", shared.Runtime.Client,
                shared.Runtime.LiveOptions, diagnostic, request), shared.Files.Post);
        Assert.Null(owner.Pending);
        Assert.Equal(1, nativeCalls);
        Assert.Equal(1, shared.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        shared.Runtime.LiveOptions.UseProductExecutor = !product;
        await using var reopened = await shared.Files.Open(options);
        var restored = (CropTeacherManagedSeedOperation)typeof(CropTeacherManagedSeedOwner)
            .GetField("lastAcceptedOperation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(reopened)!;
        Assert.Equal(product, NativeRefillStart(restored).UsesNativeRefillExecution);
        await reopened.RestoreHeadAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, nativeCalls);
        Assert.Equal(1, shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(options.EvidenceDirectory, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_refill_approach_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_watering_prefix_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
    }

    private static CropTeacherWateringMaintenance.Preparation NativeRefillStart(CropTeacherManagedSeedOperation operation) =>
        (CropTeacherWateringMaintenance.Preparation)typeof(CropTeacherManagedSeedOperation)
            .GetProperty("WateringStart", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operation)!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRefillAcceptedControlContinuesOnlyItsCurrentOwnerWithoutWateringCredit(bool product)
    {
        using var f = new OwnedWateringFixture(water: 0, atRefillApproach: true, productExecutor: product);
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        Assert.False(HasAcceptedNativeRefill(owner, operation));
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client,
            () => f.EmitNative(applied: 0, failedSuffix: true, refillOnlySuffix: true));
        Assert.False(HasAcceptedNativeRefill(owner, operation));
        await f.Rebind(owner, operation);
        Assert.True(HasAcceptedNativeRefill(owner, operation));
        var next = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 2, f.Basis.Step, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, f.Shared.Files.Native + "-after-refill");
        Assert.False(HasAcceptedNativeRefill(owner, operation));
        Assert.False(HasAcceptedNativeRefill(owner, next));
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
    }

    private static bool HasAcceptedNativeRefill(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
        (bool)typeof(CropTeacherManagedSeedOwner).GetMethod("HasAcceptedOriginalRefillControl",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, new object[] { operation })!;
}
