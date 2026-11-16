using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Execution;

public static class CropSaleStockMaterialTransferBinding
{
    private const string Prefix = "crop_sale_stock_transfer_";
    private static readonly string[] Names = { Prefix + "version", Prefix + "portfolio_id",
        Prefix + "source_decision_id", Prefix + "ledger_revision", Prefix + "reservation_ids_json" };

    public static bool IsControlParameter(string name) => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    internal static bool IsForeignControlParameter(string name) => CropSeedMaterialTransferBinding.IsControlParameter(name) ||
        MuseumOutputCustodyTransferBinding.IsControlParameter(name) || name.StartsWith("acquisition_", StringComparison.OrdinalIgnoreCase);

    public static SmallModelActionParameter[] ToParameters(StrategyCommitmentLedger ledger)
    {
        var prepared = ledger.PreparedMaterialTransfer ?? throw new InvalidOperationException("Sale-stock transfer is not prepared.");
        var values = new[] { CropSaleStockTransferLeaseContract.IsBound(ledger) ? "2" : "1", prepared.PortfolioId, prepared.RouteSourceDecisionId,
            ledger.Revision.ToString(CultureInfo.InvariantCulture),
            JsonSerializer.Serialize(prepared.MaterialRelocations.Select(row => row.MaterialReservationId).OrderBy(id => id, StringComparer.Ordinal).ToArray()) };
        return MaterialTransferIntentBinder.ToParameters(prepared.Intent).Concat(Names.Select((name, index) =>
            new SmallModelActionParameter { Name = name, Value = values[index] })).Append(new SmallModelActionParameter
            { Name = PreparedMaterialTransferContract.BindingParameter, Value = prepared.BindingSha256 }).ToArray();
    }

    public static bool IsBound(ActionQueueItem item, StrategyCommitmentLedger ledger) =>
        item.OptionId == "executor.transfer_material" && item.NormalizedCommand.OptionId == item.OptionId &&
        ParametersAreBound(item.NormalizedCommand.Parameters, item.NormalizedCommand.GoalId, ledger);

    internal static bool ParametersAreBound(SmallModelActionParameter[] parameters, string? goalId, StrategyCommitmentLedger ledger)
    {
        if (parameters is null || parameters.Any(row => row is null) ||
            parameters.Count(row => row.Name == PreparedMaterialTransferContract.BindingParameter) != 1 ||
            parameters.Any(row => IsForeignControlParameter(row.Name)) ||
            Names.Any(name => parameters.Count(row => row.Name == name) != 1) ||
            parameters.Where(row => IsControlParameter(row.Name)).Any(row => !Names.Contains(row.Name))) return false;
        string Read(string name) => parameters.SingleOrDefault(row => row.Name == name)?.Value ?? string.Empty;
        var prepared = ledger.PreparedMaterialTransfer;
        var lease = Read(Names[0]) == "2";
        if (Read(Names[0]) is not "1" and not "2" || prepared is null || goalId != prepared.GoalId ||
            !int.TryParse(Read(Names[3]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision) || revision != ledger.Revision ||
            !PreparedMaterialTransferContract.IsBound(ledger, Read(PreparedMaterialTransferContract.BindingParameter),
                Read(Names[1]), goalId, Read(Names[2])) ||
            !MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent) ||
            JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(prepared.Intent) ||
            !long.TryParse(ledger.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actor) ||
            actor != prepared.BeforeInventoryGraph.PlayerId) return false;
        var source = prepared.BeforeInventoryGraph.InventoryNodes.Where(row => row.NodeId == prepared.Intent.SourceNodeId).ToArray();
        var destination = prepared.BeforeInventoryGraph.InventoryNodes.Where(row => row.NodeId == prepared.Intent.DestinationNodeId).ToArray();
        if (source.Length != 1 || destination.Length != 1 || !source[0].ActorUseAuthorized || !destination[0].ActorUseAuthorized ||
            !((source[0].InventoryKind == "chest" && destination[0].InventoryKind == "player_inventory") ||
              (source[0].InventoryKind == "player_inventory" && destination[0].InventoryKind == "chest"))) return false;
        var player = source[0].InventoryKind == "player_inventory" ? source[0] : destination[0];
        if (player.OwnerPlayerId != actor || player.NodeId != "player:" + ledger.PlayerId) return false;
        string[]? ids;
        try { ids = JsonSerializer.Deserialize<string[]>(Read(Names[4])); }
        catch (JsonException) { return false; }
        var moved = prepared.MaterialRelocations;
        if (ids is null || !ids.SequenceEqual(moved.Select(row => row.MaterialReservationId).OrderBy(id => id, StringComparer.Ordinal))) return false;
        if (lease) return CropSaleStockTransferLeaseContract.IsBound(ledger) && moved.All(move =>
            ledger.MaterialReservations.Count(row => row.ReservationId == move.MaterialReservationId &&
                row.Status == StrategyCommitmentStatuses.Active && row.OwnerPlayerId == actor &&
                row.NodeId == prepared.Intent.SourceNodeId && row.SlotIndex == prepared.Intent.SourceSlotIndex) == 1);
        return moved.All(move => ledger.MaterialReservations.Count(row => row.ReservationId == move.MaterialReservationId &&
            row.Status == StrategyCommitmentStatuses.Active && row.SourceDecisionId == prepared.RouteSourceDecisionId &&
            row.GoalId == prepared.GoalId &&
            row.Quantity == move.Quantity && row.NodeId == prepared.Intent.SourceNodeId && row.SlotIndex == prepared.Intent.SourceSlotIndex &&
            row.QualifiedItemId == prepared.Intent.QualifiedItemId && row.OwnerPlayerId == actor &&
            CropSaleStockReservationContract.TryRead(row, out var purpose) &&
            CropSaleStockReservationContract.IsTransferable(purpose!)) == 1);
    }
}
