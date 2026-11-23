using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

internal static class CommunityCenterShipmentProtection
{
    private const string Goal = "goal.world_progress.complete_community_center";
    private const string PurposePrefix = "community_center_bundle:";
    internal const string Reserved = "inventory_item_reserved_for_community_center_bundle";
    internal const string UnknownSupply = "community_center_bundle_material_supply_unavailable";

    internal static bool Applies(StrategyCommitmentLedger? ledger) =>
        ledger?.MaterialReservations.Any(row => IsBundleClaim(row, ledger.PlayerId)) == true;

    internal static int? FreeQuantity(
        SnapshotEnvelope? snapshot,
        StrategyCommitmentLedger? ledger,
        int? slotIndex,
        string itemId,
        out string? unavailableReason)
    {
        unavailableReason = null;
        if (ledger is null) return null;
        var claims = ledger.MaterialReservations.Where(row =>
            IsBundleClaim(row, ledger.PlayerId) && row.QualifiedItemId == itemId).ToArray();
        if (claims.Length == 0) return null;
        if (!slotIndex.HasValue || snapshot is null || snapshot.SaveId.Status != FieldStatus.Available ||
            snapshot.PlayerId.Status != FieldStatus.Available || snapshot.SaveId.Value != ledger.SaveId ||
            snapshot.PlayerId.Value != ledger.PlayerId || !MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            claims.Any(row => row.OwnerPlayerId != graph!.PlayerId))
        {
            unavailableReason = UnknownSupply;
            return 0;
        }

        var projection = new MaterialSupplyProjection().Project(graph!, ledger.MaterialReservations);
        var matches = projection.Slots.Where(row =>
            row.NodeId == "player:" + ledger.PlayerId && row.SlotIndex == slotIndex && row.QualifiedItemId == itemId).ToArray();
        if (projection.Status != "available" || matches.Length != 1)
        {
            unavailableReason = UnknownSupply;
            return 0;
        }
        return matches[0].AvailableQuantity;
    }

    internal static string? BlockReason(SnapshotEnvelope? snapshot, StrategyCommitmentLedger? ledger,
        int? slotIndex, string itemId, int? quantity)
    {
        var free = FreeQuantity(snapshot, ledger, slotIndex, itemId, out var unavailableReason);
        if (unavailableReason is not null || !free.HasValue) return unavailableReason;
        return quantity is > 0 && quantity <= free.Value ? null : Reserved;
    }

    private static bool IsBundleClaim(MaterialReservation row, string playerId) =>
        row.Status == StrategyCommitmentStatuses.Active && row.NodeId == "player:" + playerId &&
        row.GoalId == Goal && row.Purpose.StartsWith(PurposePrefix, StringComparison.Ordinal) &&
        row.Purpose.Length > PurposePrefix.Length;
}
