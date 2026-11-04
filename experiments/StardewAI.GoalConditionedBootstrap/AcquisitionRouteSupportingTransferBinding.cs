using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionRouteSupportingTransferBinding
{
    internal static MaterialTransferPreparationRequest? Create(
        MaterialReservationUpsertRequest[] claims, AcquisitionSupportMaterialRelocation[] moves,
        MaterialTransferIntent? intent)
    {
        if (intent is null || moves.Length == 0) return null;
        var selected = moves.Select(move => claims.Where(claim => claim.ReservationId == move.ReservationId).ToArray()).ToArray();
        if (selected.Any(rows => rows.Length != 1)) return null;
        var routes = selected.Select(rows => rows[0].SourceDecisionId).Distinct(StringComparer.Ordinal).ToArray();
        if (routes.Length != 1 || string.IsNullOrWhiteSpace(routes[0])) return null;
        return new MaterialTransferPreparationRequest
        {
            RouteSourceDecisionId = routes[0], Intent = intent,
            MaterialRelocations = moves.Select(move => new ReservationPortfolioMaterialRelocation
            {
                MaterialReservationId = move.ReservationId, SourceNodeId = move.SourceNodeId,
                SourceSlotIndex = move.SourceSlotIndex, DestinationNodeId = move.DestinationNodeId,
                DestinationSlotIndex = move.DestinationSlotIndex, QualifiedItemId = move.QualifiedItemId, Quantity = move.Quantity
            }).ToArray()
        };
    }

    internal static void Validate(AcquisitionRouteSupportingTransitionRequest request,
        StrategyCommitmentLedger ledger, string? binding, ICollection<string> reasons)
    {
        if (request.SupportTransitionKind != "machine_input_material_transfer")
        {
            if (request.AtomicCommitRequest?.MaterialTransferPreparation is not null || binding is not null)
                reasons.Add("support_unrelated_transfer_preparation");
            return;
        }
        var expected = Create(request.ReservationMaterialClaims, request.SupportMaterialRelocations, request.MaterialTransferIntent);
        var actual = request.AtomicCommitRequest?.MaterialTransferPreparation;
        var prepared = ledger.PreparedMaterialTransfer;
        if (expected is null || actual is null || prepared is null ||
            JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(actual) ||
            !PreparedMaterialTransferContract.IsBound(ledger, binding, request.SupportRequestId,
                request.GoalId, expected.RouteSourceDecisionId) ||
            JsonSerializer.Serialize(prepared.Intent) != JsonSerializer.Serialize(expected.Intent) ||
            JsonSerializer.Serialize(prepared.MaterialRelocations) != JsonSerializer.Serialize(expected.MaterialRelocations) ||
            prepared.SourceStateHash != request.SourceStateHash)
            reasons.Add("support_material_transfer_preparation_binding_invalid");
    }
}
