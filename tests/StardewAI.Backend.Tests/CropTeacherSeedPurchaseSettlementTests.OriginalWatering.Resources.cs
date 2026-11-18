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
    public async Task OriginalWateringConsumesItsRealWireShapePickupAndRetainsTheCompressedRequest(bool mutateBlob)
    {
        using var f = new OwnedWateringResourcesFixture();
        await using var owner = await f.Owned.Shared.Files.Open();
        var operation = await f.Owned.Prepare(owner);
        await owner.ExecuteOriginalWateringAsync(operation, f.Owned.Shared.Runtime.Client, f.Emit);
        var transition = (ICropTeacherSupportingTransition)operation.GetType().GetProperty("SupportingTransition",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(operation)!;
        Assert.True(transition.InputHashes.ContainsKey(f.JournalPath + ".gz"));
        await f.Owned.Rebind(owner, operation);
        Assert.Null(owner.Pending); Assert.Equal(1, f.Owned.Shared.Runtime.Handler.Commits);
        Assert.Equal(f.Owned.AfterPath, owner.CurrentSnapshotPath);
        await owner.DisposeAsync();
        await using var recovered = await f.Owned.Shared.Files.Open();
        await recovered.RestoreHeadAsync(f.Owned.Shared.Runtime.Client, f.Owned.Shared.Files.Recovery, f.Owned.Shared.Files.Post);
        var trace = await recovered.ReadProductionTraceAsync();
        var bind = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!
            .GetMethod("BindNativePreparation", BindingFlags.Static | BindingFlags.NonPublic)!;
        var native = (MoneyRevenueCropNativeLifecycleTransitionRequest)bind.Invoke(null,
            [trace, f.Owned.Shared.Inputs.Plan, Assert.Single(trace.Transitions)])!;
        Assert.NotNull(native.NativeWateringPreparation); Assert.Null(native.NativeRefillPreparation);
        var builder = typeof(MoneyRevenueCropNativeLifecycleProofBuilder);
        var loaded = builder.GetMethod("LoadNativeObservedTransition", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [native])!;
        Assert.Equal(1, loaded.GetType().GetProperty("NativeAppliedItemCount")!.GetValue(loaded));
        builder.GetMethod("ValidateQueueReceipt", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [loaded]);
        Assert.Equal(1, f.Owned.Shared.Runtime.Handler.Commits);
        trace.ValidateInputsUnchanged();
        File.AppendAllText(mutateBlob ? f.JournalPath + ".gz" : f.ResultPath, " ");
        Assert.Throws<InvalidDataException>(transition.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalWateringSelectsAndRecoversOnlyItsExplicitResourceMode(bool selected)
    {
        using var f = new OwnedWateringFixture();
        f.Shared.Runtime.LiveOptions.UseManagedWateringResources = selected;
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        Assert.Equal(selected, NativeRefillStart(operation).UsesObservedWateringResources);
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-intent.json")))!;
        Assert.Equal(selected ? "watering_resources" : null, record["maintenance_control_kind"]?.GetValue<string>());
        await owner.DisposeAsync();
        f.Shared.Runtime.LiveOptions.UseManagedWateringResources = !selected;
        await using var recovered = await f.Shared.Files.Open();
        Assert.Equal(selected, NativeRefillStart(recovered.Pending!).UsesObservedWateringResources);
    }

    [Fact]
    public async Task NewObservationModeCannotAcceptAnOldReceiptWithoutItsNativeWitness()
    {
        using var f = new OwnedWateringFixture();
        f.Shared.Runtime.LiveOptions.UseManagedWateringResources = true;
        await using var owner = await f.Shared.Files.Open();
        var operation = await f.Prepare(owner);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteOriginalWateringAsync(operation,
            f.Shared.Runtime.Client, () => f.EmitNative()));
        Assert.NotNull(owner.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json")));
    }
}
