using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

// A real, temporary reservation of unreserved moved units; never a crop-output claim.
public static class CropSaleStockTransferLeaseContract
{
    public const string Schema = "inventory_transfer_material_lease.v1";
    public static string ReservationId(string portfolioId, int slot) => portfolioId + ":inventory-transfer:" + slot.ToString(CultureInfo.InvariantCulture);

    public static bool IsLeaseRequest(ReservationPortfolioCommitRequest request) =>
        request.MaterialClaims is { Length: > 0 } && request.MaterialClaims.All(row => row is not null && Read(row.Purpose) is not null);

    public static bool IsBound(StrategyCommitmentLedger ledger)
    {
        var transfer = ledger.PreparedMaterialTransfer;
        return transfer is not null && transfer.MaterialRelocations is { Length: > 0 } &&
            transfer.MaterialRelocations.All(move => ledger.MaterialReservations.Count(claim =>
                claim.ReservationId == move.MaterialReservationId && IsLease(claim, transfer, move)) == 1) &&
            PreparedMaterialTransferContract.IsBound(ledger, transfer.BindingSha256, transfer.PortfolioId,
                transfer.GoalId, transfer.RouteSourceDecisionId);
    }

    private static bool IsLease(MaterialReservation claim, PreparedMaterialTransfer transfer, ReservationPortfolioMaterialRelocation move)
    {
        var purpose = Read(claim.Purpose);
        return purpose is not null && purpose.PortfolioId == transfer.PortfolioId &&
            purpose.SourceStateHash == transfer.SourceStateHash && purpose.DestinationSlotIndex == move.DestinationSlotIndex &&
            claim.ReservationId == ReservationId(transfer.PortfolioId, purpose.DestinationSlotIndex) &&
            claim.GoalId == transfer.GoalId && claim.SourceDecisionId == transfer.PortfolioId &&
            transfer.RouteSourceDecisionId == transfer.PortfolioId && claim.OutputCustody is null &&
            claim.QualifiedItemId == transfer.Intent.QualifiedItemId && claim.Quantity == move.Quantity;
    }

    internal static string Purpose(string portfolioId, string stateHash, int slot) => JsonSerializer.Serialize(new LeasePurpose
        { SchemaVersion = Schema, PortfolioId = portfolioId, SourceStateHash = stateHash, DestinationSlotIndex = slot });

    private static LeasePurpose? Read(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("{", StringComparison.Ordinal)) return null;
        try
        {
            var value = JsonSerializer.Deserialize<LeasePurpose>(text);
            return value is { SchemaVersion: Schema, DestinationSlotIndex: >= 0 } &&
                !string.IsNullOrWhiteSpace(value.PortfolioId) && !string.IsNullOrWhiteSpace(value.SourceStateHash) ? value : null;
        }
        catch (JsonException) { return null; }
    }

    private sealed class LeasePurpose
    {
        [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("portfolio_id")] public string PortfolioId { get; set; } = string.Empty;
        [JsonPropertyName("source_state_hash")] public string SourceStateHash { get; set; } = string.Empty;
        [JsonPropertyName("destination_slot_index")] public int DestinationSlotIndex { get; set; } = -1;
    }
}
