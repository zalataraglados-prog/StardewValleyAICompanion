using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionRouteSupportingTransitionQueueShapes
{
    internal static bool Purchase(ActionQueueItem[] items)
    {
        var stages = items.Select(item => UniqueParameter(
                item.NormalizedCommand?.Parameters,
                "acquisition_support_purchase_stage"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (stages.Length != 1)
            return false;

        var optionIds = items.Select(item => item.OptionId).ToArray();
        return stages[0] switch
        {
            "route_connector" => items.Length == 1,
            "shop_interaction" => optionIds.SequenceEqual(
                    new[]
                    {
                        "executor.move_to_tile",
                        "executor.interact"
                    },
                    StringComparer.Ordinal) ||
                optionIds.SequenceEqual(
                    new[]
                    {
                        "executor.move_to_tile",
                        "executor.interact",
                        "executor.choose_dialogue_response"
                    },
                    StringComparer.Ordinal),
            "purchase" => optionIds.SequenceEqual(
                    new[]
                    {
                        "executor.buy_shop_item",
                        "executor.close_menu"
                    },
                    StringComparer.Ordinal) ||
                optionIds.SequenceEqual(
                    new[]
                    {
                        "executor.wait_ticks",
                        "executor.buy_shop_item",
                        "executor.close_menu"
                    },
                    StringComparer.Ordinal),
            _ => false
        };
    }

    private static string UniqueParameter(
        SmallModelActionParameter[]? parameters,
        string name)
    {
        var values = (parameters ?? Array.Empty<SmallModelActionParameter>())
            .Where(parameter => parameter.Name == name)
            .Select(parameter => parameter.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }
}
