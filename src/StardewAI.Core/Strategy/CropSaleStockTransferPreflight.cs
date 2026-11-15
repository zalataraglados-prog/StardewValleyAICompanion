using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public static class CropSaleStockTransferPreflight
{
    // Resource preflight only. The original dispatch, native receipt and settlement guards remain authoritative.
    public static string[] Evaluate(string optionId, SmallModelActionParameter[] parameters,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger) =>
        EvaluateCore(optionId, parameters, snapshot, ledger, out _);

    internal static string[] EvaluateCandidate(string optionId, SmallModelActionParameter[] parameters,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger, out string? preparedBinding) =>
        EvaluateCore(optionId, parameters, snapshot, ledger, out preparedBinding);

    private static string[] EvaluateCore(string optionId, SmallModelActionParameter[] parameters,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger, out string? preparedBinding)
    {
        preparedBinding = null;
        if (optionId is not "inventory.transfer_item" and not "executor.transfer_material" || ledger?.MaterialReservations is null)
            return Array.Empty<string>();
        var phases = parameters.Where(row => row.Name == MaterialTransferIntentBinder.PhaseParameter).ToArray();
        if (optionId == "inventory.transfer_item" && phases is [var phase] && phase.Value == MaterialTransferIntentBinder.ApproachPhase)
            return Array.Empty<string>();
        var stock = ledger.MaterialReservations.Where(row => row is not null && row.Status == StrategyCommitmentStatuses.Active &&
            CropSaleStockReservationContract.TryRead(row, out var purpose) &&
            purpose!.SchemaVersion == CropSaleStockReservationContract.TransferableSchema).ToArray();
        if (stock.Length == 0) return Array.Empty<string>();
        if (!MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent))
            return new[] { "crop_sale_stock_transfer_intent_unavailable" };
        if (!stock.Any(row => row.NodeId == intent!.SourceNodeId || row.NodeId == intent.DestinationNodeId))
            return Array.Empty<string>();
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            snapshot.SaveId.Value != ledger.SaveId || snapshot.PlayerId.Value != ledger.PlayerId ||
            !MaterialInventoryGraphReader.TryRead(snapshot, out var graph))
            return new[] { "crop_sale_stock_transfer_source_unavailable" };
        var prepared = ledger.PreparedMaterialTransfer;
        var bindings = parameters.Where(row => row.Name == PreparedMaterialTransferContract.BindingParameter).ToArray();
        if (prepared is null || bindings.Length != 1)
            return new[] { "crop_sale_stock_transfer_preparation_required" };
        if (!PreparedMaterialTransferContract.IsBound(ledger, bindings[0].Value, prepared.PortfolioId,
                prepared.GoalId, prepared.RouteSourceDecisionId) || snapshot.GameTick < prepared.SourceGameTick ||
            JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(prepared.Intent))
            return new[] { "crop_sale_stock_transfer_preparation_drifted" };
        var projection = new MaterialTransferProjector().ProjectReservations(graph!, intent!, ledger, prepared.MaterialRelocations);
        if (projection.Status != "projected") return projection.BlockingReasons;
        if (JsonSerializer.Serialize(projection) != JsonSerializer.Serialize(prepared.ReservationProjection))
            return new[] { "crop_sale_stock_transfer_preparation_drifted" };
        preparedBinding = bindings[0].Value;
        return Array.Empty<string>();
    }
}
