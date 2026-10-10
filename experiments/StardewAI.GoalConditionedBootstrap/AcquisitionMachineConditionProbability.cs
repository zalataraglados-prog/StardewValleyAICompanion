using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionMachineConditionProbability
{
    internal static bool TryResolveSimple(
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
