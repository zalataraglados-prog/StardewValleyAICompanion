using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static class NativeDaySettlementSupport
{
    internal static bool IsSingleNativeSleepQueue(ActionQueueEnvelope queue)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        if (items.Length != 1)
            return false;
        var item = items[0];
        var parameters = item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        return item.OptionId == "executor.sleep" ||
            (item.OptionId == "recovery.stabilize_day" &&
                parameters.Any(parameter =>
                    parameter.Name == "execution_option_id" &&
                    parameter.Value == "executor.sleep"));
    }
}
