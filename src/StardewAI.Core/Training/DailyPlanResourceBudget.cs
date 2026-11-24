using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training;

// Shared original Backend budget rules, also used when replaying its frozen
// requests. No second clock or alternative budget is introduced.
public static class DailyPlanResourceBudget
{
    public static int? JointEnergyBudget(SnapshotEnvelope? snapshot) =>
        SnapshotEnergyBudgetReader.AvailableWholeActionEnergy(snapshot);

    public static int? AvailablePlanMinutes(SnapshotEnvelope? snapshot,
        IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        var time = ReadSnapshotInt(snapshot, "time", "time");
        return time.HasValue ? AvailablePlanMinutes(time.Value, candidates) : null;
    }

    public static int AvailablePlanMinutes(int time, IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        var recovery = candidates.Any(candidate => candidate.OptionId == "recovery.stabilize_day" ||
            candidate.Kind.StartsWith("recovery_", StringComparison.Ordinal));
        var deadline = recovery ? GameClockBudgetPolicy.DayEndTime : GameClockBudgetPolicy.AutonomousRecoveryStartTime;
        var buffer = recovery ? GameClockBudgetPolicy.RecoverySafetyBufferMinutes : 0;
        return Math.Max(0, GameClockBudgetPolicy.ClockMinutesBetween(time, deadline) - buffer);
    }

    public static int? ReadSnapshotInt(SnapshotEnvelope? snapshot, string sectionName, string fieldName)
    {
        if (snapshot is null || !snapshot.State.TryGetValue(sectionName, out var section) ||
            section.ValueKind != JsonValueKind.Object || !section.TryGetProperty(fieldName, out var field) ||
            field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)) return null;
        return number;
    }
}
