using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    internal static bool TryMachineSingleAttemptProbability(
        AcquisitionRouteCalendarResolution route,
        out double probability,
        out string blockingReason)
    {
        probability = 0d;
        blockingReason = string.Empty;
        var source = route.MachineSource;
        if (!IsMachineRoute(route.RouteKind) || source is null)
        {
            blockingReason = "machine_probability_source_missing";
            return false;
        }
        var rows = source.OutputSelectionRows;
        if (rows is null || rows.Length != source.OutputSelectionCount ||
            rows.Length == 0 || source.OutputIndex < 0 ||
            source.OutputIndex >= rows.Length ||
            rows.Select(row => row.OutputIndex).Distinct().Count() !=
                rows.Length ||
            rows.Any(row => row.OutputIndex < 0 ||
                row.OutputIndex >= rows.Length))
        {
            blockingReason = "machine_output_selection_rows_incomplete";
            return false;
        }
        var ordered = rows.OrderBy(row => row.OutputIndex).ToArray();
        var current = ordered[source.OutputIndex];
        if (current.OutputIndex != source.OutputIndex ||
            current.Condition != source.OutputCondition ||
            current.OutputItemQuery != source.OutputItemQuery ||
            current.RandomItemId != source.RandomItemId ||
            current.MinimumStack != source.MinimumStack ||
            current.MaximumStack != source.MaximumStack ||
            current.OutputMethod != source.OutputMethod ||
            current.PerItemCondition != source.PerItemCondition)
        {
            blockingReason = "machine_output_selection_row_identity_drifted";
            return false;
        }
        if (source.RandomItemId.Length > 0 ||
            source.OutputItemQuery.Contains('|', StringComparison.Ordinal) ||
            source.OutputMethod.Length > 0 ||
            source.PerItemCondition.Length > 0)
        {
            blockingReason = "machine_output_identity_probability_unresolved";
            return false;
        }

        if (source.UseFirstValidOutput)
        {
            probability = 1d;
            for (var index = 0; index <= source.OutputIndex; index++)
            {
                if (!TrySimpleMachineConditionProbability(
                        ordered[index].Condition,
                        out var conditionProbability))
                {
                    blockingReason =
                        "machine_first_valid_condition_probability_unresolved:" +
                        index;
                    return false;
                }
                probability *= index == source.OutputIndex
                    ? conditionProbability
                    : 1d - conditionProbability;
            }
            return true;
        }

        if (ordered.Length == 1)
        {
            if (TrySimpleMachineConditionProbability(
                    ordered[0].Condition,
                    out probability))
            {
                return true;
            }
            blockingReason =
                "machine_single_random_valid_condition_probability_unresolved";
            return false;
        }
        if (ordered.Any(row =>
                !TrySimpleMachineConditionProbability(
                    row.Condition,
                    out var rowProbability) ||
                rowProbability != 1d))
        {
            blockingReason =
                "machine_conditional_random_valid_set_probability_unresolved";
            return false;
        }
        probability = 1d / ordered.Length;
        return true;
    }

    private static bool TrySimpleMachineConditionProbability(
        string condition,
        out double probability)
    {
        probability = 0d;
        if (string.IsNullOrWhiteSpace(condition) ||
            string.Equals(condition.Trim(), "TRUE", StringComparison.Ordinal))
        {
            probability = 1d;
            return true;
        }
        var parts = condition.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
            string.Equals(parts[0], "RANDOM", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out probability) &&
            probability >= 0d && probability <= 1d;
    }
}
