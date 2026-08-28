using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static MachineOutputPrediction? PredictMachineOutputFromProbe(
            JsonElement input,
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            int inputSalePrice,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (!input.TryGetProperty("predicted_output", out var predictedOutput) ||
                predictedOutput.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var status = ReadString(predictedOutput, "status");
            if (!string.Equals(status, "available", StringComparison.OrdinalIgnoreCase))
            {
                var reason = ReadString(predictedOutput, "reason");
                return string.IsNullOrWhiteSpace(reason)
                    ? null
                    : MachineOutputPrediction.Unavailable("machine_native_probe_" + SanitizeStatus(reason));
            }

            var trainingContract =
                MachinePredictionTrainingPolicy.ReadContract(
                    predictedOutput,
                    qualifiedItemId);
            var hasOutputItem =
                predictedOutput.TryGetProperty(
                    "item",
                    out var outputItem) &&
                outputItem.ValueKind ==
                    JsonValueKind.Object;
            if (!hasOutputItem &&
                trainingContract.Kind ==
                    "complete_distribution")
            {
                hasOutputItem =
                    predictedOutput.TryGetProperty(
                        "output_identity",
                        out outputItem) &&
                    outputItem.ValueKind ==
                        JsonValueKind.Object;
            }
            if (!hasOutputItem)
            {
                return MachineOutputPrediction.Unavailable("machine_native_probe_output_item_unavailable");
            }

            var matchedRuleId = ReadString(predictedOutput, "matched_rule_id");
            var additionalConsumed =
                trainingContract.Kind ==
                    "complete_distribution"
                    ? ReadAdditionalConsumedSummaryFromPrediction(
                        predictedOutput,
                        inventoryStacks)
                    : ReadAdditionalConsumedSummaryForRequiredItem(
                        machineData,
                        qualifiedItemId,
                        itemId,
                        matchedRuleId,
                        inventoryStacks);
            if (!additionalConsumed.HasValue)
            {
                return MachineOutputPrediction.Unavailable("machine_native_probe_additional_consumption_unpriced");
            }

            var outputQualifiedId = ReadString(outputItem, "qualified_item_id");
            var outputItemId = ReadString(outputItem, "item_id");
            var outputContextTags = ReadStringArray(
                predictedOutput,
                "output_context_tags");
            var outputStack = ReadInt(
                predictedOutput,
                "stack");
            if (outputStack <= 0)
            {
                outputStack = Math.Max(1, ReadInt(outputItem, "stack"));
            }

            var outputSalePrice = Math.Max(0, ReadInt(predictedOutput, "sale_price"));
            if (outputSalePrice <= 0)
            {
                outputSalePrice = Math.Max(0, ReadInt(outputItem, "sale_price"));
            }

            var totalValue = outputSalePrice * Math.Max(1, outputStack);
            var additionalValue = additionalConsumed.Value.TotalValue;
            var netValue = totalValue - inputSalePrice - additionalValue;
            var suffix = string.Empty;
            if (!string.IsNullOrWhiteSpace(outputQualifiedId))
            {
                suffix += ";predicted_output_qualified_item_id=" + outputQualifiedId;
            }
            if (!string.IsNullOrWhiteSpace(outputItemId))
            {
                suffix += ";predicted_output_item_id=" + outputItemId;
            }
            suffix += ";predicted_output_context_tags_json=" +
                JsonSerializer.Serialize(outputContextTags) +
                ";predicted_output_additional_consumed_item_count=" +
                ReadInt(
                    predictedOutput,
                    "additional_consumed_item_count",
                    -1);

            suffix += ";predicted_output_stack=" + Math.Max(1, outputStack) +
                ";predicted_output_sale_price=" + outputSalePrice +
                ";predicted_output_price_source=" +
                (trainingContract.Kind ==
                    "complete_distribution"
                    ? "distribution_output_identity_sale_price"
                    : "machine_native_probe_sale_price") +
                ";predicted_output_total_value=" + totalValue +
                ";machine_additional_consumed_total_value=" + additionalValue +
                ";predicted_output_net_value=" + netValue;
            if (trainingContract.Kind ==
                "complete_distribution")
            {
                var utility =
                    AnvilReforgeUtilityProjection.Read(
                        predictedOutput);
                if (!utility.Supported)
                {
                    return MachineOutputPrediction
                        .Unavailable(
                            "machine_distribution_utility_unavailable");
                }
                suffix +=
                    ";anvil_reforge_utility_status=" +
                    utility.Status +
                    ";anvil_reforge_utility_metric=" +
                    utility.MetricId +
                    ";anvil_reforge_utility_ordering=" +
                    utility.Ordering +
                    ";anvil_reforge_current_utility=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.CurrentUtility) +
                    ";anvil_reforge_expected_utility=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.ExpectedUtility) +
                    ";anvil_reforge_expected_utility_delta=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.ExpectedDelta) +
                    ";anvil_reforge_improvement_probability=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.ImprovementProbability) +
                    ";anvil_reforge_equal_probability=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.EqualProbability) +
                    ";anvil_reforge_degradation_probability=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.DegradationProbability) +
                    ";anvil_reforge_decision_class=" +
                    utility.DecisionClass;
            }
            var requiredItemId = ReadString(predictedOutput, "required_item_id");
            if (!string.IsNullOrWhiteSpace(requiredItemId))
            {
                suffix += ";predicted_output_rule_required_item_id=" + requiredItemId;
            }
            if (!string.IsNullOrWhiteSpace(matchedRuleId))
            {
                suffix += ";predicted_output_rule_id=" + matchedRuleId;
            }
            var preserveType = ReadString(predictedOutput, "preserve_type");
            if (!string.IsNullOrWhiteSpace(preserveType))
            {
                suffix += ";predicted_output_preserve_type=" + preserveType;
            }
            var preservedItemId = ReadString(predictedOutput, "preserved_item_id");
            if (!string.IsNullOrWhiteSpace(preservedItemId))
            {
                suffix += ";predicted_output_preserved_item_id=" + preservedItemId;
            }
            if (!string.IsNullOrWhiteSpace(additionalConsumed.Value.ConsumedItems))
            {
                suffix += ";machine_additional_consumed_items=" + additionalConsumed.Value.ConsumedItems +
                    ";machine_additional_consumed_available=" + additionalConsumed.Value.AvailableItems;
            }
            var minutesUntilReady = ReadInt(predictedOutput, "effective_minutes_until_ready");
            if (minutesUntilReady <= 0)
            {
                minutesUntilReady = ReadInt(predictedOutput, "override_minutes_until_ready");
            }
            if (minutesUntilReady <= 0)
            {
                minutesUntilReady = ReadInt(predictedOutput, "rule_minutes_until_ready");
            }
            if (minutesUntilReady > 0)
            {
                suffix += ";predicted_minutes_until_ready=" + minutesUntilReady;
            }
            var daysUntilReady = ReadInt(
                predictedOutput,
                "effective_days_until_ready");
            if (daysUntilReady > 0)
            {
                suffix += ";predicted_days_until_ready=" +
                    daysUntilReady;
            }
            var daysToNextQuality = ReadInt(
                predictedOutput,
                "effective_days_to_next_quality");
            if (daysToNextQuality > 0)
            {
                suffix += ";predicted_days_to_next_quality=" +
                    daysToNextQuality;
            }
            var specialModelId = ReadString(
                predictedOutput,
                "special_prediction_model_id");
            if (!string.IsNullOrWhiteSpace(specialModelId))
            {
                suffix += ";machine_special_prediction_model_id=" +
                    specialModelId;
            }
            if (string.Equals(
                    specialModelId,
                    "incubator_animal_hatch.v1",
                    StringComparison.Ordinal))
            {
                suffix +=
                    ";incubator_hatch_animal_type_id=" +
                    ReadString(
                        predictedOutput,
                        "hatch_animal_type_id") +
                    ";incubator_suggested_hatch_name=" +
                    ReadString(
                        predictedOutput,
                        "suggested_hatch_name") +
                    ";incubator_unreserved_hatch_slot_count=" +
                    ReadInt(
                        predictedOutput,
                        "unreserved_hatch_slot_count") +
                    ";incubator_animal_house_occupant_count=" +
                    ReadInt(
                        predictedOutput,
                        "animal_house_occupant_count") +
                    ";incubator_animal_house_occupant_limit=" +
                    ReadInt(
                        predictedOutput,
                        "animal_house_occupant_limit") +
                    ";incubator_animal_purchase_equivalent_value=" +
                    ReadInt(
                        predictedOutput,
                        "animal_purchase_equivalent_value");
            }
            var initialQuality = ReadInt(
                predictedOutput,
                "initial_quality");
            var projectedFinalQuality = ReadInt(
                predictedOutput,
                "projected_final_quality");
            if (projectedFinalQuality > 0)
            {
                suffix += ";predicted_initial_quality=" +
                    initialQuality +
                    ";predicted_final_quality=" +
                    projectedFinalQuality;
            }
            var agingRate = ReadDouble(
                predictedOutput,
                "aging_rate_per_day");
            if (agingRate > 0)
            {
                suffix += ";predicted_aging_rate_per_day=" +
                    agingRate.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
            }

            return new MachineOutputPrediction(
                trainingContract.Kind ==
                    "complete_distribution"
                    ? "machine_distribution_probe_available"
                    : "machine_native_probe_available",
                additionalValue > 0
                    ? trainingContract.Kind ==
                        "complete_distribution"
                        ? "distribution_identity_value_minus_transparent_input_and_additional_consumed_sale_price"
                        : "machine_native_probe_total_value_minus_transparent_input_and_additional_consumed_sale_price"
                    : "machine_native_probe_total_value_minus_transparent_input_sale_price",
                suffix,
                outputQualifiedId,
                outputItemId,
                outputContextTags,
                ReadInt(
                    predictedOutput,
                    "additional_consumed_item_count",
                    -1));
        }

        private static MachineOutputPrediction PredictMachineOutputFromSummary(
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            int inputSalePrice,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (machineData.ValueKind != JsonValueKind.Object ||
                !machineData.TryGetProperty("output_rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return MachineOutputPrediction.Unavailable("machine_data_summary_unavailable");
            }

            var normalizedQualified = NormalizeObjectQualifiedId(qualifiedItemId, itemId);
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requiredItemId = ReadString(rule, "required_item_id");
                if (!MachineRuleRequiredItemMatches(requiredItemId, normalizedQualified, itemId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ReadString(rule, "condition")) ||
                    !string.IsNullOrWhiteSpace(ReadString(rule, "per_item_condition")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_condition_not_evaluated");
                }

                var additionalSource = ReadInt(machineData, "additional_consumed_item_count") > 0
                    ? machineData
                    : rule;
                var additionalConsumed = ReadAdditionalConsumedSummary(additionalSource, inventoryStacks);
                if (ReadInt(additionalSource, "additional_consumed_item_count") > 0 && !additionalConsumed.HasValue)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_additional_consumption_unpriced");
                }

                if (!rule.TryGetProperty("output_item", out var outputItem) || outputItem.ValueKind != JsonValueKind.Object)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_without_output_item");
                }

                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "condition")) ||
                    !string.IsNullOrWhiteSpace(ReadString(outputItem, "per_item_condition")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_output_condition_not_evaluated");
                }
                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "output_method")) ||
                    (outputItem.TryGetProperty("random_item_ids", out var randomIds) &&
                     randomIds.ValueKind == JsonValueKind.Array && randomIds.GetArrayLength() > 0))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_dynamic_output_not_priced");
                }

                var copyPrice = ReadBool(outputItem, "copy_price") == true;
                if (ReadBool(outputItem, "copy_quality") == true)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_copy_quality_not_priced");
                }

                if (ReadBool(outputItem, "copy_color") == true)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_copy_color_not_priced");
                }

                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "preserve_type")) ||
                    !string.IsNullOrWhiteSpace(ReadString(outputItem, "preserve_id")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_preserve_not_priced");
                }

                var outputQualifiedId = ReadString(outputItem, "qualified_item_id");
                var outputItemId = ReadString(outputItem, "item_id");
                var outputStack = ReadInt(outputItem, "stack");
                if (outputStack <= 0)
                {
                    outputStack = Math.Max(1, ReadInt(outputItem, "min_stack"));
                }

                var outputSalePrice = copyPrice ? inputSalePrice : Math.Max(0, ReadInt(outputItem, "sale_price"));
                var totalValue = outputSalePrice * Math.Max(1, outputStack);
                var additionalValue = additionalConsumed.HasValue ? additionalConsumed.Value.TotalValue : 0;
                var netValue = totalValue - inputSalePrice - additionalValue;
                var suffix = string.Empty;
                if (!string.IsNullOrWhiteSpace(outputQualifiedId))
                {
                    suffix += ";predicted_output_qualified_item_id=" + outputQualifiedId;
                }
                if (!string.IsNullOrWhiteSpace(outputItemId))
                {
                    suffix += ";predicted_output_item_id=" + outputItemId;
                }

                suffix += ";predicted_output_stack=" + Math.Max(1, outputStack) +
                    ";predicted_output_sale_price=" + outputSalePrice +
                    ";predicted_output_price_source=" + (copyPrice ? "copy_price_from_transparent_input_sale_price" : "output_item_sale_price") +
                    ";predicted_output_total_value=" + totalValue +
                    ";machine_additional_consumed_total_value=" + additionalValue +
                    ";predicted_output_net_value=" + netValue +
                    ";predicted_output_rule_required_item_id=" + requiredItemId;
                if (additionalConsumed.HasValue && !string.IsNullOrWhiteSpace(additionalConsumed.Value.ConsumedItems))
                {
                    suffix += ";machine_additional_consumed_items=" + additionalConsumed.Value.ConsumedItems +
                        ";machine_additional_consumed_available=" + additionalConsumed.Value.AvailableItems;
                }

                var minutesUntilReady = ReadInt(rule, "minutes_until_ready");
                if (minutesUntilReady <= 0 && ReadInt(rule, "days_until_ready") > 0)
                {
                    minutesUntilReady = ReadInt(rule, "days_until_ready") * 1600;
                }
                if (minutesUntilReady > 0)
                {
                    suffix += ";predicted_minutes_until_ready=" + minutesUntilReady;
                }

                return new MachineOutputPrediction(
                    "machine_data_exact_required_item_match",
                    additionalValue > 0
                        ? "predicted_output_total_value_minus_transparent_input_and_additional_consumed_sale_price"
                        : "predicted_output_total_value_minus_transparent_input_sale_price",
                    suffix,
                    outputQualifiedId,
                    outputItemId,
                    Array.Empty<string>(),
                    -1);
            }

            return MachineOutputPrediction.Unavailable("machine_data_no_exact_required_item_match");
        }

        private static AdditionalConsumedSummary? ReadAdditionalConsumedSummary(JsonElement rule, IReadOnlyDictionary<string, int> inventoryStacks)
        {
            var count = ReadInt(rule, "additional_consumed_item_count");
            if (count <= 0)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }

            if (!rule.TryGetProperty("additional_consumed_items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var pricedCount = 0;
            var total = 0;
            var consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var amount = Math.Max(1, ReadInt(item, "amount"));
                var salePrice = ReadInt(item, "sale_price");
                if (salePrice <= 0)
                {
                    return null;
                }

                var qualifiedId = NormalizeObjectQualifiedId(ReadString(item, "qualified_item_id"), ReadString(item, "item_id"));
                if (string.IsNullOrWhiteSpace(qualifiedId))
                {
                    return null;
                }

                total += amount * salePrice;
                consumed[qualifiedId] = consumed.TryGetValue(qualifiedId, out var current) ? current + amount : amount;
                pricedCount++;
            }

            if (pricedCount != count)
            {
                return null;
            }

            var consumedItems = string.Join(",", consumed
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value));
            var availableItems = string.Join(",", consumed
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                {
                    inventoryStacks.TryGetValue(pair.Key, out var available);
                    return pair.Key + ":" + available;
                }));
            return new AdditionalConsumedSummary(total, consumedItems, availableItems);
        }

        private static AdditionalConsumedSummary? ReadAdditionalConsumedSummaryForRequiredItem(
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            string matchedRuleId,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (machineData.ValueKind != JsonValueKind.Object)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }
            if (ReadInt(machineData, "additional_consumed_item_count") > 0)
            {
                return ReadAdditionalConsumedSummary(machineData, inventoryStacks);
            }
            if (
                !machineData.TryGetProperty("output_rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }

            var normalizedQualified = NormalizeObjectQualifiedId(qualifiedItemId, itemId);
            if (!string.IsNullOrWhiteSpace(matchedRuleId))
            {
                foreach (var rule in rules.EnumerateArray())
                {
                    if (rule.ValueKind == JsonValueKind.Object &&
                        string.Equals(ReadString(rule, "id"), matchedRuleId, StringComparison.Ordinal))
                    {
                        return ReadAdditionalConsumedSummary(rule, inventoryStacks);
                    }
                }
            }

            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requiredItemId = ReadString(rule, "required_item_id");
                if (MachineRuleRequiredItemMatches(requiredItemId, normalizedQualified, itemId))
                {
                    return ReadAdditionalConsumedSummary(rule, inventoryStacks);
                }
            }

            return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
        }

        private static bool MachineRuleRequiredItemMatches(string requiredItemId, string normalizedQualifiedItemId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(requiredItemId))
            {
                return false;
            }

            return string.Equals(requiredItemId, normalizedQualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requiredItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeObjectQualifiedId(requiredItemId, requiredItemId), normalizedQualifiedItemId, StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeStatus(string value)
        {
            var chars = value
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
                .ToArray();
            var status = new string(chars).Trim('_');
            while (status.Contains("__", StringComparison.Ordinal))
            {
                status = status.Replace("__", "_", StringComparison.Ordinal);
            }

            return string.IsNullOrWhiteSpace(status) ? "unavailable" : status;
        }

        private static string NormalizeObjectQualifiedId(string qualifiedItemId, string itemId)
        {
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return qualifiedItemId;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return string.Empty;
            }

            return itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId;
        }

        private readonly struct MachineOutputPrediction
        {
            public MachineOutputPrediction(
                string status,
                string valueBasis,
                string expectedEffectSuffix,
                string outputQualifiedItemId,
                string outputItemId,
                string[] outputContextTags,
                int additionalConsumedItemCount)
            {
                Status = status;
                ValueBasis = valueBasis;
                ExpectedEffectSuffix = expectedEffectSuffix;
                OutputQualifiedItemId = outputQualifiedItemId;
                OutputItemId = outputItemId;
                OutputContextTags = outputContextTags;
                AdditionalConsumedItemCount = additionalConsumedItemCount;
            }

            public string Status { get; }

            public string ValueBasis { get; }

            public string ExpectedEffectSuffix { get; }

            public string OutputQualifiedItemId { get; }

            public string OutputItemId { get; }

            public string[] OutputContextTags { get; }

            public int AdditionalConsumedItemCount { get; }

            public static MachineOutputPrediction Unavailable(string status)
            {
                return new MachineOutputPrediction(
                    status,
                    "transparent_input_sale_price_output_unknown",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Array.Empty<string>(),
                    -1);
            }
        }

        private readonly struct AdditionalConsumedSummary
        {
            public AdditionalConsumedSummary(int totalValue, string consumedItems, string availableItems)
            {
                TotalValue = totalValue;
                ConsumedItems = consumedItems;
                AvailableItems = availableItems;
            }

            public int TotalValue { get; }

            public string ConsumedItems { get; }

            public string AvailableItems { get; }
        }

        private static (int X, int Y)? FirstDebrisChunkTile(JsonElement debris)
        {
            if (!debris.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind == JsonValueKind.Object)
                {
                    return (ReadInt(chunk, "tile_x"), ReadInt(chunk, "tile_y"));
                }
            }

            return null;
        }

        private static bool InventoryMayAcceptItem(SnapshotEnvelope snapshot, string qualifiedItemId, string itemId, int quality)
        {
            var normalizedQualifiedId = !string.IsNullOrWhiteSpace(qualifiedItemId)
                ? qualifiedItemId
                : string.IsNullOrWhiteSpace(itemId)
                    ? string.Empty
                    : itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
            if (string.IsNullOrWhiteSpace(normalizedQualifiedId))
            {
                return false;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), normalizedQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
