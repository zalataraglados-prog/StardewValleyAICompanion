using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeLifecycleReusesOriginalPlantingPrefixWithoutCompletingItsUnexecutedSuffix(bool failedSuffix)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, consumed: 2, failedSuffix: failedSuffix) with { TransitionKind = "crop_actions" };
        var paths = new[] { transition.QueuePath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath };
        var hashes = paths.ToDictionary(path => path, PrefixHash);
        var type = typeof(MoneyRevenueCropNativeLifecycleProofBuilder);
        object Load(string name) => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { transition })!;
        var native = Load("LoadNativeObservedTransition");
        Assert.Equal(2, native.GetType().GetProperty("NativeAppliedItemCount")!.GetValue(native));
        var observed = Array.CreateInstance(native.GetType(), 1); observed.SetValue(native, 0);
        Assert.Equal(2, type.GetMethod("CountOption", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { observed, "executor.plant_seed" }));
        type.GetMethod("ValidateQueueReceipt", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { native });
        var legacy = Load("LoadTransition");
        var failure = Assert.Throws<TargetInvocationException>(() => type.GetMethod("ValidateQueueReceipt",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { legacy }));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
        Assert.False(receipt.Success); Assert.False(receipt.SelectedCandidateCompleted);
        Assert.Equal(failedSuffix ? 3 : 2, receipt.ExecutedItemCount);
        foreach (var (path, hash) in hashes) Assert.Equal(hash, PrefixHash(path));
    }

    [Fact]
    public void NativeLifecycleCannotInventPlantingPrefixFromDifferentPlannedCount()
    {
        using var f = new Fixture();
        var transition = f.NativeTransition(f.Mark(), consumed: 2, receiptChange: receipt => receipt.PlannedItemCount++)
            with { TransitionKind = "crop_actions" };
        var failure = Assert.Throws<TargetInvocationException>(() => typeof(MoneyRevenueCropNativeLifecycleProofBuilder)
            .GetMethod("LoadNativeObservedTransition", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { transition }));
        Assert.IsType<InvalidDataException>(failure.InnerException);
    }

    private static string PrefixHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
