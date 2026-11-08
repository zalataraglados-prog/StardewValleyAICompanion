using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputObservationClientTests
{
    [Fact]
    public void InventoryGraphIsExplicitAndCannotBypassAnyOriginalCalibrationPrerequisite()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeInventoryGraph);
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-inventory-graph"]));
        var options = new LiveTrainingOptions { ObserveNativeInventoryGraph = true };
        Assert.True(options.ObserveNativeInventoryGraph);
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
        options.ObserveNativeInventoryTransients = true;
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
        options.ObserveNativeInventoryPayload = options.ObserveNativeClearanceToolInputs = options.ObserveNativeOutputSnapshotAnchor = true;
        options.ObserveNativeOutputs = true; options.ExecutionSnapshotProfile = "full";
        Assert.Throws<ArgumentException>(() => NativeOutputObservationMode.Validate(options));
        options.SkipTraining = options.RequireExecutorFeedback = options.UseRuntimeTestHarnessExecutor = true;
        NativeOutputObservationMode.Validate(options);
        Assert.DoesNotContain("action_origin_inventory_graph", JsonSerializer.Serialize(new TrainingExecutionRequest()));
        Assert.Contains("\"action_origin_inventory_graph\":true", JsonSerializer.Serialize(new TrainingExecutionRequest { ActionOriginInventoryGraph = true }));
    }
}
