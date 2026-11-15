using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal static partial class CropTeacherProductionEvidence
{
    private static MoneyRevenueCropNativeLifecycleCheckpointResumeRequest[] SavedReadbackResumes(
        CropTeacherManagedProductionTrace trace, CropTeacherManagedProductionTransition[] transitions)
    {
        var result = new List<MoneyRevenueCropNativeLifecycleCheckpointResumeRequest>();
        for (var index = 1; index < transitions.Length; index++)
        {
            var references = ReadbacksBetween(trace, transitions[index - 1], transitions[index]);
            if (references.Length == 0) continue;
            var before = Read<SnapshotEnvelope>(transitions[index - 1].NativeTransition.AfterSnapshotPath);
            var after = Read<SnapshotEnvelope>(transitions[index].NativeTransition.BeforeSnapshotPath);
            if (before.StateHash == after.StateHash && before.GameTick == after.GameTick) continue;
            // Declare the accepted owner relation; the lifecycle consumer verifies it once when closing the chain.
            result.Add(new(index) { AcceptedSavedReadbacks = references });
        }
        return result.ToArray();
    }

    private static MoneyRevenueCropAcceptedSavedReadbackReference[] ReadbacksBetween(CropTeacherManagedProductionTrace trace,
        CropTeacherManagedProductionTransition previous, CropTeacherManagedProductionTransition next) =>
        trace.SavedContentReadbacks.Where(row => row.OwnerSequence > previous.OwnerSequence && row.OwnerSequence < next.OwnerSequence)
            .OrderBy(row => row.OwnerSequence).Select(row => row.Reference).ToArray();
}
