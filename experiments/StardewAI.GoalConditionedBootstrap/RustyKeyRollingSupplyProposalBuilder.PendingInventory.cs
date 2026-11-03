using System.Globalization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyRollingSupplyProposalBuilder
{
    private static string[] ReadPendingDonations(
        RustyKeyAcquisitionPortfolioBuilder.RustyKeySourceScope source, SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger, string goalId, List<string> reasons)
    {
        if (!MaterialInventoryGraphReader.TryRead(snapshot, out var graph) || graph is null ||
            graph.PlayerId.ToString(CultureInfo.InvariantCulture) != ledger.PlayerId ||
            ledger.MaterialReservations is null || ledger.MaterialReservations.Any(row => row is null))
        {
            reasons.Add("rusty_key_pending_supply_inventory_unavailable_or_wrong_actor");
            return Array.Empty<string>();
        }
        var active = ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active).ToArray();
        var projection = new MaterialSupplyProjection().Project(graph, active);
        if (projection.Status != "available")
        {
            reasons.AddRange(projection.BlockingReasons);
            return Array.Empty<string>();
        }
        // Reuse the shared reservation projection; only this exact consumer's authenticated custody is added back.
        var owned = projection.Slots.Where(slot => slot.AvailableQuantity > 0 || active.Any(row =>
                row.NodeId == slot.NodeId && row.SlotIndex == slot.SlotIndex && row.QualifiedItemId == slot.QualifiedItemId &&
                MaterialOutputCustodyContract.IsAuthorized(row, ledger, MaterialOutputCustodyContract.MuseumConsumer, goalId)))
            .Select(slot => slot.QualifiedItemId).ToHashSet(StringComparer.Ordinal);
        return source.Museum.Groups.Where(group => !source.Progress.Requirements[group.RequirementId].Completed &&
                owned.Contains(group.Alternatives[0].QualifiedItemId))
            .Select(group => group.RequirementId).Order(StringComparer.Ordinal).ToArray();
    }
}
