using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputObservationClientTests
{
    [Fact]
    public void InventoryPayloadRequiresExplicitIsolatedToolObservationAndKeepsOtherActionsUnchanged()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeInventoryPayload);
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-inventory-payload", "--skip-training"]));
        var options = LiveTrainingOptions.Parse(["--observe-native-inventory-payload", "--observe-native-clearance-tool-inputs",
            "--observe-native-output-snapshot-anchor", "--observe-native-outputs", "--skip-training", "--use-runtime-test-harness-executor"]);
        options.RunId = "run"; options.SaveIsolationPath = "isolated-saves";
        Assert.True(Build(options, "executor.move_to_tile").ActionOriginInventoryPayload);
        Assert.True(Build(options, "executor.clear_obstacle").ActionOriginInventoryPayload);
        Assert.False(Build(options, "executor.water_crop").ActionOriginInventoryPayload);
        Assert.False(Build(options, "executor.wait_ticks").ActionOriginInventoryPayload);
    }

    [Fact]
    public void DefaultWireOmitsInventoryPayloadAndDoesNotManufactureTransientProof()
    {
        Assert.DoesNotContain("action_origin_inventory_payload", JsonSerializer.Serialize(new TrainingExecutionRequest()));
        Assert.DoesNotContain("inventory_payload_before", JsonSerializer.Serialize(new ActionOriginObservationReceipt()));
        Assert.DoesNotContain("inventory_payload_after", JsonSerializer.Serialize(new ActionOriginObservationReceipt()));
        Assert.False(new ActionOriginInventoryPayload().FullTransientStateVerified);
    }
}
