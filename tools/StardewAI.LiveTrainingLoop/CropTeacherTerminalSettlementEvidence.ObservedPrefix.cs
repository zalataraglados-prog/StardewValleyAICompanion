using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal static partial class CropTeacherTerminalSettlementEvidence
{
    private static int ReadObservedDepositCount(CropTeacherObservedShippingPrefix prefix,
        CropTeacherTerminalShippingTarget target, CropSeedRuntimeArtifacts artifacts)
    {
        // Classification only, so the original native prefix is verified once by the proof builder.
        // No intent count becomes settlement credit before that original-outcome verification.
        var count = prefix.Transitions.SelectMany(row => artifacts.Read<ActionQueueEnvelope>(row.QueuePath).Items)
            .Count(item => item.OptionId == "executor.ship_inventory_item_to_bin" &&
                item.NormalizedCommand.Parameters.Any(value => value.Name == "qualified_item_id" && value.Value == target.QualifiedItemId));
        if (count <= 0 || count > target.RequiredQuantity)
            throw new InvalidDataException("Managed terminal evidence: observed intent count exceeds the original whole obligation.");
        return count;
    }
}
