using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeAutoGrabberExecutorTests
{
    [Fact]
    public void AutoGrabberRequestFieldsAndHarnessDispatchAreTyped()
    {
        var request = new TrainingExecutionRequest
        {
            AutoGrabberContentsBeforeJson = "[]",
            AutoGrabberTransferableContentsJson = "[]",
            AutoGrabberExpectedTransferQuantity = 5
        };

        Assert.Equal("[]", request.AutoGrabberContentsBeforeJson);
        Assert.Equal("[]", request.AutoGrabberTransferableContentsJson);
        Assert.Equal(5, request.AutoGrabberExpectedTransferQuantity);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("animals.collect_auto_grabber_contents"));
    }
}
