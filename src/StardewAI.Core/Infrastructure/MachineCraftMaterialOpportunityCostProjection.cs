using System;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineCraftMaterialOpportunityCost(
    string Status,
    int TotalSaleValue);

internal static class MachineCraftMaterialOpportunityCostProjection
{
    public static MachineCraftMaterialOpportunityCost Evaluate(
        JsonElement ingredientRows,
        bool usesWorkbench)
    {
        if (ingredientRows.ValueKind != JsonValueKind.Array)
        {
            return new(
                "incomplete_ingredient_rows_unavailable",
                0);
        }

        var planProperty = usesWorkbench
            ? "native_consumption_plan"
            : "reverse_slot_consumption_plan";
        long total = 0;
        var consumedCount = 0;
        foreach (var ingredient in ingredientRows.EnumerateArray())
        {
            if (ingredient.ValueKind != JsonValueKind.Object ||
                !ingredient.TryGetProperty(
                    planProperty,
                    out var plan) ||
                plan.ValueKind != JsonValueKind.Array)
            {
                return new(
                    "incomplete_consumption_plan_unavailable",
                    0);
            }

            foreach (var consumed in plan.EnumerateArray())
            {
                if (consumed.ValueKind != JsonValueKind.Object ||
                    !consumed.TryGetProperty(
                        "total_sale_value",
                        out var value) ||
                    value.ValueKind != JsonValueKind.Number ||
                    !value.TryGetInt64(out var saleValue) ||
                    saleValue < 0)
                {
                    return new(
                        "incomplete_consumed_item_sale_value",
                        0);
                }

                total += saleValue;
                consumedCount++;
                if (total > int.MaxValue)
                {
                    return new(
                        "blocked_material_sale_value_overflow",
                        0);
                }
            }
        }

        return consumedCount == 0
            ? new(
                "incomplete_no_consumed_material_rows",
                0)
            : new(
                "complete_exact_native_consumption_sale_value",
                (int)total);
    }
}
