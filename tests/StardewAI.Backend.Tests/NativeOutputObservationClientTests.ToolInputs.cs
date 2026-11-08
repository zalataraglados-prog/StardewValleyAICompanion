using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputObservationClientTests
{
    [Fact]
    public void NativeToolInputsAreExplicitAndOnlyOriginalObservedItemsAreEligible()
    {
        Assert.False(LiveTrainingOptions.Parse([]).ObserveNativeClearanceToolInputs);
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--observe-native-clearance-tool-inputs", "--skip-training"]));
        var options = LiveTrainingOptions.Parse(["--observe-native-clearance-tool-inputs", "--observe-native-output-snapshot-anchor",
            "--observe-native-outputs", "--skip-training", "--use-runtime-test-harness-executor"]);
        options.RunId = "run"; options.SaveIsolationPath = "isolated-saves";
        Assert.True(Build(options, "executor.clear_obstacle").ActionOriginToolInputs);
        Assert.True(Build(options, "executor.move_to_tile").ActionOriginToolInputs);
        Assert.False(Build(options, "executor.wait_ticks").ActionOriginToolInputs);
        options.ObserveNativeClearanceToolInputs = false;
        Assert.False(Build(options, "executor.clear_obstacle").ActionOriginToolInputs);
    }

    [Fact]
    public void OldWireOmitsNewFalseFlagAndNullListButExplicitValuesRoundTrip()
    {
        Assert.DoesNotContain("action_origin_tool_inputs", JsonSerializer.Serialize(new TrainingExecutionRequest()));
        Assert.DoesNotContain("clearance_tool_calls", JsonSerializer.Serialize(new ActionOriginObservationReceipt()));
        var json = JsonSerializer.Serialize(new TrainingExecutionRequest { ActionOriginToolInputs = true });
        Assert.True(JsonSerializer.Deserialize<TrainingExecutionRequest>(json)!.ActionOriginToolInputs);
        var receipt = JsonSerializer.Deserialize<ActionOriginObservationReceipt>(
            JsonSerializer.Serialize(new ActionOriginObservationReceipt { ClearanceToolCalls = [] }))!;
        Assert.Empty(receipt.ClearanceToolCalls!);
    }
}
