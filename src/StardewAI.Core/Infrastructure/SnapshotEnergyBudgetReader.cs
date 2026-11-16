using System;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public static class SnapshotEnergyBudgetReader
{
    public static int? AvailableWholeActionEnergy(SnapshotEnvelope? snapshot)
    {
        if (snapshot is null || !SnapshotValueReader.ReadableStatus(
                SnapshotValueReader.ReadStateFieldStatus(snapshot, "player", "energy"))) return null;
        var energy = SnapshotValueReader.ReadStateFieldDoubleOptional(snapshot, "player", "energy");
        // Integer candidate costs may spend only the floor of actual fractional stamina.
        return energy.HasValue && !double.IsNaN(energy.Value) && !double.IsInfinity(energy.Value) && energy.Value <= int.MaxValue
            ? (int)Math.Max(0, Math.Floor(energy.Value)) : null;
    }
}
