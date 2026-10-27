using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingUnverifiedExecutionOptionsTests
{
    [Fact]
    public void RepeatedUnverifiedExecutionStopIsDisabledByDefault()
    {
        var options = LiveTrainingOptions.Parse([]);

        Assert.Equal(0, options.MaxUnverifiedExecutionStreak);
        Assert.Equal(0, options.MaxQueueNoProgressStreak);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("3", 3)]
    [InlineData("100", 64)]
    public void RepeatedUnverifiedExecutionStopIsClamped(
        string requested,
        int expected)
    {
        var options = LiveTrainingOptions.Parse(
        [
            "--max-unverified-execution-streak",
            requested
        ]);

        Assert.Equal(expected, options.MaxUnverifiedExecutionStreak);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("8", 8)]
    [InlineData("100", 64)]
    public void RepeatedQueueNoProgressStopIsClamped(
        string requested,
        int expected)
    {
        var options = LiveTrainingOptions.Parse(
        [
            "--max-queue-no-progress-streak",
            requested
        ]);

        Assert.Equal(expected, options.MaxQueueNoProgressStreak);
    }
}
