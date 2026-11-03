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
            "shop_interaction" => ShopInteraction(optionIds),
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

    private static bool ShopInteraction(string[] options)
    {
        // The shared compiler elides movement when the actor already occupies the stand tile.
        var actions = options.Skip(options.FirstOrDefault() == "executor.move_to_tile" ? 1 : 0).ToArray();
        return actions.SequenceEqual(new[] { "executor.interact" }, StringComparer.Ordinal) ||
            actions.SequenceEqual(new[] { "executor.interact", "executor.choose_dialogue_response" }, StringComparer.Ordinal);
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
