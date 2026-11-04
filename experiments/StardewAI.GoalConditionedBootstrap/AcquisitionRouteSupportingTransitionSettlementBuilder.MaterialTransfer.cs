using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionSettlementBuilder
{
    private static void ValidateTransferCompilation(AcquisitionRouteSupportingTransitionRequest request,
        AcquisitionRouteSupportingTransitionCommitReceipt commit, AcquisitionRouteDispatchCompilation compilation,
        StrategyCommitmentLedger ledger)
    {
        var reasons = new List<string>();
        AcquisitionRouteSupportingTransferBinding.Validate(request, ledger, commit.PreparedMaterialTransferSha256, reasons);
        Require(reasons.Count == 0, "Support settlement transfer preparation drifted: " + string.Join(",", reasons));
        if (request.SupportTransitionKind != "machine_input_material_transfer") return;
        var transfers = compilation.ActionQueue?.Items.Where(item =>
            item.OptionId is "executor.transfer_material" or "inventory.transfer_item").ToArray();
        Require(transfers is { Length: 1 } && new ActionQueueDispatchReadinessService().Evaluate(
                compilation.ActionQueue!, transfers[0], ledger, compilation.SourceStateHash).Ready,
            "Support settlement compiled transfer preparation binding is invalid.");
    }

    private static int ValidateTransferSettlementHistory(SettlementContext context,
        ReservationPortfolioSupportingTransitionSettlementRequest request, StrategyCommitmentLedger settled,
        ICollection<string> reasons)
    {
        if (request.PreparedMaterialTransferSha256 is null) return 0;
        var projection = context.BaseLedger.PreparedMaterialTransfer!.ReservationProjection;
        var selectedIds = request.MaterialRelocations.Select(row => row.MaterialReservationId).ToHashSet(StringComparer.Ordinal);
        var passive = projection.ReservationTransitions.Where(row => !selectedIds.Contains(row.ReservationId)).ToArray();
        var history = settled.History.Where(row => row.LedgerRevision == settled.Revision).ToArray();
        foreach (var change in projection.ReservationTransitions)
        {
            var before = context.BaseLedger.MaterialReservations.Single(row => row.ReservationId == change.ReservationId);
            var expected = System.Text.Json.JsonSerializer.Deserialize<MaterialReservation>(System.Text.Json.JsonSerializer.Serialize(before))!;
            expected.NodeId = change.DestinationNodeId;
            expected.SlotIndex = change.DestinationSlotIndex;
            expected.SourceStateHash = context.AfterSnapshot.StateHash;
            expected.Revision++;
            if (settled.MaterialReservations.Count(row => EqualJson(row, expected)) != 1)
                reasons.Add("support_settlement_transfer_map_claim_drifted:" + change.ReservationId);
        }
        if (passive.Any(change => history.Count(row => row.CommitmentId == change.ReservationId &&
                row.SourceDecisionId == context.BaseLedger.MaterialReservations.Single(value => value.ReservationId == change.ReservationId).SourceDecisionId &&
                row.Operation == "material_reservation_rebound_after_native_transfer" &&
                row.Reason == request.PreparedMaterialTransferSha256) != 1) ||
            history.Count(row => row.CommitmentId == request.PortfolioId && row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation == "material_transfer_preparation_completed" && row.Reason == request.PreparedMaterialTransferSha256) != 1)
            reasons.Add("support_settlement_transfer_map_history_drifted");
        return passive.Length + 1;
    }
}
