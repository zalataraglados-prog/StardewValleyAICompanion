using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputObservationClientTests
{
    [Fact]
    public void DebrisStateModeRequiresTheWholeOriginalIsolatedObservationChain()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeDebrisState);
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-debris-state"]));
        var options = new LiveTrainingOptions { ObserveNativeDebrisState = true };
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
        options.ObserveNativeInventoryGraph = true;
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
        options.ObserveNativeInventoryTransients = options.ObserveNativeInventoryPayload = true;
        options.ObserveNativeClearanceToolInputs = options.ObserveNativeOutputSnapshotAnchor = options.ObserveNativeOutputs = true;
        options.ExecutionSnapshotProfile = "full";
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
        options.SkipTraining = options.RequireExecutorFeedback = options.UseRuntimeTestHarnessExecutor = true;
        NativeOutputObservationMode.Validate(options);
        options.ExecutionSnapshotProfile = "default";
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
    }
}
