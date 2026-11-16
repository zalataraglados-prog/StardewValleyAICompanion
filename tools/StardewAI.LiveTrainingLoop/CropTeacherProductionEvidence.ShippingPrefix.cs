using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal static partial class CropTeacherProductionEvidence
{
    // Classification only: the full lifecycle and original producer-retention builder own verification.
    private static MoneyRevenueCropNativeLifecycleTransitionRequest[]? ReadObservedShippingTail(
        MoneyRevenueCropNativeLifecycleTransitionRequest[] transitions, int lastHarvestIndex,
        string qualifiedItemId, int harvestDay)
    {
        if (lastHarvestIndex < 0 || lastHarvestIndex >= transitions.Length)
            throw new InvalidDataException("Observed shipping has no original complete harvest boundary.");
        for (var index = lastHarvestIndex + 1; index < transitions.Length; index++)
        {
            var transition = transitions[index];
            if (transition.TotalDay != harvestDay || transition.NativeSharedSource is null) continue;
            var queue = Read<ActionQueueEnvelope>(transition.QueuePath);
            if (!queue.Items.Any(item => item.OptionId == "executor.ship_inventory_item_to_bin" &&
                item.NormalizedCommand.Parameters.Any(parameter => parameter.Name == "qualified_item_id" && parameter.Value == qualifiedItemId))) continue;
            // Keep every original transition through the actual head; the terminal consumer must
            // explain the whole tail, including controls, rather than filtering to matching ships.
            return transitions[index..];
        }
        return null;
    }
}
