using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Strategy;

// Prospective support only: the original commit must be submitted and verified before fresh compilation.
public sealed partial class CropSaleStockTransferPreparation
{
    public const string RequestParameter = "crop_sale_stock_transfer_prepare_request_json";
    public ReservationPortfolioCommitRequest Request { get; }
    public StrategyCommitmentLedger PreviewLedger { get; }

    private CropSaleStockTransferPreparation(ReservationPortfolioCommitRequest request, StrategyCommitmentLedger preview)
    { Request = request; PreviewLedger = preview; }

    public static CropSaleStockTransferPreparation? Create(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        SmallModelActionParameter[] parameters, out string[] reasons)
    {
        reasons = Array.Empty<string>();
        if (ledger.PreparedMaterialTransfer is not null ||
            !MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent) || intent is null ||
            !MaterialTransferIntentBinder.TryReadGraph(snapshot, out var graph))
        { reasons = new[] { "crop_sale_stock_prepare_source_unavailable" }; return null; }
        var claims = ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.NodeId == intent.SourceNodeId && row.SlotIndex == intent.SourceSlotIndex &&
            row.QualifiedItemId == intent.QualifiedItemId && CropSaleStockReservationContract.TryRead(row, out var purpose) &&
            CropSaleStockReservationContract.IsTransferable(purpose!)).OrderBy(row => row.ReservationId, StringComparer.Ordinal).ToArray();
        if (claims.Length == 0) return null;
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            snapshot.SaveId.Value != ledger.SaveId || snapshot.PlayerId.Value != ledger.PlayerId ||
            claims.Select(row => row.SourceDecisionId).Distinct(StringComparer.Ordinal).Count() != 1 ||
            claims.Sum(row => (long)row.Quantity) > intent.Quantity)
        { reasons = new[] { "crop_sale_stock_prepare_claim_scope_unavailable" }; return null; }
        var physical = new MaterialTransferProjector().Project(graph!, intent);
        if (physical.Status != "projected") { reasons = physical.BlockingReasons; return null; }
        var destinations = physical.DestinationSlotChanges.Where(row => row.StackAfter > row.StackBefore).ToArray();
        if (destinations.Length == 0)
        { reasons = new[] { "crop_sale_stock_prepare_destination_unavailable" }; return null; }
        var portfolio = "crop-sale-transfer:" + Hash(JsonSerializer.Serialize(new
        { snapshot.StateHash, ledger.LedgerId, ledger.Revision, Intent = intent, Claims = claims.Select(row => row.ReservationId).ToArray() }));
        var request = new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = portfolio, SourceDecisionId = portfolio, GoalId = "earn_money",
            MaterialTransferPreparation = new MaterialTransferPreparationRequest
            {
                RouteSourceDecisionId = claims[0].SourceDecisionId, Intent = intent,
                MaterialRelocations = claims.Select(row => new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = row.ReservationId, SourceNodeId = intent.SourceNodeId, SourceSlotIndex = intent.SourceSlotIndex,
                    DestinationNodeId = intent.DestinationNodeId, DestinationSlotIndex = destinations[0].SlotIndex,
                    QualifiedItemId = intent.QualifiedItemId, Quantity = row.Quantity
                }).ToArray()
            }
        };
        if (destinations.Length > 1 && !AllocatePartitions(request, ledger, claims, destinations, out reasons)) return null;
        var preview = new ReservationPortfolioLedgerService().Commit(ledger, snapshot, request, ledger.UpdatedAt);
        if (!preview.Accepted || preview.Ledger?.PreparedMaterialTransfer is null)
        { reasons = preview.Errors; return null; }
        if (!CropSaleStockMaterialTransferBinding.ParametersAreBound(CropSaleStockMaterialTransferBinding.ToParameters(preview.Ledger),
                request.GoalId, preview.Ledger))
        { reasons = new[] { "crop_sale_stock_prepare_control_binding_unavailable" }; return null; }
        return new(request, preview.Ledger);
    }

    private static string Hash(string raw)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)).Select(value => value.ToString("x2")));
    }
}
