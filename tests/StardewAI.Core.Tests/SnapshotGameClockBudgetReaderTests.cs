using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class SnapshotGameClockBudgetReaderTests
{
    [Theory]
    [InlineData(1000, 1900, 540)]
    [InlineData(1852, 1900, 8)]
    [InlineData(1900, 1900, 0)]
    [InlineData(2120, 1900, 0)]
    [InlineData(2120, 2600, 280)]
    [InlineData(2600, 2600, 0)]
    [InlineData(1860, 2600, null)]
    [InlineData(559, 1900, null)]
    [InlineData(2601, 2600, null)]
    public void ValidatesClockAndPreservesCallerDeadline(int time, int deadline, int? expected)
    {
        Assert.Equal(expected, SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new
        {
            time = new { value = time, status = "available" }
        }), deadline));
    }

    [Theory]
    [InlineData(1000, 540)]
    [InlineData(1100, null)]
    public void ReadableAliasesMustAgree(int alias, int? expected)
    {
        Assert.Equal(expected, SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new
        {
            time = new { value = 1000, status = "available" },
            time_of_day = new { value = alias, status = "derived" }
        }), 1900));
    }

    [Fact]
    public void AliasAloneIsSupportedButMissingUnreadableAndMalformedAreNotBudgets()
    {
        Assert.Equal(540, SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new
        {
            time_of_day = new { value = 1000, status = "available" }
        }), 1900));
        Assert.Null(SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new { }), 1900));
        Assert.Null(SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new
        {
            time = new { value = 1000, status = "unavailable" }
        }), 1900));
        Assert.Null(SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new
        {
            time = new { value = "1000", status = "available" }
        }), 1900));
    }

    [Theory]
    [InlineData(1860)]
    [InlineData(2700)]
    [InlineData(500)]
    public void InvalidDeadlineIsAProgrammingError(int deadline) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SnapshotGameClockBudgetReader.AvailableMinutes(Snapshot(new { }), deadline));

    private static SnapshotEnvelope Snapshot(object clock) => new()
    {
        State = new Dictionary<string, JsonElement> { ["time"] = JsonSerializer.SerializeToElement(clock) }
    };
}
