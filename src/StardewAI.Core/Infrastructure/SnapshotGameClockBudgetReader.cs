using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static class SnapshotGameClockBudgetReader
{
    public static int? AvailableMinutes(SnapshotEnvelope snapshot, int deadline)
    {
        if (deadline is < 600 or > GameClockBudgetPolicy.DayEndTime || deadline % 100 >= 60)
            throw new ArgumentOutOfRangeException(nameof(deadline));
        var time = CurrentTime(snapshot);
        return time.HasValue ? Math.Max(0, GameClockBudgetPolicy.ClockMinutesBetween(time.Value, deadline)) : null;
    }

    public static int? CurrentTime(SnapshotEnvelope snapshot)
    {
        var clocks = new[] { "time", "time_of_day" }
            .Where(field => ReadableStatus(ReadStateFieldStatus(snapshot, "time", field)))
            .Select(field => ReadStateFieldIntOptional(snapshot, "time", field)).ToArray();
        if (clocks.Any(value => !value.HasValue)) return null;
        var values = clocks.Select(value => value!.Value).Distinct().ToArray();
        return values.Length == 1 && values[0] is >= 600 and <= GameClockBudgetPolicy.DayEndTime && values[0] % 100 < 60
            ? values[0] : null;
    }
}
