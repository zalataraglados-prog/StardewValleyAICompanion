using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class CropSaleStockReservationContract
{
    public const string Schema = "crop_sale_stock_reservation.v1";

    public static string Purpose(string commitmentId, string outputRetentionKey, string inventoryIdentitySha256) =>
        JsonSerializer.Serialize(new CropSaleStockReservationPurpose
        {
            SchemaVersion = Schema, CommitmentId = commitmentId, OutputRetentionKey = outputRetentionKey, InventoryIdentitySha256 = inventoryIdentitySha256
        });

    public static bool TryRead(MaterialReservation claim, out CropSaleStockReservationPurpose? purpose)
    {
        purpose = null;
        if (string.IsNullOrWhiteSpace(claim.Purpose) || !claim.Purpose.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
        try { purpose = JsonSerializer.Deserialize<CropSaleStockReservationPurpose>(claim.Purpose); }
        catch (JsonException) { return false; }
        return purpose is not null && purpose.SchemaVersion == Schema &&
            !string.IsNullOrWhiteSpace(purpose.CommitmentId) && !string.IsNullOrWhiteSpace(purpose.OutputRetentionKey) &&
            MaterialOutputCustodyContract.IsLowerSha256(purpose.InventoryIdentitySha256) &&
            claim.OutputCustody is null && !string.IsNullOrWhiteSpace(claim.SourceDecisionId) && claim.Quantity > 0 &&
            claim.SlotIndex >= 0 && claim.ReservationId == ReservationId(claim.SourceDecisionId, claim.SlotIndex) &&
            claim.NodeId == "player:" + claim.OwnerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) && claim.GoalId == "earn_money";
    }

    public static string ReservationId(string producerStepId, int slotIndex) => producerStepId + ":crop-sale-stock:" +
        slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class CropSaleStockReservationPurpose
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;
    [JsonPropertyName("output_retention_key")]
    public string OutputRetentionKey { get; set; } = string.Empty;
    [JsonPropertyName("inventory_identity_sha256")]
    public string InventoryIdentitySha256 { get; set; } = string.Empty;
}
