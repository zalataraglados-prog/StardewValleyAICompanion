using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    private static bool PreparedTransferCovers(StrategyCommitmentLedger ledger,
        ReservationPortfolioSupportingTransitionSettlementRequest request, string reservationId) =>
        request.PreparedMaterialTransferSha256 is not null && ledger.PreparedMaterialTransfer is { } prepared &&
        request.PreparedMaterialTransferSha256 == prepared.BindingSha256 &&
        prepared.ReservationProjection.ReservationTransitions.Any(row => row.ReservationId == reservationId);

    private static void RetainCustodyPositionTransitions(StrategyCommitmentLedger before, StrategyCommitmentLedger after,
        ReservationPortfolioSupportingTransitionSettlementRequest request, MaterialTransferReservationProjection projection,
        ICollection<string> errors)
    {
        var prepared = before.PreparedMaterialTransfer!;
        var affected = projection.ReservationTransitions.Select(row => row.ReservationId).ToHashSet(StringComparer.Ordinal);
        var selected = request.MaterialRelocations.Select(row => row.MaterialReservationId).ToHashSet(StringComparer.Ordinal);
        var custodyRows = before.MaterialReservations.Where(row => row.OutputCustody is not null && affected.Contains(row.ReservationId)).ToArray();
        foreach (var row in custodyRows)
        {
            if (!MaterialOutputCustodyContract.TryGetPositionBinding(row, before, out var previous))
            {
                errors.Add("output_custody_previous_position_unverifiable");
                continue;
            }
            var next = after.MaterialReservations.Single(value => value.ReservationId == row.ReservationId);
            var operation = selected.Contains(row.ReservationId)
                ? "material_reservation_relocated" : "material_reservation_rebound_after_native_transfer";
            var entries = after.History.Where(entry => entry.CommitmentId == row.ReservationId &&
                entry.LedgerRevision == after.Revision && entry.CommitmentRevision == next.Revision && entry.Operation == operation).ToArray();
            if (entries.Length != 1)
            {
                errors.Add("output_custody_position_history_missing");
                continue;
            }
            var transition = new MaterialOutputCustodyPositionTransition
            {
                InitialPositionSha256 = row.OutputCustody!.InitialPosition!.BindingSha256, PreviousPositionSha256 = previous,
                BeforeNodeId = row.NodeId, BeforeSlotIndex = row.SlotIndex, BeforeStateHash = row.SourceStateHash,
                BeforeReservationRevision = row.Revision, AfterNodeId = next.NodeId, AfterSlotIndex = next.SlotIndex,
                AfterStateHash = next.SourceStateHash, AfterReservationRevision = next.Revision,
                PreparedPortfolioId = prepared.PortfolioId, PreparedRouteSourceDecisionId = prepared.RouteSourceDecisionId,
                PreparedLedgerRevision = prepared.ExpectedLedgerRevision, PreparedSourceStateHash = prepared.SourceStateHash,
                PreparedTransferSha256 = request.PreparedMaterialTransferSha256!,
                SupportingTransitionReceiptSha256 = request.SupportingTransitionReceiptSha256
            };
            transition.BindingSha256 = MaterialOutputCustodyContract.ComputePositionTransitionSha256(after, entries[0], transition);
            entries[0].OutputCustodyPositionTransition = transition;
        }
        foreach (var row in after.MaterialReservations.Where(row => row.OutputCustody is not null && affected.Contains(row.ReservationId)))
            if (!MaterialOutputCustodyContract.TryGetPositionBinding(row, after, out _))
                errors.Add("output_custody_result_position_unverifiable");
    }
}
