using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputObservationClientTests
{
    [Fact]
    public void TransientsRequireOriginalPayloadModeAndOnlyReachObservedMoveOrClear()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeInventoryTransients);
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-inventory-transients", "--skip-training"]));
        var options = LiveTrainingOptions.Parse(["--observe-native-inventory-transients", "--observe-native-inventory-payload",
            "--observe-native-clearance-tool-inputs", "--observe-native-output-snapshot-anchor", "--observe-native-outputs",
            "--skip-training", "--use-runtime-test-harness-executor"]);
        options.RunId = "run"; options.SaveIsolationPath = "isolated-saves";
        Assert.True(Build(options, "executor.move_to_tile").ActionOriginInventoryTransients);
        Assert.True(Build(options, "executor.clear_obstacle").ActionOriginInventoryTransients);
        Assert.False(Build(options, "executor.water_crop").ActionOriginInventoryTransients);
        Assert.False(Build(options, "executor.wait_ticks").ActionOriginInventoryTransients);
    }

    [Fact]
    public void DefaultWireDoesNotAddUnavailableTransientStateOrAuthorization()
    {
        Assert.DoesNotContain("action_origin_inventory_transients", JsonSerializer.Serialize(new TrainingExecutionRequest()));
        Assert.DoesNotContain("transient_state", JsonSerializer.Serialize(new ActionOriginInventoryPayloadSlot()));
        Assert.DoesNotContain("selection_transient_state", JsonSerializer.Serialize(new ActionOriginInventoryPayload()));
    }
}
