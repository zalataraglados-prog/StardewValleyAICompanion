using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Execution;

public static class CropSeedMaterialTransferBinding
{
    private const string Prefix = "crop_seed_transfer_";
    private static readonly string[] Names = { Prefix + "version", Prefix + "commitment_id", Prefix + "portfolio_id",
        Prefix + "source_decision_id", Prefix + "ledger_revision", Prefix + "reservation_ids_json" };

    public static bool IsControlParameter(string name) => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static SmallModelActionParameter[] ToParameters(StrategyCommitmentLedger ledger, string commitmentId)
    {
        var prepared = ledger.PreparedMaterialTransfer ?? throw new InvalidOperationException("Owned seed transfer is not prepared.");
        var values = new[] { "1", commitmentId, prepared.PortfolioId, prepared.RouteSourceDecisionId,
            ledger.Revision.ToString(CultureInfo.InvariantCulture),
            JsonSerializer.Serialize(prepared.MaterialRelocations.Select(row => row.MaterialReservationId).OrderBy(id => id, StringComparer.Ordinal).ToArray()) };
        return MaterialTransferIntentBinder.ToParameters(prepared.Intent).Concat(Names.Select((name, index) =>
            new SmallModelActionParameter { Name = name, Value = values[index] })).Append(new SmallModelActionParameter
            { Name = PreparedMaterialTransferContract.BindingParameter, Value = prepared.BindingSha256 }).ToArray();
    }

    public static bool IsBound(ActionQueueItem item, StrategyCommitmentLedger ledger)
    {
        var parameters = item.NormalizedCommand.Parameters ?? Array.Empty<SmallModelActionParameter>();
        if (item.OptionId != "executor.transfer_material" || item.NormalizedCommand.OptionId != item.OptionId ||
            parameters.Count(row => row.Name == PreparedMaterialTransferContract.BindingParameter) != 1 ||
            parameters.Any(row => MuseumOutputCustodyTransferBinding.IsControlParameter(row.Name) ||
                row.Name.StartsWith("acquisition_", StringComparison.OrdinalIgnoreCase)) ||
            Names.Any(name => parameters.Count(row => row.Name == name) != 1) ||
            parameters.Where(row => IsControlParameter(row.Name)).Any(row => !Names.Contains(row.Name))) return false;
        string Read(string name) => parameters.SingleOrDefault(row => row.Name == name)?.Value ?? string.Empty;
        var prepared = ledger.PreparedMaterialTransfer;
        if (Read(Names[0]) != "1" || prepared is null || item.NormalizedCommand.GoalId != "earn_money" ||
            !int.TryParse(Read(Names[4]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision) || revision != ledger.Revision ||
            !PreparedMaterialTransferContract.IsBound(ledger, Read(PreparedMaterialTransferContract.BindingParameter),
                Read(Names[2]), "earn_money", Read(Names[3])) ||
            !MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent) ||
            JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(prepared.Intent)) return false;
        var crops = ledger.CropPlantingCommitments.Where(row => row.CommitmentId == Read(Names[1]) &&
            row.Status == StrategyCommitmentStatuses.Active).ToArray();
        if (crops.Length != 1 || crops[0].SourceDecisionId != prepared.RouteSourceDecisionId ||
            crops[0].SeedQualifiedItemId != prepared.Intent.QualifiedItemId) return false;
        if (!long.TryParse(ledger.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actor) ||
            actor != prepared.BeforeInventoryGraph.PlayerId) return false;
        var sources = prepared.BeforeInventoryGraph.InventoryNodes.Where(row => row.NodeId == prepared.Intent.SourceNodeId).ToArray();
        var destinations = prepared.BeforeInventoryGraph.InventoryNodes.Where(row => row.NodeId == prepared.Intent.DestinationNodeId).ToArray();
        if (sources.Length != 1 || destinations.Length != 1) return false;
        var source = sources[0];
        var destination = destinations[0];
        if (source?.InventoryKind != "chest" || destination?.InventoryKind != "player_inventory" ||
            !source.ActorUseAuthorized || !destination.ActorUseAuthorized ||
            destination.OwnerPlayerId != prepared.BeforeInventoryGraph.PlayerId) return false;
        string[]? ids;
        try { ids = JsonSerializer.Deserialize<string[]>(Read(Names[5])); }
        catch (JsonException) { return false; }
        var moved = prepared.MaterialRelocations;
        if (ids is null || !ids.SequenceEqual(moved.Select(row => row.MaterialReservationId).OrderBy(id => id, StringComparer.Ordinal)) ||
            moved.Sum(row => (long)row.Quantity) != prepared.Intent.Quantity || prepared.Intent.Quantity > crops[0].TileCount) return false;
        return moved.All(move => ledger.MaterialReservations.Count(row => row.ReservationId == move.MaterialReservationId &&
            row.Status == StrategyCommitmentStatuses.Active && row.SourceDecisionId == crops[0].SourceDecisionId &&
            row.GoalId == "earn_money" && row.Purpose == "crop_seed_supply:" + crops[0].CommitmentId &&
            row.QualifiedItemId == crops[0].SeedQualifiedItemId && row.Quantity == move.Quantity &&
            row.NodeId == prepared.Intent.SourceNodeId && row.SlotIndex == prepared.Intent.SourceSlotIndex &&
            row.OwnerPlayerId == actor && row.OutputCustody is null) == 1);
    }
}
