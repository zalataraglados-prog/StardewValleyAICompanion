using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static class ExactCraftingMaterialConsumptionReader
{
    public static ExactCraftingMaterialConsumption[] Read(
        JsonElement ingredientRows,
        bool usesWorkbench,
        long playerId)
    {
        var rows = new List<ExactCraftingMaterialConsumption>();
        if (ingredientRows.ValueKind != JsonValueKind.Array)
            return Array.Empty<ExactCraftingMaterialConsumption>();

        var planProperty = usesWorkbench
            ? "native_consumption_plan"
            : "reverse_slot_consumption_plan";
        foreach (var ingredient in ingredientRows.EnumerateArray())
        {
            if (ingredient.ValueKind != JsonValueKind.Object ||
                !ingredient.TryGetProperty(planProperty, out var plan) ||
                plan.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in plan.EnumerateArray())
            {
                var nodeId = usesWorkbench
                    ? ReadString(entry, "source_node_id")
                    : "player:" + playerId;
                var slotIndex = NullableReadInt(entry, "slot_index");
                var qualifiedItemId = ReadString(
                    entry,
                    "qualified_item_id");
                var amount = ReadInt(entry, "amount");
                if (!string.IsNullOrWhiteSpace(nodeId) &&
                    slotIndex.HasValue &&
                    !string.IsNullOrWhiteSpace(qualifiedItemId) &&
                    amount > 0)
                {
                    rows.Add(new ExactCraftingMaterialConsumption(
                        nodeId,
                        slotIndex.Value,
                        qualifiedItemId,
                        amount));
                }
            }
        }

        return rows
            .GroupBy(
                row => row.NodeId + "\n" + row.SlotIndex + "\n" +
                    row.QualifiedItemId,
                StringComparer.Ordinal)
            .Select(group => new ExactCraftingMaterialConsumption(
                group.First().NodeId,
                group.First().SlotIndex,
                group.First().QualifiedItemId,
                group.Sum(row => row.Quantity)))
            .OrderBy(row => row.NodeId, StringComparer.Ordinal)
            .ThenBy(row => row.SlotIndex)
            .ThenBy(row => row.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed record ExactCraftingMaterialConsumption(
    string NodeId,
    int SlotIndex,
    string QualifiedItemId,
    int Quantity);

public sealed record MaterialReservationConsumptionAuthorization(
    string GoalId,
    string SourceDecisionId,
    string Purpose)
{
    public bool Matches(
        StardewAI.Contracts.Strategy.MaterialReservation reservation) =>
        string.Equals(
            reservation.GoalId,
            GoalId,
            StringComparison.Ordinal) &&
        string.Equals(
            reservation.SourceDecisionId,
            SourceDecisionId,
            StringComparison.Ordinal) &&
        string.Equals(
            reservation.Purpose,
            Purpose,
            StringComparison.Ordinal);
}
