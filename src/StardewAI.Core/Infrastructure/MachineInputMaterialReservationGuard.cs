using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed class MachineInputMaterialReservationGuard
{
    private readonly MaterialSupplyProjection projection = new();

    internal MachineInputMaterialReservationGuardResult Evaluate(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        int slotIndex,
        string qualifiedItemId,
        int requiredQuantity)
    {
        var activeReservations = ledger.MaterialReservations
            .Where(row => string.Equals(
                row.Status,
                StrategyCommitmentStatuses.Active,
                StringComparison.Ordinal))
            .ToArray();
        if (activeReservations.Length == 0)
        {
            return Ready(
                ledger,
                "ready_no_active_material_reservations",
                Array.Empty<string>());
        }

        if (!TryReadGraph(snapshot, out var graph))
        {
            return Blocked(
                ledger,
                "machine_input_material_inventory_graph_unavailable");
        }

        var playerNodeId = "player:" + graph!.PlayerId;
        var supply = projection.Project(graph, activeReservations);
        if (!string.Equals(supply.Status, "available", StringComparison.Ordinal))
        {
            return Blocked(ledger, supply.BlockingReasons);
        }

        var slot = supply.Slots.SingleOrDefault(row =>
            string.Equals(row.NodeId, playerNodeId, StringComparison.Ordinal) &&
            row.SlotIndex == slotIndex &&
            string.Equals(
                row.QualifiedItemId,
                qualifiedItemId,
                StringComparison.Ordinal));
        var reservationIds = activeReservations
            .Where(row =>
                string.Equals(row.NodeId, playerNodeId, StringComparison.Ordinal) &&
                row.SlotIndex == slotIndex &&
                string.Equals(
                    row.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.Ordinal))
            .Select(row => row.ReservationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (requiredQuantity <= 0 ||
            slot is null ||
            slot.AvailableQuantity < requiredQuantity)
        {
            return Blocked(
                ledger,
                new[] { "machine_input_reserved_for_other_goal" },
                reservationIds);
        }

        return Ready(ledger, "ready", reservationIds);
    }

    private static bool TryReadGraph(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph)
    {
        graph = null;
        if (!ReadableStatus(ReadStateFieldStatus(
                snapshot,
                "farm",
                "material_inventory_graph")))
        {
            return false;
        }

        var value = ReadStateFieldValue(
            snapshot,
            "farm",
            "material_inventory_graph");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.Value.GetRawText());
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

    private static MachineInputMaterialReservationGuardResult Ready(
        StrategyCommitmentLedger ledger,
        string status,
        string[] reservationIds) => new()
        {
            Ready = true,
            Status = status,
            LedgerId = ledger.LedgerId,
            LedgerRevision = ledger.Revision,
            ReservationIds = reservationIds
        };

    private static MachineInputMaterialReservationGuardResult Blocked(
        StrategyCommitmentLedger ledger,
        params string[] reasons) =>
        Blocked(ledger, reasons, Array.Empty<string>());

    private static MachineInputMaterialReservationGuardResult Blocked(
        StrategyCommitmentLedger ledger,
        string[] reasons,
        string[] reservationIds) => new()
        {
            Status = "blocked",
            LedgerId = ledger.LedgerId,
            LedgerRevision = ledger.Revision,
            ReservationIds = reservationIds,
            BlockingReasons = reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
}

internal sealed class MachineInputMaterialReservationGuardResult
{
    internal bool Ready { get; init; }

    internal string Status { get; init; } = string.Empty;

    internal string LedgerId { get; init; } = string.Empty;

    internal int LedgerRevision { get; init; }

    internal string[] ReservationIds { get; init; } = Array.Empty<string>();

    internal string[] BlockingReasons { get; init; } = Array.Empty<string>();
}
