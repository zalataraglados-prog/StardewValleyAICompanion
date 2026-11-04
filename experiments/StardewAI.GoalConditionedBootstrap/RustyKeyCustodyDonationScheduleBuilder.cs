using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static class RustyKeyCustodyDonationScheduleBuilder
{
    internal static RustyKeyCustodyDonationSchedule Build(string goalId, SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        MaterialInventoryGraph graph, RustyKeyRollingCustodySelection selection, PolicyEventCandidatePrediction candidate,
        Lazy<MuseumDonationRouteScheduleBuilder> scheduler)
    {
        var reasons = new List<string>();
        var rows = ledger.MaterialReservations.Where(row => row.ReservationId == selection.ReservationId).ToArray();
        var custody = rows.Length == 1 ? rows[0] : null;
        var accessRows = custody is null ? Array.Empty<MaterialInventoryAccessPoint>() :
            graph.AccessPoints.Where(row => row.NodeId == custody.NodeId).ToArray();
        var access = accessRows.Length == 1 ? accessRows[0] : null;
        if (custody is null || !MaterialOutputCustodyContract.IsAuthorized(custody, ledger, MaterialOutputCustodyContract.MuseumConsumer, goalId) ||
            !MaterialOutputCustodyContract.TryGetPositionBinding(custody, ledger, out var position) || position != selection.PositionSha256 ||
            candidate.QualifiedItemId != custody.QualifiedItemId || candidate.Quantity != custody.Quantity || custody.Quantity <= 0 ||
            access is null || !access.ActorUseAuthorized || !access.TileX.HasValue || !access.TileY.HasValue ||
            string.IsNullOrWhiteSpace(access.LocationId))
            reasons.Add("rusty_key_custody_schedule_source_binding_invalid");
        bool HasUniqueParameter(string name, string value) => candidate.Parameters.Count(row => row.Name == name) == 1 &&
            candidate.Parameters.Any(row => row.Name == name && row.Value == value);
        if (custody is not null)
        {
            if (selection.Kind == "custody_transfer")
            {
                if (candidate.Kind != "transfer_inventory_item" || !MaterialTransferIntentBinder.TryBuildIntent(candidate.Parameters, out var intent) || intent is null ||
                    intent.SourceNodeId != custody.NodeId || intent.SourceSlotIndex != custody.SlotIndex || intent.Quantity != custody.Quantity ||
                    intent.DestinationNodeId != selection.DestinationNodeId)
                    reasons.Add("rusty_key_custody_schedule_transfer_binding_invalid");
            }
            else if (selection.Kind != "custody_navigation" || !HasUniqueParameter("material_transfer.approach_only", "true") ||
                !HasUniqueParameter("material_transfer.target_node_id", custody.NodeId) || !HasUniqueParameter("material_transfer.source_node_id", custody.NodeId) ||
                !HasUniqueParameter("material_transfer.destination_node_id", selection.DestinationNodeId))
                reasons.Add("rusty_key_custody_schedule_navigation_binding_invalid");
        }
        var route = reasons.Count == 0 ? scheduler.Value.BuildWithdrawal(candidate, access!.LocationId,
            access.TileX!.Value, access.TileY!.Value, custody!.Quantity) : null;
        if (route is not null) reasons.AddRange(route.BlockingReasons);
        return new(selection.ReservationId, selection.PositionSha256, selection.DestinationNodeId, route,
            reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}
