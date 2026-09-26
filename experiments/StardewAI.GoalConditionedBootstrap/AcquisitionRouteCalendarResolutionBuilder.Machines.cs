using System.Globalization;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution ResolveMachineWindows(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        JsonElement machines,
        int deadlineTotalDayExclusive)
    {
        if (!TryParseMachineSource(
                route,
                out var machineId,
                out var ruleIndex,
                out var outputIndex))
        {
            return BlockMachine(
                "blocked_authoritative_machine_output_not_found",
                "machine_route_source_identity_is_malformed");
        }
        if (!machines.TryGetProperty(machineId, out var machine) ||
            machine.ValueKind != JsonValueKind.Object)
        {
            return BlockMachine(
                "blocked_authoritative_machine_output_not_found",
                "machine_id_not_found_in_runtime_data_machines");
        }
        if (!TryArrayElement(machine, "OutputRules", ruleIndex, out var rule) ||
            !TryArrayElement(rule, "OutputItem", outputIndex, out var output))
        {
            return BlockMachine(
                "blocked_authoritative_machine_output_not_found",
                "machine_rule_or_output_not_found_in_runtime_data_machines");
        }

        var ruleId = ReadMachineString(rule, "Id");
        var expectedSourceId = route.RouteKind == "machine_output"
            ? $"machine:{machineId}:rule:{(ruleId.Length > 0 ? ruleId : ruleIndex)}"
            : $"machine:{machineId}:rule:{ruleIndex}:output:{outputIndex}";
        if (!string.Equals(route.SourceId, expectedSourceId, StringComparison.Ordinal))
        {
            return BlockMachine(
                "blocked_authoritative_machine_source_mismatch",
                "machine_route_source_id_does_not_match_exact_output_row");
        }

        var outputItemQuery = ReadMachineString(output, "ItemId");
        var targetItemId = qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal)
            ? qualifiedItemId[3..]
            : string.Empty;
        if (targetItemId.Length == 0 ||
            !AuthoritativeRequirementInventoryBuilder
                .MachineOutputItemIds(outputItemQuery)
                .Contains(targetItemId, StringComparer.Ordinal))
        {
            return BlockMachine(
                "blocked_authoritative_machine_item_mismatch",
                "machine_output_item_query_does_not_match_requirement");
        }

        var flavored = outputItemQuery.StartsWith(
            "FLAVORED_ITEM ",
            StringComparison.Ordinal);
        if (route.RouteKind == "native_machine_flavored_output" != flavored ||
            route.RouteKind == "native_machine_item_query_output" && flavored)
        {
            return BlockMachine(
                "blocked_authoritative_machine_route_kind_mismatch",
                "machine_route_kind_does_not_match_output_item_query");
        }

        var triggers = ReadMachineArray(rule, "Triggers", allowNull: false)
            .Select((trigger, index) => new AcquisitionMachineTriggerEvidence(
                ReadMachineString(trigger, "Id"),
                ReadRequiredMachineInt(trigger, "Trigger", MachineKey(machineId, ruleIndex, index)),
                ReadMachineString(trigger, "RequiredItemId"),
                ReadMachineStringArray(trigger, "RequiredTags"),
                ReadRequiredMachineInt(trigger, "RequiredCount", MachineKey(machineId, ruleIndex, index)),
                ReadMachineString(trigger, "Condition")))
            .ToArray();
        var additionalConsumedItems = ReadMachineArray(
                machine,
                "AdditionalConsumedItems",
                allowNull: true)
            .Select((item, index) => new AcquisitionMachineConsumedItemEvidence(
                ReadMachineString(item, "ItemId"),
                ReadRequiredMachineInt(
                    item,
                    "RequiredCount",
                    MachineKey(machineId, ruleIndex, index))))
            .ToArray();
        var readyTimeModifiers = ReadMachineModifiers(
            machine,
            "ReadyTimeModifiers",
            machineId);
        var stackModifiers = ReadMachineModifiers(
            output,
            "StackModifiers",
            MachineKey(machineId, ruleIndex, outputIndex));
        var qualityModifiers = ReadMachineModifiers(
            output,
            "QualityModifiers",
            MachineKey(machineId, ruleIndex, outputIndex));
        var outputRows = ReadMachineArray(rule, "OutputItem", allowNull: false);

        var ruleCondition = ReadMachineString(rule, "Condition");
        var outputCondition = ReadMachineString(output, "Condition");
        var perItemCondition = ReadMachineString(output, "PerItemCondition");
        var randomItemId = ReadMachineStringOrJson(output, "RandomItemId");
        var useFirstValidOutput = ReadRequiredMachineBool(
            rule,
            "UseFirstValidOutput",
            MachineKey(machineId, ruleIndex));
        var minimumStack = ReadRequiredMachineInt(
            output,
            "MinStack",
            MachineKey(machineId, ruleIndex, outputIndex));
        var maximumStack = ReadRequiredMachineInt(
            output,
            "MaxStack",
            MachineKey(machineId, ruleIndex, outputIndex));
        var allConditions = new[]
            {
                ruleCondition,
                outputCondition,
                perItemCondition
            }
            .Concat(triggers.Select(value => value.Condition))
            .Concat(readyTimeModifiers.Select(value => value.Condition))
            .Concat(stackModifiers.Select(value => value.Condition))
            .Concat(qualityModifiers.Select(value => value.Condition))
            .Where(value => value.Length > 0)
            .ToArray();
        var stochasticOutcome =
            !useFirstValidOutput && outputRows.Length > 1 ||
            outputItemQuery.Contains('|') ||
            randomItemId.Length > 0 ||
            maximumStack >= 0 && minimumStack >= 0 && maximumStack != minimumStack ||
            allConditions.Any(HasRandomMachineExpression) ||
            readyTimeModifiers.Concat(stackModifiers).Concat(qualityModifiers)
                .Any(value => value.RandomAmount.HasValue);

        var source = new AcquisitionMachineSourceEvidence(
            machineId,
            ruleId,
            ruleIndex,
            outputIndex,
            ruleCondition,
            useFirstValidOutput,
            ReadRequiredMachineInt(rule, "MinutesUntilReady", MachineKey(machineId, ruleIndex)),
            ReadRequiredMachineInt(rule, "DaysUntilReady", MachineKey(machineId, ruleIndex)),
            ReadRequiredMachineBool(machine, "OnlyCompleteOvernight", machineId),
            ReadRequiredMachineBool(rule, "RecalculateOnCollect", MachineKey(machineId, ruleIndex)),
            ReadRequiredMachineInt(machine, "ReadyTimeModifierMode", machineId),
            readyTimeModifiers,
            triggers,
            additionalConsumedItems,
            outputItemQuery,
            ReadMachineString(output, "OutputMethod"),
            outputCondition,
            perItemCondition,
            randomItemId,
            minimumStack,
            maximumStack,
            ReadRequiredMachineInt(output, "Quality", MachineKey(machineId, ruleIndex, outputIndex)),
            ReadRequiredMachineBool(output, "CopyQuality", MachineKey(machineId, ruleIndex, outputIndex)),
            ReadRequiredMachineInt(output, "StackModifierMode", MachineKey(machineId, ruleIndex, outputIndex)),
            stackModifiers,
            ReadRequiredMachineInt(output, "QualityModifierMode", MachineKey(machineId, ruleIndex, outputIndex)),
            qualityModifiers,
            outputRows.Length,
            stochasticOutcome);

        Require(deadlineTotalDayExclusive > 0,
            "Machine calendar deadline must be positive.");
        var dynamicConditions = new[]
            {
                ruleCondition,
                outputCondition,
                perItemCondition
            }
            .Where(value => value.Length > 0 && !HasRandomMachineExpression(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var window = new AuthoritativeCalendarSourceWindow
        {
            SourceKind = "machine_output_rule",
            SourceKey = route.SourceId,
            RuleId = ruleId,
            FirstTotalDay = 0,
            LastTotalDay = deadlineTotalDayExclusive - 1,
            TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
            WeatherModes = NativeCalendarConstraintNormalizer.AllWeatherModes,
            DynamicConditions = dynamicConditions,
            RequiresLocationAccessEvidence = true,
            RequiresExistingLiveCandidateMatch = true,
            StochasticOutcome = stochasticOutcome
        };
        return new CalendarSourceResolution(
            ResolvedStatus,
            "runtime_machine_output_rule",
            new[] { window },
            Array.Empty<string>(),
            MachineSource: source);
    }

    private static CalendarSourceResolution BlockMachine(string status, string reason) =>
        new(
            status,
            string.Empty,
            Array.Empty<AuthoritativeCalendarSourceWindow>(),
            new[] { reason });

    private static bool TryParseMachineSource(
        AcquisitionRequirementRouteLowering route,
        out string machineId,
        out int ruleIndex,
        out int outputIndex)
    {
        machineId = string.Empty;
        ruleIndex = -1;
        outputIndex = -1;
        if (!string.Equals(route.SourceAsset, "Data/Machines", StringComparison.Ordinal))
            return false;

        const string prefix = "payload.";
        const string ruleMarker = ".OutputRules[";
        const string outputMarker = "].OutputItem[";
        const string suffix = "].ItemId";
        if (!route.SourcePath.StartsWith(prefix, StringComparison.Ordinal) ||
            !route.SourcePath.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }
        var ruleMarkerIndex = route.SourcePath.IndexOf(
            ruleMarker,
            prefix.Length,
            StringComparison.Ordinal);
        if (ruleMarkerIndex <= prefix.Length)
            return false;
        var outputMarkerIndex = route.SourcePath.IndexOf(
            outputMarker,
            ruleMarkerIndex + ruleMarker.Length,
            StringComparison.Ordinal);
        if (outputMarkerIndex < 0)
            return false;

        machineId = route.SourcePath[prefix.Length..ruleMarkerIndex];
        var ruleText = route.SourcePath[
            (ruleMarkerIndex + ruleMarker.Length)..outputMarkerIndex];
        var outputText = route.SourcePath[
            (outputMarkerIndex + outputMarker.Length)..^suffix.Length];
        return machineId.Length > 0 &&
            int.TryParse(ruleText, NumberStyles.None, CultureInfo.InvariantCulture, out ruleIndex) &&
            int.TryParse(outputText, NumberStyles.None, CultureInfo.InvariantCulture, out outputIndex) &&
            ruleIndex >= 0 &&
            outputIndex >= 0;
    }

}
