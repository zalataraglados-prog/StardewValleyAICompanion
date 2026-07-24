using System;
using System.Linq;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Infrastructure;

internal sealed class MachinePlacementMaterialReservationGuard
{
    internal MachinePlacementMaterialReservationGuardResult Evaluate(
        StrategyCommitmentLedger? ledger,
        int slotIndex,
        string qualifiedItemId)
    {
        if (ledger is null || string.IsNullOrWhiteSpace(ledger.LedgerId))
        {
            return new MachinePlacementMaterialReservationGuardResult(
                false,
                "unavailable_strategy_ledger",
                string.Empty,
                -1,
                Array.Empty<string>());
        }

        var reservationIds = ledger.MaterialReservations
            .Where(reservation =>
                string.Equals(
                    reservation.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal) &&
                reservation.NodeId.StartsWith(
                    "player:",
                    StringComparison.Ordinal) &&
                reservation.SlotIndex == slotIndex &&
                string.Equals(
                    reservation.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase))
            .Select(reservation => reservation.ReservationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return new MachinePlacementMaterialReservationGuardResult(
            reservationIds.Length == 0,
            reservationIds.Length == 0
                ? "ready_no_active_material_reservations"
                : "blocked_inventory_item_reserved",
            ledger.LedgerId,
            ledger.Revision,
            reservationIds);
    }
}

internal sealed record MachinePlacementMaterialReservationGuardResult(
    bool Ready,
    string Status,
    string LedgerId,
    int LedgerRevision,
    string[] ReservationIds);
