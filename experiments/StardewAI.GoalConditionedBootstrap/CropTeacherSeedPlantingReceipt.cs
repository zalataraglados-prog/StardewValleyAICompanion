using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropTeacherSeedPlantingReceipt
{
    internal static int Validate(ActionQueueEnvelope queue, SnapshotEnvelope before, QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after, MoneyRevenueCropNativeLifecycleTransitionRequest request)
        => CropTeacherAppliedPrefixReceipt.Validate(queue, before, receipt, after, request, "plant_seed", "Owned planting");

    internal static void ValidateStep(ActionQueueEnvelope queue, QueueExecutionStepReceipt step, int index,
        string expectedHash, long expectedTick, bool successful, bool completed)
        => CropTeacherAppliedPrefixReceipt.ValidateStep(queue, step, index, expectedHash, expectedTick, successful, completed, "plant_seed", "Owned planting");
}
