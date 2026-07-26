using StardewValley;
using StardewValley.GameData.Machines;
using System.Reflection;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string ExactMachinePredictionStatus = "exact_current_snapshot_probe_supported";

    private static object ReadMachineExecutionSemantics(StardewValley.Object machine, MachineData? machineData)
    {
        var inputMethod = machine.GetType().GetMethod(
            nameof(StardewValley.Object.performObjectDropInAction),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Item), typeof(bool), typeof(Farmer), typeof(bool) },
            modifiers: null);
        var outputMethod = machine.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
                string.Equals(method.Name, nameof(StardewValley.Object.OutputMachine), StringComparison.Ordinal) &&
                method.GetParameters().Length == 7);
        var collectionMethod = ReadMachineMethod(
            machine,
            nameof(StardewValley.Object.checkForAction),
            typeof(Farmer),
            typeof(bool));
        var minuteUpdateMethod = ReadMachineMethod(
            machine,
            nameof(StardewValley.Object.minutesElapsed),
            typeof(int));
        var dayUpdateMethod = ReadMachineMethod(
            machine,
            nameof(StardewValley.Object.DayUpdate));
        var fairyDustMethod = ReadMachineMethod(
            machine,
            nameof(StardewValley.Object.TryApplyFairyDust),
            typeof(bool));
        var placementMethod = ReadMachineMethod(
            machine,
            nameof(StardewValley.Object.placementAction),
            typeof(GameLocation),
            typeof(int),
            typeof(int),
            typeof(Farmer));
        var inputDispatchKind = ReadMachineDispatchKind(inputMethod);
        var outputDispatchKind = ReadMachineDispatchKind(outputMethod);
        var runtimeMethods = new[]
        {
            new MachineRuntimeMethod("input", inputMethod),
            new MachineRuntimeMethod("output", outputMethod),
            new MachineRuntimeMethod("collection", collectionMethod),
            new MachineRuntimeMethod("minute_update", minuteUpdateMethod),
            new MachineRuntimeMethod("day_update", dayUpdateMethod),
            new MachineRuntimeMethod("fairy_dust", fairyDustMethod),
            new MachineRuntimeMethod("placement", placementMethod)
        };
        var nativeOverrideMethods = runtimeMethods
            .Where(row => ReadMachineDispatchKind(row.Method) == "native_runtime_override")
            .Select(row => row.Name)
            .ToArray();
        var externalOverrideMethods = runtimeMethods
            .Where(row => ReadMachineDispatchKind(row.Method) == "external_or_mod_runtime_override")
            .Select(row => row.Name)
            .ToArray();

        if (machineData is null)
        {
            return new
            {
                schema_version = "machine_execution_semantics.v1",
                status = "blocked",
                reason = "machine_data_unavailable",
                input_dispatch_kind = inputDispatchKind,
                input_method_declaring_type = inputMethod?.DeclaringType?.FullName ?? string.Empty,
                output_dispatch_kind = outputDispatchKind,
                output_method_declaring_type = outputMethod?.DeclaringType?.FullName ?? string.Empty,
                native_runtime_override_methods = nativeOverrideMethods,
                external_runtime_override_methods = externalOverrideMethods
            };
        }

        var itemPlacedRules = machineData.OutputRules?
            .Where(rule => rule.Triggers?.Any(trigger =>
                trigger.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) == true)
            .ToArray() ?? Array.Empty<MachineOutputRule>();
        var outputs = itemPlacedRules
            .SelectMany(rule => rule.OutputItem ?? new List<MachineItemOutput>())
            .ToArray();
        var randomTriggerConditionCount = itemPlacedRules
            .SelectMany(rule => rule.Triggers ?? new List<MachineOutputTriggerRule>())
            .Count(trigger =>
                trigger.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine) &&
                ConditionUsesRandomQuery(trigger.Condition));
        var randomOutputConditionCount = outputs.Count(output =>
            ConditionUsesRandomQuery(output.Condition) ||
            ConditionUsesRandomQuery(ReadString(output, "PerItemCondition")));
        var randomRuleChoiceCount = itemPlacedRules.Count(rule =>
            !rule.UseFirstValidOutput &&
            (rule.OutputItem?.Count ?? 0) > 1);
        var customOutputMethods = outputs
            .Select(output => output.OutputMethod ?? string.Empty)
            .Where(method => !string.IsNullOrWhiteSpace(method))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var vettedSpecialOutputMethods = customOutputMethods
            .Where(method =>
                IsVettedSpecialOutputMethod(machine, method))
            .ToArray();
        var unvettedCustomOutputMethods = customOutputMethods
            .Except(
                vettedSpecialOutputMethods,
                StringComparer.Ordinal)
            .ToArray();
        var vettedSpecialModelIds = vettedSpecialOutputMethods
            .Select(method =>
                ReadVettedSpecialOutputModelId(machine, method))
            .Where(modelId => modelId.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var randomItemQueryCount = outputs.Count(OutputUsesRandomItemQuery);
        var randomStackCount = outputs.Count(OutputUsesRandomStack);
        var randomModifierCount = outputs.Count(OutputUsesRandomModifier);
        var recalculateOnCollectCount = itemPlacedRules.Count(rule => rule.RecalculateOnCollect);
        var predictionBlockReasons = new List<string>();
        if (externalOverrideMethods.Length > 0)
        {
            predictionBlockReasons.Add("external_runtime_override");
        }
        if (randomTriggerConditionCount > 0)
        {
            predictionBlockReasons.Add("random_item_trigger_condition");
        }
        if (randomOutputConditionCount > 0)
        {
            predictionBlockReasons.Add("random_output_condition");
        }
        if (randomRuleChoiceCount > 0)
        {
            predictionBlockReasons.Add("random_output_rule_choice");
        }
        if (randomItemQueryCount > 0)
        {
            predictionBlockReasons.Add("random_item_query");
        }
        if (randomStackCount > 0)
        {
            predictionBlockReasons.Add("random_output_stack");
        }
        if (randomModifierCount > 0)
        {
            predictionBlockReasons.Add("random_quantity_modifier");
        }
        if (unvettedCustomOutputMethods.Length > 0)
        {
            predictionBlockReasons.Add("custom_output_method_requires_vetted_semantics");
        }
        if (recalculateOnCollectCount > 0)
        {
            predictionBlockReasons.Add("output_recalculates_on_collect");
        }

        var executionStatus = externalOverrideMethods.Length > 0
            ? "blocked_unclassified_runtime_override"
            : nativeOverrideMethods.Length > 0
                ? "available_native_runtime_override"
                : inputDispatchKind == "base_object_data_driven"
                    ? "available_data_driven"
                    : "blocked_unclassified_runtime_override";
        var trainingStatus = itemPlacedRules.Length == 0
            ? "not_applicable_no_item_placed_rule"
            : predictionBlockReasons.Count == 0
                ? ExactMachinePredictionStatus
                : "blocked_requires_special_machine_model";

        return new
        {
            schema_version = "machine_execution_semantics.v1",
            status = executionStatus.StartsWith("available_", StringComparison.Ordinal) ? "available" : "blocked",
            execution_status = executionStatus,
            runtime_type = machine.GetType().FullName ?? machine.GetType().Name,
            input_dispatch_kind = inputDispatchKind,
            input_method_declaring_type = inputMethod?.DeclaringType?.FullName ?? string.Empty,
            output_dispatch_kind = outputDispatchKind,
            output_method_declaring_type = outputMethod?.DeclaringType?.FullName ?? string.Empty,
            collection_dispatch_kind = ReadMachineDispatchKind(collectionMethod),
            collection_method_declaring_type = collectionMethod?.DeclaringType?.FullName ?? string.Empty,
            minute_update_dispatch_kind = ReadMachineDispatchKind(minuteUpdateMethod),
            minute_update_method_declaring_type = minuteUpdateMethod?.DeclaringType?.FullName ?? string.Empty,
            day_update_dispatch_kind = ReadMachineDispatchKind(dayUpdateMethod),
            day_update_method_declaring_type = dayUpdateMethod?.DeclaringType?.FullName ?? string.Empty,
            fairy_dust_dispatch_kind = ReadMachineDispatchKind(fairyDustMethod),
            fairy_dust_method_declaring_type = fairyDustMethod?.DeclaringType?.FullName ?? string.Empty,
            placement_dispatch_kind = ReadMachineDispatchKind(placementMethod),
            placement_method_declaring_type = placementMethod?.DeclaringType?.FullName ?? string.Empty,
            native_runtime_override_methods = nativeOverrideMethods,
            external_runtime_override_methods = externalOverrideMethods,
            item_placed_rule_count = itemPlacedRules.Length,
            conditional_trigger_count = itemPlacedRules
                .SelectMany(rule => rule.Triggers ?? new List<MachineOutputTriggerRule>())
                .Count(trigger =>
                    trigger.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine) &&
                    !string.IsNullOrWhiteSpace(trigger.Condition)),
            conditional_output_count = outputs.Count(output => !string.IsNullOrWhiteSpace(output.Condition)),
            random_trigger_condition_count = randomTriggerConditionCount,
            random_output_condition_count = randomOutputConditionCount,
            random_rule_choice_count = randomRuleChoiceCount,
            random_item_query_count = randomItemQueryCount,
            random_stack_count = randomStackCount,
            random_modifier_count = randomModifierCount,
            custom_output_method_count = customOutputMethods.Length,
            custom_output_methods = customOutputMethods,
            vetted_special_output_method_count =
                vettedSpecialOutputMethods.Length,
            vetted_special_output_methods =
                vettedSpecialOutputMethods,
            unvetted_custom_output_method_count =
                unvettedCustomOutputMethods.Length,
            unvetted_custom_output_methods =
                unvettedCustomOutputMethods,
            vetted_special_prediction_model_ids =
                vettedSpecialModelIds,
            recalculate_on_collect_count = recalculateOnCollectCount,
            input_probe_rng_safety_status = randomTriggerConditionCount == 0
                ? "safe_no_random_item_trigger_condition"
                : "blocked_probe_would_evaluate_random_item_trigger_condition",
            prediction_training_status = trainingStatus,
            prediction_block_reasons = predictionBlockReasons.Distinct(StringComparer.Ordinal).ToArray(),
            native_contract =
                "Object.performObjectDropInAction_probe_then_Object.PlaceInMachine_then_Object.OutputMachine"
        };
    }

    private static string ReadMachineDispatchKind(MethodInfo? method)
    {
        if (method?.DeclaringType == typeof(StardewValley.Object))
        {
            return "base_object_data_driven";
        }
        if (method?.DeclaringType?.Assembly == typeof(StardewValley.Object).Assembly)
        {
            return "native_runtime_override";
        }
        return "external_or_mod_runtime_override";
    }

    private static MethodInfo? ReadMachineMethod(
        StardewValley.Object machine,
        string name,
        params Type[] parameterTypes)
    {
        return machine.GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: parameterTypes,
            modifiers: null);
    }

    private static bool MachineInputProbeIsRngSafe(MachineData? machineData)
    {
        return machineData?.OutputRules?
            .SelectMany(rule => rule.Triggers ?? new List<MachineOutputTriggerRule>())
            .Where(trigger => trigger.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine))
            .All(trigger => !ConditionUsesRandomQuery(trigger.Condition)) != false;
    }

    private static bool ConditionUsesRandomQuery(string? condition)
    {
        return !string.IsNullOrWhiteSpace(condition) &&
            condition.IndexOf("RANDOM", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool OutputUsesRandomItemQuery(object output)
    {
        return ReadStringList(output, "RandomItemId").Length > 0;
    }

    private static bool OutputUsesRandomStack(object output)
    {
        var maxStack = ReadIntNullable(output, "MaxStack") ?? -1;
        return maxStack > 1;
    }

    private static bool OutputUsesRandomModifier(object output)
    {
        return MemberUsesRandomModifier(ReadMemberValue(output, "StackModifiers")) ||
            MemberUsesRandomModifier(ReadMemberValue(output, "QualityModifiers")) ||
            MemberUsesRandomModifier(ReadMemberValue(output, "PriceModifiers"));
    }

    private static bool MemberUsesRandomModifier(object? modifiers)
    {
        if (modifiers is not System.Collections.IEnumerable enumerable)
        {
            return false;
        }

        return enumerable.Cast<object?>()
            .Where(modifier => modifier is not null)
            .Any(modifier =>
                ReadStringList(modifier!, "RandomAmount").Length > 0 ||
                ConditionUsesRandomQuery(ReadString(modifier!, "Condition")));
    }

    private static string[] ReadPredictionBlockReasons(
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput? outputData)
    {
        var reasons = new List<string>();
        if (ConditionUsesRandomQuery(triggerRule?.Condition))
        {
            reasons.Add("random_item_trigger_condition");
        }
        if (outputRule.OutputItem?.Any(output =>
                ConditionUsesRandomQuery(output.Condition) ||
                ConditionUsesRandomQuery(ReadString(output, "PerItemCondition"))) == true)
        {
            reasons.Add("random_output_condition");
        }
        if (!outputRule.UseFirstValidOutput && (outputRule.OutputItem?.Count ?? 0) > 1)
        {
            reasons.Add("random_output_rule_choice");
        }
        if (outputData is not null)
        {
            if (!string.IsNullOrWhiteSpace(outputData.OutputMethod))
            {
                reasons.Add("custom_output_method_requires_vetted_semantics");
            }
            if (OutputUsesRandomItemQuery(outputData))
            {
                reasons.Add("random_item_query");
            }
            if (OutputUsesRandomStack(outputData))
            {
                reasons.Add("random_output_stack");
            }
            if (OutputUsesRandomModifier(outputData))
            {
                reasons.Add("random_quantity_modifier");
            }
            if (!MachineOutputItemQueryIsBounded(outputData))
            {
                reasons.Add("unbounded_item_query");
            }
        }
        if (outputRule.RecalculateOnCollect)
        {
            reasons.Add("output_recalculates_on_collect");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool MachineOutputItemQueryIsBounded(MachineItemOutput output)
    {
        var itemId = output.ItemId ?? string.Empty;
        return string.Equals(itemId, "DROP_IN", StringComparison.Ordinal) ||
            itemId.StartsWith("(", StringComparison.Ordinal) ||
            itemId.StartsWith("FLAVORED_ITEM ", StringComparison.Ordinal);
    }

    private sealed record MachineRuntimeMethod(string Name, MethodInfo? Method);
}
