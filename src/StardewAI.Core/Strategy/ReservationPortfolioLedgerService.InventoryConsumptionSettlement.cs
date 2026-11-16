using System;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    private static ReservationPortfolioSupportingTransitionSettlementResult SettlePreparedInventoryConsumption(
        StrategyCommitmentLedger? current, SnapshotEnvelope snapshot,
        ReservationPortfolioSupportingTransitionSettlementRequest request, string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(current, snapshot, request.StateHash, request.ExpectedLedgerRevision);
        var prepared = current?.PreparedInventoryConsumption;
        if (current is null || prepared is null || !PreparedInventoryConsumptionContract.IsBound(current,
                request.PreparedInventoryConsumptionSha256, request.PortfolioId, request.GoalId, request.RouteSourceDecisionId) ||
            request.ConsumerOptionId != PreparedInventoryConsumptionContract.Consumer || string.IsNullOrWhiteSpace(request.Reason) ||
            !MaterialOutputCustodyContract.IsLowerSha256(request.SupportingTransitionReceiptSha256) ||
            prepared.SourceStateHash == snapshot.StateHash || snapshot.GameTick < prepared.SourceGameTick)
            errors.Add("prepared_inventory_consumption_binding_drifted");
        if (request.MaterialConsumptions is not { Length: 0 } || request.MaterialRelocations is not { Length: 0 } ||
            request.CurrencyConsumptions is not { Length: 0 } || request.RebindActiveReservationIds is not { Length: 0 } ||
            !string.IsNullOrEmpty(request.MaterialReservationId) || !string.IsNullOrEmpty(request.NodeId) || request.SlotIndex != 0 ||
            !string.IsNullOrEmpty(request.QualifiedItemId) || request.ConsumedQuantity != 0 ||
            !string.IsNullOrEmpty(request.MachineSupportIntentId) || !string.IsNullOrEmpty(request.MachineSupportIntentStage) ||
            request.MachineSupportSourcesJson != "[]" || request.PreparedMaterialTransferSha256 is not null)
            errors.Add("prepared_inventory_consumption_mixed_mutation");
        if (!MaterialInventoryGraphReader.TryRead(snapshot, out var graph))
            errors.Add("prepared_inventory_consumption_after_graph_unavailable");
        if (errors.Count == 0) errors.AddRange(InventoryConsumptionProjector.VerifyAfter(prepared!, graph!));
        ReservationPortfolioSupportingTransitionSettlementResult Reject() => SupportingTransitionSettlementRejected(current, request,
            Array.Empty<ReservationPortfolioMaterialConsumption>(), Array.Empty<ReservationPortfolioMaterialRelocation>(),
            Array.Empty<ReservationPortfolioCurrencyConsumption>(), Array.Empty<string>(), errors);
        if (errors.Count > 0) return Reject();

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(current, snapshot, updatedAt);
        var byId = prepared!.Projection.PassiveReservationTransitions.ToDictionary(row => row.ReservationId, StringComparer.Ordinal);
        ledger.MaterialReservations = ledger.MaterialReservations.Select(row => byId.TryGetValue(row.ReservationId, out var position)
            ? RebindNativePosition(row, position, snapshot.StateHash) : row).ToArray();
        errors.AddRange(new MaterialSupplyProjection().Project(graph!, ledger.MaterialReservations).BlockingReasons);
        if (errors.Count > 0) return Reject();
        ledger.PreparedInventoryConsumption = null;
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        foreach (var position in byId.Values)
        {
            var row = ledger.MaterialReservations.Single(value => value.ReservationId == position.ReservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(ledger, row.ReservationId, row.Revision, row.SourceDecisionId,
                "material_reservation_rebound_after_native_consumption", updatedAt, prepared.BindingSha256);
        }
        StrategyCommitmentLedgerSupport.AppendHistory(ledger, request.PortfolioId, ledger.Revision, request.RouteSourceDecisionId,
            "inventory_consumption_preparation_completed", updatedAt, prepared.BindingSha256);
        StrategyCommitmentLedgerSupport.AppendHistory(ledger, request.PortfolioId, ledger.Revision, request.RouteSourceDecisionId,
            "reservation_portfolio_supporting_transition_complete", updatedAt, request.SupportingTransitionReceiptSha256);
        return new ReservationPortfolioSupportingTransitionSettlementResult
        {
            Accepted = true, PortfolioId = request.PortfolioId, RouteSourceDecisionId = request.RouteSourceDecisionId,
            SupportingTransitionReceiptSha256 = request.SupportingTransitionReceiptSha256, CommittedLedgerRevision = ledger.Revision,
            ReboundActiveReservationIds = byId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(), Ledger = ledger
        };
    }

    private static MaterialReservation RebindNativePosition(MaterialReservation row, MaterialTransferReservationTransition change, string stateHash)
    {
        var rebound = StrategyCommitmentLedgerSupport.CloneMaterial(row);
        rebound.Revision++;
        rebound.SourceStateHash = stateHash;
        rebound.NodeId = change.DestinationNodeId;
        rebound.SlotIndex = change.DestinationSlotIndex;
        return rebound;
    }
}
