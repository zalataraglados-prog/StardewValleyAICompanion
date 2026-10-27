using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed class MachineCraftingMaterialReservationGuard
{
    private readonly MaterialSupplyProjection projection = new();

    internal MachineCraftingMaterialReservationGuardResult Evaluate(
        SnapshotEnvelope snapshot,
        JsonElement ingredientRows,
        bool usesWorkbench,
        StrategyCommitmentLedger? ledger,
        MaterialReservationConsumptionAuthorization? authorization = null)
    {
        var activeReservations = (ledger?.MaterialReservations ?? Array.Empty<MaterialReservation>())
            .Where(row => string.Equals(
                row.Status,
                StrategyCommitmentStatuses.Active,
                StringComparison.Ordinal))
            .ToArray();
        if (activeReservations.Length == 0)
        {
            return Ready(ledger, "ready_no_active_material_reservations", Array.Empty<string>());
        }

        if (!TryReadGraph(snapshot, out var graph))
        {
            return Blocked(ledger, "material_inventory_graph_unavailable");
        }

        var plannedConsumption = ExactCraftingMaterialConsumptionReader.Read(
            ingredientRows,
            usesWorkbench,
            graph!.PlayerId);
        if (plannedConsumption.Length == 0)
        {
            return Blocked(ledger, "machine_recipe_material_consumption_plan_unavailable");
        }

        var authorizedReservations = authorization is null
            ? Array.Empty<MaterialReservation>()
            : activeReservations.Where(authorization.Matches).ToArray();
        var authorizedIds = authorizedReservations
            .Select(row => row.ReservationId)
            .ToHashSet(StringComparer.Ordinal);
        var competingReservations = activeReservations
            .Where(row => !authorizedIds.Contains(row.ReservationId))
            .ToArray();
        var supply = projection.Project(graph, competingReservations);
        if (!string.Equals(supply.Status, "available", StringComparison.Ordinal))
        {
            return Blocked(ledger, supply.BlockingReasons);
        }

        var relevantReservationIds = authorizedReservations
            .Where(reservation => plannedConsumption.Any(consumption =>
                string.Equals(consumption.NodeId, reservation.NodeId, StringComparison.Ordinal) &&
                consumption.SlotIndex == reservation.SlotIndex &&
                string.Equals(
                    consumption.QualifiedItemId,
                    reservation.QualifiedItemId,
                    StringComparison.Ordinal)))
            .Select(row => row.ReservationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var reasons = new List<string>();
        foreach (var consumption in plannedConsumption)
        {
            var slot = supply.Slots.SingleOrDefault(row =>
                string.Equals(row.NodeId, consumption.NodeId, StringComparison.Ordinal) &&
                row.SlotIndex == consumption.SlotIndex &&
                string.Equals(
                    row.QualifiedItemId,
                    consumption.QualifiedItemId,
                    StringComparison.Ordinal));
            if (slot is null || slot.AvailableQuantity < consumption.Quantity)
            {
                reasons.Add(
                    "machine_recipe_material_reserved_for_other_goal:" +
                    consumption.NodeId + "#" + consumption.SlotIndex);
            }
        }
        return reasons.Count == 0
            ? Ready(ledger, "ready", relevantReservationIds)
            : Blocked(ledger, reasons, relevantReservationIds);
    }

    private static bool TryReadGraph(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph)
    {
        graph = null;
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "farm", "material_inventory_graph")))
        {
            return false;
        }
        var value = ReadStateFieldValue(snapshot, "farm", "material_inventory_graph");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        try
        {
            graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(value.Value.GetRawText());
            return graph is not null &&
                string.Equals(
                    graph.SchemaVersion,
                    "material_inventory_graph.v1",
                    StringComparison.Ordinal) &&
                string.Equals(graph.Status, "available", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static MachineCraftingMaterialReservationGuardResult Ready(
        StrategyCommitmentLedger? ledger,
        string status,
        string[] reservationIds) => new()
        {
            Ready = true,
            Status = status,
            LedgerId = ledger?.LedgerId ?? string.Empty,
            LedgerRevision = ledger?.Revision ?? 0,
            ReservationIds = reservationIds
        };

    private static MachineCraftingMaterialReservationGuardResult Blocked(
        StrategyCommitmentLedger? ledger,
        params string[] reasons) =>
        Blocked(ledger, reasons, Array.Empty<string>());

    private static MachineCraftingMaterialReservationGuardResult Blocked(
        StrategyCommitmentLedger? ledger,
        IEnumerable<string> reasons,
        string[]? reservationIds = null) => new()
        {
            Status = "blocked",
            LedgerId = ledger?.LedgerId ?? string.Empty,
            LedgerRevision = ledger?.Revision ?? 0,
            ReservationIds = reservationIds ?? Array.Empty<string>(),
            BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };

}

internal sealed class MachineCraftingMaterialReservationGuardResult
{
    internal bool Ready { get; init; }

    internal string Status { get; init; } = string.Empty;

    internal string LedgerId { get; init; } = string.Empty;

    internal int LedgerRevision { get; init; }

    internal string[] ReservationIds { get; init; } = Array.Empty<string>();

    internal string[] BlockingReasons { get; init; } = Array.Empty<string>();
}
