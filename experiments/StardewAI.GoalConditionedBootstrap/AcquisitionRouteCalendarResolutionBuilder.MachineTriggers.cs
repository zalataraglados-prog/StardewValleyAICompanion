namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private const int ItemPlacedInMachineTrigger = 1;

    internal static MachineTriggerConditionProjection
        ProjectMachineTriggerConditions(
            AcquisitionMachineTriggerEvidence[] triggers)
    {
        var alternatives = new List<
            AcquisitionMachineTriggerConditionAlternativeEvidence>();
        var unsupported = new List<string>();
        for (var index = 0; index < triggers.Length; index++)
        {
            var trigger = triggers[index];
            var location = new List<string>();
            var resource = new List<string>();
            var stochastic = new List<string>();
            foreach (var clause in SplitTriggerCondition(trigger.Condition))
            {
                var tokens = clause.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
                if (IsSupportedLocationSeasonCondition(tokens))
                {
                    location.Add(clause);
                    continue;
                }
                if ((trigger.Trigger & ItemPlacedInMachineTrigger) != 0 &&
                    IsSupportedMachineInputCondition(tokens))
                {
                    resource.Add(clause);
                    continue;
                }
                if (IsSupportedRandomCondition(tokens))
                {
                    stochastic.Add(clause);
                    continue;
                }
                unsupported.Add(
                    "machine_trigger_condition_unsupported:" +
                    StableTriggerId(trigger.Id, index));
            }
            alternatives.Add(
                new AcquisitionMachineTriggerConditionAlternativeEvidence(
                    index,
                    trigger.Id,
                    trigger.Trigger,
                    trigger.Condition,
                    location.Distinct(StringComparer.Ordinal).ToArray(),
                    resource.Distinct(StringComparer.Ordinal).ToArray(),
                    stochastic.ToArray()));
        }

        if (triggers.Length > 1 && alternatives.Any(value =>
                value.LocationConditions.Length > 0 ||
                value.StochasticConditions.Length > 0))
        {
            unsupported.Add(
                "machine_trigger_condition_or_semantics_not_supported");
        }
        var blocking = unsupported.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new MachineTriggerConditionProjection(
            blocking.Length == 0,
            new AcquisitionMachineTriggerConditionSetEvidence(
                "or",
                alternatives.ToArray()),
            triggers.Length == 1
                ? alternatives.Single().LocationConditions
                : Array.Empty<string>(),
            blocking);
    }

    private static IEnumerable<string> SplitTriggerCondition(
        string condition) =>
        string.IsNullOrWhiteSpace(condition)
            ? Array.Empty<string>()
            : condition.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

    private static bool IsSupportedLocationSeasonCondition(string[] tokens)
    {
        if (tokens.Length < 3)
            return false;
        var predicate = tokens[0].StartsWith('!')
            ? tokens[0][1..]
            : tokens[0];
        return predicate == "LOCATION_SEASON" &&
            tokens[1] == "Target" &&
            tokens.Skip(2).All(IsSeason);
    }

    private static bool IsSupportedMachineInputCondition(string[] tokens)
    {
        if (tokens.Length == 0)
            return false;
        var negated = tokens[0].StartsWith('!');
        var predicate = negated ? tokens[0][1..] : tokens[0];
        return predicate switch
        {
            "ITEM_CONTEXT_TAG" => tokens.Length >= 3 &&
                tokens[1].Equals("Input", StringComparison.OrdinalIgnoreCase),
            "ITEM_EDIBILITY" => !negated &&
                tokens.Length is >= 2 and <= 4 &&
                tokens[1].Equals("Input", StringComparison.OrdinalIgnoreCase) &&
                tokens.Skip(2).All(value => int.TryParse(value, out _)),
            _ => false
        };
    }

    private static bool IsSupportedRandomCondition(string[] tokens) =>
        tokens.Length == 2 &&
        tokens[0] == "RANDOM" &&
        double.TryParse(
            tokens[1],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var probability) &&
        probability is >= 0d and <= 1d;

    private static bool IsSeason(string value) => value.ToLowerInvariant() is
        "spring" or "summer" or "fall" or "winter";

    private static string StableTriggerId(string value, int index) =>
        string.IsNullOrWhiteSpace(value) ? index.ToString() : value;
}

internal sealed record MachineTriggerConditionProjection(
    bool Supported,
    AcquisitionMachineTriggerConditionSetEvidence ConditionSet,
    string[] PendingLocationConditions,
    string[] BlockingReasons);
