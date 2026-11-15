using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class CropSaleStockReservationContract
{
    public const string Schema = "crop_sale_stock_reservation.v1";
    public const string TransferableSchema = "crop_sale_stock_reservation.v2";

    public static string Purpose(string commitmentId, string outputRetentionKey, string inventoryIdentitySha256) =>
        JsonSerializer.Serialize(new CropSaleStockReservationPurpose
        {
            SchemaVersion = Schema, CommitmentId = commitmentId, OutputRetentionKey = outputRetentionKey, InventoryIdentitySha256 = inventoryIdentitySha256
        });

    public static string TransferablePurpose(string commitmentId, string outputRetentionKey, string inventoryIdentitySha256,
        int originSlotIndex) => JsonSerializer.Serialize(new CropSaleStockReservationPurpose
        {
            SchemaVersion = TransferableSchema, CommitmentId = commitmentId, OutputRetentionKey = outputRetentionKey,
            InventoryIdentitySha256 = inventoryIdentitySha256, OriginSlotIndex = originSlotIndex
        });

    public static bool TryRead(MaterialReservation claim, out CropSaleStockReservationPurpose? purpose)
    {
        purpose = null;
        if (string.IsNullOrWhiteSpace(claim.Purpose) || !claim.Purpose.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
        try { purpose = JsonSerializer.Deserialize<CropSaleStockReservationPurpose>(claim.Purpose); }
        catch (JsonException) { return false; }
        return purpose is not null &&
            !string.IsNullOrWhiteSpace(purpose.CommitmentId) && !string.IsNullOrWhiteSpace(purpose.OutputRetentionKey) &&
            MaterialOutputCustodyContract.IsLowerSha256(purpose.InventoryIdentitySha256) &&
            claim.OutputCustody is null && !string.IsNullOrWhiteSpace(claim.SourceDecisionId) && claim.Quantity > 0 &&
            claim.SlotIndex >= 0 && claim.GoalId == "earn_money" && PositionMatchesVersion(claim, purpose);
    }

    private static bool PositionMatchesVersion(MaterialReservation claim, CropSaleStockReservationPurpose purpose)
    {
        var playerNode = "player:" + claim.OwnerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return purpose.SchemaVersion switch
        {
            Schema => claim.ReservationId == ReservationId(claim.SourceDecisionId, claim.SlotIndex) && claim.NodeId == playerNode,
            TransferableSchema => purpose.OriginSlotIndex is >= 0 &&
                claim.ReservationId == ReservationId(claim.SourceDecisionId, purpose.OriginSlotIndex.Value) &&
                (claim.NodeId == playerNode || claim.NodeId?.StartsWith("chest:", StringComparison.Ordinal) == true),
            _ => false
        };
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
    [JsonPropertyName("origin_slot_index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OriginSlotIndex { get; set; }
}
