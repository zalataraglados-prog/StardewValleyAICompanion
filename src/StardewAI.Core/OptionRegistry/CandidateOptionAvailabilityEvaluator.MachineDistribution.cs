using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class
    CandidateOptionAvailabilityEvaluator
{
    private static MachinePredictionTrainingContract
        ReadMachinePredictionTrainingContract(
            JsonElement input,
            string inputQualifiedItemId)
    {
        if (!input.TryGetProperty(
                "predicted_output",
                out var predictedOutput))
        {
            return MachinePredictionTrainingContract.Blocked;
        }

        return MachinePredictionTrainingPolicy.ReadContract(
            predictedOutput,
            inputQualifiedItemId);
    }

    private static AdditionalConsumedSummary?
        ReadAdditionalConsumedSummaryFromPrediction(
            JsonElement predictedOutput,
            IReadOnlyDictionary<string, int>
                inventoryStacks)
    {
        if (!predictedOutput.TryGetProperty(
                "consumed_additional_items",
                out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var total = 0;
        var consumed = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.EnumerateArray())
        {
            var qualifiedId = ReadString(
                item,
                "qualified_item_id");
            var requiredCount = ReadInt(
                item,
                "required_count");
            var unitSalePrice = ReadInt(
                item,
                "unit_sale_price");
            if (string.IsNullOrWhiteSpace(qualifiedId) ||
                requiredCount <= 0 ||
                unitSalePrice <= 0)
            {
                return null;
            }

            total = checked(
                total +
                requiredCount * unitSalePrice);
            consumed[qualifiedId] =
                consumed.TryGetValue(
                    qualifiedId,
                    out var current)
                    ? checked(
                        current + requiredCount)
                    : requiredCount;
        }

        var consumedItems = string.Join(
            ",",
            consumed
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal)
                .Select(pair =>
                    pair.Key + ":" +
                    pair.Value));
        var availableItems = string.Join(
            ",",
            consumed
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal)
                .Select(pair =>
                {
                    inventoryStacks.TryGetValue(
                        pair.Key,
                        out var available);
                    return pair.Key + ":" +
                        available;
                }));
        return new AdditionalConsumedSummary(
            total,
            consumedItems,
            availableItems);
    }
}
