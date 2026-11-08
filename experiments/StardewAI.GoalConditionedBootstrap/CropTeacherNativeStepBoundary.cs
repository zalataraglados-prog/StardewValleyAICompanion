using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropTeacherNativeStepBoundary
{
    internal static void Verify(SnapshotEnvelope before, SnapshotEnvelope after, SnapshotEnvelope source,
        QueueExecutionStepReceipt step, int totalDay, string label = "Owned planting")
    {
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(after, label.ToLowerInvariant() + " step after");
        var clock = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "time");
        var beforeClock = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "time");
        Require(after.SaveId.Value == source.SaveId.Value && after.PlayerId.Value == source.PlayerId.Value &&
                after.GameVersion == source.GameVersion && step.SourceStateHash == before.StateHash &&
                step.AfterStateHash == after.StateHash && step.BeforeGameTick == before.GameTick && step.AfterGameTick == after.GameTick &&
                after.GameTick >= before.GameTick &&
                MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "total_days") == totalDay &&
                clock >= beforeClock && FieldEnvelopeValidator.IsReadableStatus(before.InGameTime.Status) &&
                before.InGameTime.Value == beforeClock &&
                FieldEnvelopeValidator.IsReadableStatus(after.InGameTime.Status) && after.InGameTime.Value == clock,
            label + " step source/actor/tick/day/clock drifted.");
    }
}
